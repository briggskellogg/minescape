using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MineDeck.Bridge;
using MineDeck.Configuration;
using MineDeck.Models;
using MineDeck.Security;
using MineDeck.Services;
using MineDeck.State;

namespace MineDeck.Web;

public static class ApiEndpoints
{
    public static void MapMineDeckApi(this WebApplication app)
    {
        app.MapGet("/api/public/status", (StatusStore status) => Results.Ok(status.Current));

        app.MapPost("/api/auth/login", LoginAsync);
        app.MapGet("/api/auth/session", (ClaimsPrincipal user) =>
        {
            if (user.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            return Results.Ok(new LoginResponse(user.FindFirstValue("csrf")!, DateTimeOffset.FromUnixTimeSeconds(long.Parse(user.FindFirstValue("exp")!))));
        }).RequireAuthorization();
        app.MapPost("/api/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization();

        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/status", (StatusStore status) => ApiEnvelope<RuntimeStatus>.Success(status.Current));
        api.MapGet("/snapshot", async (IBridgeGateway bridge, CancellationToken ct) => await bridge.SnapshotAsync(ct));
        api.MapGet("/backups", async (BackupService backups, CancellationToken ct) => ApiEnvelope<IReadOnlyList<BackupRecord>>.Success(await backups.ListAsync(ct)));
        api.MapGet("/local-activity", async (AtomicJsonStateStore state, CancellationToken ct) =>
            ApiEnvelope<IReadOnlyList<ActivityEvent>>.Success((await state.ReadAsync(ct)).LocalAudit.OrderByDescending(x => x.At).ToArray()));
        api.MapGet("/updates", () => ApiEnvelope<object>.Success(new
        {
            worldGameplay = "Frozen at V1.0",
            worldGeneration = "Frozen at V1.0",
            matchaData = "Frozen at 1.02",
            permittedLane = "A separately tested Matcha visual-resource derivative only",
            automaticUpdates = false
        }));
        api.MapGet("/remote", () => ApiEnvelope<object>.Success(new
        {
            adapterAvailable = false,
            message = "Tailscale status adapter is not configured yet. MineDeck remains loopback-only.",
            publicPortOpen = false
        }, adapterAvailable: false));

        api.MapPost("/server/start", async (ServerSupervisor server, AdminAuditService audit, CancellationToken ct) =>
            await AuditAsync("server.start", "Start MineScape", () => server.StartProductionAsync(ct), audit, ct));
        api.MapPost("/server/stop", async (ServerSupervisor server, AdminAuditService audit, CancellationToken ct) =>
            await AuditAsync("server.stop", "Cleanly stop MineScape", () => server.StopProductionAsync(ct), audit, ct));
        api.MapPost("/minejammer/start", async (ServerSupervisor server, AdminAuditService audit, CancellationToken ct) =>
            await AuditAsync("minejammer.start", "Start MineJammer", () => server.StartMineJammerAsync(ct), audit, ct));
        api.MapPost("/minejammer/stop", async (ServerSupervisor server, AdminAuditService audit, CancellationToken ct) =>
            await AuditAsync("minejammer.stop", "Cleanly stop MineJammer", () => server.StopMineJammerAsync(ct), audit, ct));

        api.MapPost("/players/approve", (PlayerDecision request, IBridgeGateway bridge, CancellationToken ct) => bridge.ApprovePlayerAsync(request, ct));
        api.MapPost("/players/deny", (PlayerDenial request, IBridgeGateway bridge, CancellationToken ct) => bridge.DenyPlayerAsync(request, ct));
        api.MapPost("/players/revoke", (PlayerDenial request, IBridgeGateway bridge, CancellationToken ct) => bridge.RevokePlayerAsync(request, ct));
        api.MapPost("/family-assist", (FamilyAssistRequest request, IBridgeGateway bridge, CancellationToken ct) => bridge.RequestFamilyAssistAsync(request, ct));
        api.MapPost("/characters/pardon", async (SensitiveRequest<CharacterDecision> envelope, HttpContext context, MineDeckOptions options, LoginThrottle throttle, IBridgeGateway bridge, CancellationToken ct) =>
            await SensitiveAsync(envelope, context, options, throttle, bridge.PardonCharacterAsync, ct));
        api.MapPost("/characters/new", async (SensitiveRequest<NewCharacterRequest> envelope, HttpContext context, MineDeckOptions options, LoginThrottle throttle, IBridgeGateway bridge, CancellationToken ct) =>
            await SensitiveAsync(envelope, context, options, throttle, bridge.BeginNewCharacterAsync, ct));
        api.MapPost("/settlements/register", (SettlementRegistration request, IBridgeGateway bridge, CancellationToken ct) => bridge.RegisterSettlementAsync(request, ct));
        api.MapPost("/rewards/correct", async (SensitiveRequest<RewardCorrection> envelope, HttpContext context, MineDeckOptions options, LoginThrottle throttle, IBridgeGateway bridge, CancellationToken ct) =>
            await SensitiveAsync(envelope, context, options, throttle, bridge.CorrectRewardAsync, ct));
        api.MapPost("/steward/lease", async (SensitiveRequest<StewardLeaseRequest> envelope, HttpContext context, MineDeckOptions options, LoginThrottle throttle, IBridgeGateway bridge, CancellationToken ct) =>
            await SensitiveAsync(envelope, context, options, throttle, bridge.RequestStewardLeaseAsync, ct));
        api.MapPost("/steward/end", (IBridgeGateway bridge, CancellationToken ct) => bridge.EndStewardLeaseAsync(ct));
        api.MapPost("/minejammer/compare", (PromotionRequest request, IBridgeGateway bridge, CancellationToken ct) => bridge.ComparePromotionAsync(request, ct));
        api.MapPost("/minejammer/commit", async (SensitiveRequest<PromotionRequest> envelope, HttpContext context, MineDeckOptions options, LoginThrottle throttle, IBridgeGateway bridge, CancellationToken ct) =>
            await SensitiveAsync(envelope, context, options, throttle, bridge.CommitPromotionAsync, ct));
        api.MapPost("/backups/create", async (BackupRequest request, BackupService backups, CancellationToken ct) => await backups.CreateAsync(request.Reason, ct));
        api.MapPost("/backups/{id}/verify", async (string id, BackupService backups, CancellationToken ct) => await backups.VerifyAsync(id, ct));
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context, MineDeckOptions options, LoginThrottle throttle)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!throttle.CanAttempt(key, out var retry))
            return Results.Json(new { error = $"Too many failed attempts. Retry in {Math.Ceiling(retry.TotalMinutes)} minute(s)." }, statusCode: StatusCodes.Status429TooManyRequests);
        if (!PasswordHasher.Verify(request.Password, options.Admin.PasswordHash))
        {
            throttle.RecordFailure(key);
            return Results.Unauthorized();
        }

        throttle.RecordSuccess(key);
        var expires = DateTimeOffset.UtcNow.AddHours(options.Admin.CookieHours);
        var csrf = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "MineDeck Administrator"),
            new Claim("csrf", csrf),
            new Claim("exp", expires.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = expires,
            AllowRefresh = false
        }).ConfigureAwait(false);
        return Results.Ok(new LoginResponse(csrf, expires));
    }

    private static async Task<OperationResponse> AuditAsync(
        string kind,
        string summary,
        Func<Task<OperationResponse>> operation,
        AdminAuditService audit,
        CancellationToken cancellationToken)
    {
        var result = await operation().ConfigureAwait(false);
        await audit.AppendAsync(kind, summary, result.Accepted ? "accepted" : $"rejected: {result.Message}", cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static async Task<IResult> SensitiveAsync<T>(
        SensitiveRequest<T> envelope,
        HttpContext context,
        MineDeckOptions options,
        LoginThrottle throttle,
        Func<T, CancellationToken, Task<BridgeResponse>> operation,
        CancellationToken cancellationToken)
    {
        var key = "sensitive:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (!throttle.CanAttempt(key, out var retry))
            return Results.Json(new BridgeResponse(false, $"Reauthentication locked. Retry in {Math.Ceiling(retry.TotalMinutes)} minute(s)."), statusCode: StatusCodes.Status429TooManyRequests);
        if (!PasswordHasher.Verify(envelope.Password, options.Admin.PasswordHash))
        {
            throttle.RecordFailure(key);
            return Results.Json(new BridgeResponse(false, "Administrator reauthentication failed."), statusCode: StatusCodes.Status403Forbidden);
        }
        throttle.RecordSuccess(key);
        return Results.Ok(await operation(envelope.Request, cancellationToken).ConfigureAwait(false));
    }
}
