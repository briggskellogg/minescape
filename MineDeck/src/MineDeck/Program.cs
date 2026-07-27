using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Serialization;
using MineDeck.Bridge;
using MineDeck.Configuration;
using MineDeck.Protocol;
using MineDeck.Security;
using MineDeck.Services;
using MineDeck.State;
using MineDeck.Tray;
using MineDeck.Web;

namespace MineDeck;

public static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        StartupMode mode;
        try { mode = StartupMode.Parse(args); }
        catch (ArgumentException exception)
        {
            MessageBox.Show(exception.Message, "MineDeck startup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 64;
        }
        var configPath = Environment.GetEnvironmentVariable("MINEDECK_CONFIG")
            ?? Environment.GetEnvironmentVariable("MINDECK_CONFIG")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MineScape", "MineDeck", "appsettings.json");
        var optionsResult = LoadOptions(configPath);
        if (optionsResult.Options is null)
        {
            ShowConfigurationError(optionsResult.Error!, mode.Background);
            return 2;
        }
        var options = optionsResult.Options;

        using var broker = new SingleInstanceBroker(options.InstanceName, options.DataDirectory);
        if (!broker.TryAcquire())
        {
            if (mode.Background) return 0;
            return await RunAttachedTrayAsync(mode, options, broker).ConfigureAwait(false);
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // MineDeck's shortcut modes are application commands, not ASP.NET configuration.
            Args = Array.Empty<string>(),
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = "wwwroot"
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new DailyFileLoggerProvider(Path.Combine(options.DataDirectory, "logs")));
        builder.WebHost.UseUrls(options.Dashboard.ListenUrl);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new AtomicJsonStateStore(options.DataDirectory));
        builder.Services.AddSingleton<MinecraftPingClient>();
        builder.Services.AddSingleton<StatusStore>();
        builder.Services.AddSingleton<LoginThrottle>();
        builder.Services.AddSingleton<AdminAuditService>();
        builder.Services.AddSingleton<ServerSupervisor>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<LauncherService>();
        builder.Services.AddSingleton<SupervisorCommandHandler>();
        builder.Services.AddSingleton<IBridgeGateway>(_ =>
            string.Equals(options.Bridge.Mode, "Mock", StringComparison.OrdinalIgnoreCase)
                ? new MockBridgeGateway()
                : new HttpBridgeGateway(options));
        builder.Services.AddHostedService<StatusMonitor>();
        // Session keys intentionally live only in supervisor memory. This fails closed under
        // unattended tasks with no loaded Windows profile and invalidates every cookie on restart.
        builder.Services.AddDataProtection()
            .SetApplicationName($"MineDeck:{options.InstanceName}")
            .UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(cookie =>
        {
            cookie.Cookie.Name = "MineDeck.Admin";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            cookie.SlidingExpiration = false;
            cookie.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            await next().ConfigureAwait(false);
        });
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = context => context.Context.Response.Headers["Cache-Control"] = "no-store" });
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api") &&
                context.Request.Method is not ("GET" or "HEAD" or "OPTIONS") &&
                !context.Request.Path.StartsWithSegments("/api/auth/login") &&
                context.User.Identity?.IsAuthenticated == true)
            {
                var expected = context.User.FindFirstValue("csrf");
                var actual = context.Request.Headers["X-MineDeck-CSRF"].ToString();
                if (string.IsNullOrWhiteSpace(expected) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(actual)))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid anti-forgery token." }).ConfigureAwait(false);
                    return;
                }
            }
            await next().ConfigureAwait(false);
        });
        app.UseAuthorization();
        app.MapMineDeckApi();
        app.MapFallbackToFile("index.html");
        await app.StartAsync().ConfigureAwait(false);

        var handler = app.Services.GetRequiredService<SupervisorCommandHandler>();
        using var pipeLifetime = CancellationTokenSource.CreateLinkedTokenSource(app.Lifetime.ApplicationStopping);
        var pipeTask = Task.Run(() => broker.ListenAsync(handler.HandleAsync, pipeLifetime.Token));

        if (mode.Background)
        {
            await app.WaitForShutdownAsync().ConfigureAwait(false);
            pipeLifetime.Cancel();
            await IgnoreCancellationAsync(pipeTask).ConfigureAwait(false);
            return 0;
        }

        SupervisorReply? startReply = null;
        if (mode.StartProduction)
            startReply = await handler.HandleAsync(new("start"), CancellationToken.None).ConfigureAwait(false);
        var launcher = app.Services.GetRequiredService<LauncherService>();
        if (mode.OpenDashboard) launcher.OpenDashboard();
        if (startReply is { Accepted: false })
            MessageBox.Show(startReply.Message, "MineDeck did not start MineScape", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        if (mode.Play && startReply is not null)
        {
            var servers = app.Services.GetRequiredService<ServerSupervisor>();
            var online = startReply.Accepted &&
                await servers.WaitForProductionOnlineAsync(CancellationToken.None).ConfigureAwait(false);
            if (mode.ShouldOpenOfficialLauncher(startReply.Accepted, online)) launcher.OpenOfficialLauncher();
        }

        var status = app.Services.GetRequiredService<StatusStore>();
        RunTray(() => new TrayApplicationContext(
                _ => Task.FromResult(status.Current),
                (action, ct) => handler.HandleAsync(new(action), ct),
                launcher));

        pipeLifetime.Cancel();
        await app.StopAsync().ConfigureAwait(false);
        await IgnoreCancellationAsync(pipeTask).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> RunAttachedTrayAsync(StartupMode mode, MineDeckOptions options, SingleInstanceBroker broker)
    {
        SupervisorReply? startReply = null;
        if (mode.StartProduction)
        {
            startReply = await broker.SendAsync(new SupervisorCommand("start"), CancellationToken.None).ConfigureAwait(false);
        }

        var launcher = new LauncherService(options);
        if (mode.OpenDashboard) launcher.OpenDashboard();
        if (startReply is { Accepted: false })
            MessageBox.Show(startReply.Message, "MineDeck did not start MineScape", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        using var statusClient = new PublicStatusClient(options);
        if (mode.Play && startReply is not null)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(options.Minecraft.StartupTimeoutSeconds);
            var online = false;
            while (startReply.Accepted && DateTimeOffset.UtcNow < deadline)
            {
                if ((await statusClient.GetAsync(CancellationToken.None).ConfigureAwait(false)).Production.Online)
                {
                    online = true;
                    break;
                }
                await Task.Delay(2000).ConfigureAwait(false);
            }
            if (mode.ShouldOpenOfficialLauncher(startReply.Accepted, online)) launcher.OpenOfficialLauncher();
        }

        RunTray(() => new TrayApplicationContext(
                statusClient.GetAsync,
                (action, ct) => broker.SendAsync(new SupervisorCommand(action), ct),
                launcher));
        return 0;
    }

    private static (MineDeckOptions? Options, string? Error) LoadOptions(string configPath)
    {
        if (!File.Exists(configPath))
            return (null, $"MineDeck configuration is missing:\n{configPath}\nRun ops\\configure-minedeck.ps1 from the MineScape repository, or create the file manually from the sanitized example.");
        try
        {
            var fullPath = Path.GetFullPath(configPath);
            using var configurationStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var configuration = new ConfigurationBuilder().AddJsonStream(configurationStream).Build();
            var options = configuration.GetSection("MineDeck").Get<MineDeckOptions>() ?? new MineDeckOptions();
            options.ExpandPaths();
            var errors = ConfigurationValidator.Validate(options);
            return errors.Count == 0 ? (options, null) : (null, string.Join(Environment.NewLine, errors));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException)
        {
            return (null, $"Could not load MineDeck configuration: {exception.Message}");
        }
    }

    private static void ShowConfigurationError(string message, bool background)
    {
        if (background) Console.Error.WriteLine(message);
        else MessageBox.Show(message, "MineDeck configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    private static void RunTray(Func<TrayApplicationContext> createContext)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                System.Windows.Forms.Application.Run(createContext());
            }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = false, Name = "MineDeck tray" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new InvalidOperationException("MineDeck tray failed.", failure);
    }

}
