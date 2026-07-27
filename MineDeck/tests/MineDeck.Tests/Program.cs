using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using MineDeck.Bridge;
using MineDeck.Configuration;
using MineDeck.Models;
using MineDeck.Protocol;
using MineDeck.Security;
using MineDeck.Services;
using MineDeck.State;

if (args.Length == 2 && args[0] == "--fake-minecraft" && int.TryParse(args[1], out var fakePort))
{
    await RunSingleMinecraftResponder(fakePort);
    return 0;
}

var tests = new (string Name, Func<Task> Run)[]
{
    ("VarInt framing round-trips protocol edge values", TestVarInts),
    ("Minecraft Server List Ping parses a real framed exchange", TestMinecraftPing),
    ("Tray lifecycle state is derived from ping and operation", TestLifecycle),
    ("Password hashes verify without storing plaintext", TestPasswordHash),
    ("Configuration refuses non-loopback control endpoints", TestConfiguration),
    ("Shortcut startup modes are explicit and mutually exclusive", TestStartupModes),
    ("An online responder cannot bypass inert production or open Play", TestOnlineResponderCannotBypassInertProduction),
    ("Production launch refuses closed or changed assembly records", TestProductionLaunchGate),
    ("Atomic JSON state survives repeated updates", TestAtomicStore),
    ("Single-instance lock is process-safe and releasable", TestSingleInstance),
    ("Attached mode reaches the supervisor over same-user IPC", TestSupervisorIpc),
    ("HTTP adapter matches the current bearer-authenticated Java Bridge", TestCurrentBridgeContract)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS  {test.Name}"); }
    catch (Exception exception) { failures.Add($"FAIL  {test.Name}: {exception.Message}"); Console.Error.WriteLine(failures[^1]); }
}
Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed.");
return failures.Count == 0 ? 0 : 1;

static Task TestVarInts()
{
    foreach (var value in new[] { 0, 1, 127, 128, 255, int.MaxValue, -1, int.MinValue })
    {
        using var stream = new MemoryStream();
        MinecraftPingClient.WriteVarInt(stream, value);
        Assert(stream.Length is >= 1 and <= 5, "VarInt must be one to five bytes.");
        stream.Position = 0;
        Assert(MinecraftPingClient.ReadVarInt(stream) == value, $"VarInt {value} did not round-trip.");
    }
    return Task.CompletedTask;
}

static async Task TestMinecraftPing()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        _ = await ReadPacket(stream);
        _ = await ReadPacket(stream);
        const string json = "{\"version\":{\"name\":\"MineScape test\",\"protocol\":1},\"players\":{\"max\":8,\"online\":2},\"description\":{\"text\":\"Forever world\"}}";
        using var response = new MemoryStream();
        response.WriteByte(0);
        var bytes = Encoding.UTF8.GetBytes(json);
        MinecraftPingClient.WriteVarInt(response, bytes.Length);
        response.Write(bytes);
        await WritePacket(stream, response.ToArray());
        var ping = await ReadPacket(stream);
        await WritePacket(stream, ping);
    });

    var result = await new MinecraftPingClient().PingAsync("127.0.0.1", port, -1, TimeSpan.FromSeconds(3));
    listener.Stop();
    await server;
    Assert(result.Online, result.Error ?? "Ping should be online.");
    Assert(result.OnlinePlayers == 2 && result.MaxPlayers == 8, "Player counts were parsed incorrectly.");
    Assert(result.Version == "MineScape test" && result.Motd == "Forever world", "Status metadata was parsed incorrectly.");
}

static Task TestLifecycle()
{
    var online = new PingResult(true, "test", 0, 8, 1, null, null, DateTimeOffset.UtcNow);
    var offline = online with { Online = false };
    Assert(StatusStore.Classify(online, OperationKind.None, null) == LifecycleState.Online, "Online ping should be green/online.");
    Assert(StatusStore.Classify(offline, OperationKind.None, null) == LifecycleState.Offline, "Offline ping should be gray/offline.");
    Assert(StatusStore.Classify(online, OperationKind.Backup, null) == LifecycleState.Maintenance, "Backup should be amber/maintenance.");
    Assert(StatusStore.Classify(online, OperationKind.None, "manifest mismatch") == LifecycleState.Fault, "Fatal validation should be red/fault.");
    return Task.CompletedTask;
}

static Task TestPasswordHash()
{
    var encoded = PasswordHasher.Hash("correct horse battery staple");
    Assert(PasswordHasher.IsValidEncoding(encoded), "Generated password hash is not valid.");
    Assert(PasswordHasher.Verify("correct horse battery staple", encoded), "Correct password was rejected.");
    Assert(!PasswordHasher.Verify("wrong password", encoded), "Wrong password was accepted.");
    return Task.CompletedTask;
}

static Task TestConfiguration()
{
    var options = new MineDeckOptions();
    options.Admin.PasswordHash = PasswordHasher.Hash("a sufficiently long password");
    options.Dashboard.ListenUrl = "http://0.0.0.0:43117";
    options.Dashboard.PublicBaseUrl = "https://example.com/minedeck";
    options.Bridge.BaseUrl = "http://example.com:8765";
    options.Minecraft.Host = "192.0.2.10";
    options.MineJammer.Host = "198.51.100.20";
    options.Launcher.LaunchTarget = "https://example.com/not-the-launcher";
    var errors = ConfigurationValidator.Validate(options);
    Assert(errors.Count(x => x.Contains("loopback", StringComparison.OrdinalIgnoreCase)) == 5,
        "Dashboard, Bridge, and both Minecraft status endpoints must reject non-loopback addresses.");
    Assert(errors.Any(x => x.Contains("minecraft-launcher:", StringComparison.OrdinalIgnoreCase)),
        "Launcher target must be pinned to the official protocol handler.");
    return Task.CompletedTask;
}

static Task TestStartupModes()
{
    var defaultMode = StartupMode.Parse(Array.Empty<string>());
    Assert(defaultMode.Kind == StartupKind.Dashboard && defaultMode.StartProduction && defaultMode.OpenDashboard,
        "No-argument interactive startup must have the documented dashboard behavior.");
    Assert(StartupMode.Parse(new[] { "--dashboard" }).Kind == StartupKind.Dashboard, "Dashboard shortcut was not recognized.");
    var play = StartupMode.Parse(new[] { "--PLAY" });
    Assert(play.Play && play.StartProduction && play.OpenDashboard, "Play shortcut must start, show MineDeck, and then launch play.");
    var tray = StartupMode.Parse(new[] { "--tray" });
    Assert(tray.TrayOnly && !tray.StartProduction && !tray.OpenDashboard, "Tray mode must not start production or open a browser.");
    var background = StartupMode.Parse(new[] { "--background" });
    Assert(background.Background && !background.StartProduction && !background.OpenDashboard, "Background mode must not start production.");
    AssertThrows<ArgumentException>(() => StartupMode.Parse(new[] { "--plaay" }), "Unknown shortcut arguments must fail closed.");
    AssertThrows<ArgumentException>(() => StartupMode.Parse(new[] { "--play", "--background" }), "Conflicting shortcut modes must fail closed.");
    return Task.CompletedTask;
}

static async Task TestOnlineResponderCannotBypassInertProduction()
{
    var root = Path.Combine(Path.GetTempPath(), "minedeck-tests", Guid.NewGuid().ToString("N"));
    var serverRoot = Path.Combine(root, "server");
    Directory.CreateDirectory(serverRoot);
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    try
    {
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var responder = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            _ = await ReadPacket(stream);
            _ = await ReadPacket(stream);
            const string json = "{\"version\":{\"name\":\"Wrong server\",\"protocol\":1},\"players\":{\"max\":8,\"online\":0},\"description\":{\"text\":\"not MineScape\"}}";
            using var response = new MemoryStream();
            response.WriteByte(0);
            var bytes = Encoding.UTF8.GetBytes(json);
            MinecraftPingClient.WriteVarInt(response, bytes.Length);
            response.Write(bytes);
            await WritePacket(stream, response.ToArray());
            var ping = await ReadPacket(stream);
            await WritePacket(stream, ping);
        });

        var executable = Path.Combine(root, "javaw.exe");
        File.WriteAllBytes(executable, new byte[] { 1 });
        File.WriteAllText(Path.Combine(serverRoot, "assembly-status.json"),
            "{\"schema\":\"minescape.runtime-assembly.v1\",\"instance\":\"MineScape\",\"launch_permitted\":false}");
        var options = new MineDeckOptions
        {
            DataDirectory = Path.Combine(root, "state"),
            Minecraft = new MinecraftOptions
            {
                Host = "127.0.0.1", Port = port, ExecutablePath = executable,
                WorkingDirectory = serverRoot, StartupTimeoutSeconds = 1
            }
        };
        var supervisor = new ServerSupervisor(options, new MinecraftPingClient(), new MockBridgeGateway(),
            new AtomicJsonStateStore(options.DataDirectory), new StatusStore());
        var result = await supervisor.StartProductionAsync(CancellationToken.None);
        Assert(!result.Accepted && result.Message.Contains("release gates are closed", StringComparison.OrdinalIgnoreCase),
            "An online responder must not bypass an inert production assembly record.");
        var play = StartupMode.Parse(new[] { "--play" });
        Assert(!play.ShouldOpenOfficialLauncher(result.Accepted, productionOnline: true),
            "A refused production start must not authorize the official Launcher even if a port answers.");

        listener.Stop();
        try { await responder.WaitAsync(TimeSpan.FromSeconds(1)); }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or TimeoutException) { }
    }
    finally
    {
        listener.Stop();
        Directory.Delete(root, recursive: true);
    }
}

static async Task TestProductionLaunchGate()
{
    var root = Path.Combine(Path.GetTempPath(), "minedeck-tests", Guid.NewGuid().ToString("N"));
    var serverRoot = Path.Combine(root, "server");
    Directory.CreateDirectory(serverRoot);
    try
    {
        var executable = Path.Combine(root, "javaw.exe");
        File.WriteAllBytes(executable, new byte[] { 1 });
        var server = new MinecraftOptions { ExecutablePath = executable, WorkingDirectory = serverRoot };
        File.WriteAllText(Path.Combine(serverRoot, "assembly-status.json"),
            "{\"schema\":\"minescape.runtime-assembly.v1\",\"instance\":\"MineScape\",\"launch_permitted\":false}");
        var closed = RuntimeLaunchValidator.Validate(server, production: true);
        Assert(closed is not null && closed.Contains("release gates are closed", StringComparison.OrdinalIgnoreCase),
            "A closed assembly record must refuse production before any process start.");

        var fabricated = new
        {
            schema = "minescape.runtime-assembly.v1", instance = "MineScape", mode = "release-assembly",
            launch_permitted = true, launcher = "verified-or-installed", seed = "6246468738900744",
            online_mode = true, whitelist = true, eula_accepted_by_automation = false,
            generated_datapacks_missing = Array.Empty<string>(), server_bundle_sha512 = new string('a', 128),
            files = Array.Empty<object>()
        };
        File.WriteAllText(Path.Combine(serverRoot, "assembly-status.json"), JsonSerializer.Serialize(fabricated));
        var fabricatedError = RuntimeLaunchValidator.Validate(server, production: true);
        Assert(fabricatedError is not null && fabricatedError.Contains("server-bundle.lock.json", StringComparison.OrdinalIgnoreCase),
            "A fabricated launch_permitted record without the deterministic bundle lock must fail closed.");

        var launcher = Path.Combine(serverRoot, "fabric-server-launch.jar");
        File.WriteAllBytes(launcher, Encoding.UTF8.GetBytes("launcher"));
        File.WriteAllText(Path.Combine(serverRoot, "eula.txt"), "eula=true\n");
        var serverProperties = Path.Combine(serverRoot, "server.properties");
        File.WriteAllText(serverProperties,
            "level-seed=6246468738900744\nonline-mode=true\nwhite-list=true\nenforce-whitelist=true\n" +
            "enable-rcon=false\ngamemode=survival\nserver-ip=\nserver-port=25565\n");
        var bridgeProperties = Path.Combine(serverRoot, "config", "minescape", "bridge.properties");
        Directory.CreateDirectory(Path.GetDirectoryName(bridgeProperties)!);
        const string familyBridgeProperties =
            "instance.mode=family\nbridge.bind=127.0.0.1\nbridge.port=8765\nbridge.max_request_bytes=16384\n";
        const string heartQaBridgeProperties =
            "instance.mode=heart_qa\nbridge.bind=127.0.0.1\nbridge.port=8765\nbridge.max_request_bytes=16384\n";
        File.WriteAllText(bridgeProperties, heartQaBridgeProperties);
        var worldBorderConfiguration = Path.Combine(serverRoot, "config", "worldborder.json5");
        const string canonicalWorldBorderConfiguration =
            "{\n" +
            "  \"enableCustomOverworldBorder\": true,\n" +
            "  \"enableCustomNetherBorder\": true,\n" +
            "  \"enableCustomEndBorder\": true,\n" +
            "  \"shouldLoopToOppositeBorder\": false,\n" +
            "  \"distanceTeleportedBack\": 32,\n" +
            "  \"nearBorderMessage\": \"The edge of this finite world is close.\",\n" +
            "  \"hitBorderMessage\": \"You reached the world edge and were moved safely inward.\",\n" +
            "  \"loopBorderMessage\": \"World-edge looping is disabled.\",\n" +
            "  \"overworldBorderPositiveX\": 16384,\n" +
            "  \"overworldBorderNegativeX\": -16384,\n" +
            "  \"overworldBorderPositiveZ\": 16384,\n" +
            "  \"overworldBorderNegativeZ\": -16384,\n" +
            "  \"netherBorderPositiveX\": 2048,\n" +
            "  \"netherBorderNegativeX\": -2048,\n" +
            "  \"netherBorderPositiveZ\": 2048,\n" +
            "  \"netherBorderNegativeZ\": -2048,\n" +
            "  \"endBorderPositiveX\": 8192,\n" +
            "  \"endBorderNegativeX\": -8192,\n" +
            "  \"endBorderPositiveZ\": 8192,\n" +
            "  \"endBorderNegativeZ\": -8192\n" +
            "}\n";
        File.WriteAllText(worldBorderConfiguration, canonicalWorldBorderConfiguration);

        var modNames = new[]
        {
            "bluemap-test.jar", "cloth-config-test.jar", "collective-test.jar", "efallingtrees-test.jar",
            "fabric-api-test.jar", "ferritecore-test.jar", "ledger-test.jar", "lithium-test.jar",
            "minescape-test.jar", "worldborder-test.jar"
        };
        var bundleFiles = new List<object>();
        foreach (var name in modNames)
        {
            var relative = "mods/" + name;
            var path = Path.Combine(serverRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(relative));
            bundleFiles.Add(new { path = relative, sha512 = Sha512(path) });
        }
        var datapackSpecs = new[]
        {
            (0, 0, "artifact", "matcha", "00-Matcha_Flavoured.zip"),
            (1, 10, "artifact", "terralith", "10-Terralith_test.zip"),
            (2, 20, "artifact", "amplified_nether", "20-Amplified_Nether_test.zip"),
            (3, 30, "artifact", "nullscape", "30-Nullscape_test.zip"),
            (4, 40, "generated", "MineJammer/build/MineScape-Villages.zip", "40-MineScape-Villages.zip"),
            (5, 50, "generated", "MineJammer/build/MineScape-Fishing.zip", "50-MineScape-Fishing.zip"),
            (6, 60, "generated", "MineJammer/build/MineScape-Compatibility.zip", "60-MineScape-Compatibility.zip")
        };
        var datapacks = new List<object>();
        foreach (var (ordinal, priority, kind, source, destination) in datapackSpecs)
        {
            var relative = "world/datapacks/" + destination;
            var path = Path.Combine(serverRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(relative));
            var hash = Sha512(path);
            bundleFiles.Add(new { path = relative, sha512 = hash });
            datapacks.Add(new { ordinal, priority, kind, source, destination, sha512 = hash });
        }
        var bundlePath = Path.Combine(serverRoot, "server-bundle.lock.json");
        void WriteReleaseRecord()
        {
            var currentBundleFiles = new List<object>(bundleFiles)
            {
                new { path = "config/worldborder.json5", sha512 = Sha512(worldBorderConfiguration) }
            };
            var bundle = new
            {
                schema = "minescape.server-bundle-lock.v1", instance = "MineScape", minecraft = "26.2",
                fabric_loader = "0.19.3", fabric_launcher = "1.1.1", fabric_launcher_sha512 = Sha512(launcher),
                seed = "6246468738900744", server_properties_sha512 = Sha512(serverProperties),
                bridge_properties_sha512 = Sha512(bridgeProperties),
                world_border_config_sha512 = Sha512(worldBorderConfiguration),
                datapacks_low_to_high = datapacks, files = currentBundleFiles
            };
            File.WriteAllText(bundlePath, JsonSerializer.Serialize(bundle) + "\n");
            var assemblyFiles = currentBundleFiles.Select(item =>
            {
                using var document = JsonDocument.Parse(JsonSerializer.Serialize(item));
                var path = document.RootElement.GetProperty("path").GetString()!;
                var hash = document.RootElement.GetProperty("sha512").GetString()!;
                return (object)new { path = "var/MineScape/" + path, sha512 = hash, action = "verified-existing" };
            }).ToList();
            assemblyFiles.Add(new { path = "var/MineScape/fabric-server-launch.jar", sha512 = Sha512(launcher), action = "verified-existing" });
            var release = new
            {
                schema = "minescape.runtime-assembly.v1", instance = "MineScape", mode = "release-assembly",
                launch_permitted = true, minecraft = "26.2", fabric_loader = "0.19.3", fabric_launcher = "1.1.1",
                seed = "6246468738900744", online_mode = true, whitelist = true,
                eula_accepted_by_automation = false, launcher = "verified-or-installed",
                server_bundle_sha512 = Sha512(bundlePath),
                datapack_order_contract = new { direction = "lowest-to-highest", entries = datapacks, activation_evidence_required = true },
                generated_datapacks_missing = Array.Empty<string>(), files = assemblyFiles
            };
            File.WriteAllText(Path.Combine(serverRoot, "assembly-status.json"), JsonSerializer.Serialize(release));
        }

        WriteReleaseRecord();
        var wrongMode = RuntimeLaunchValidator.Validate(server, production: true);
        Assert(wrongMode is not null && wrongMode.Contains("family runtime", StringComparison.OrdinalIgnoreCase),
            "A hash-consistent heart-QA Bridge mode must not launch the family runtime.");

        File.WriteAllText(bridgeProperties, familyBridgeProperties);
        File.WriteAllText(worldBorderConfiguration,
            canonicalWorldBorderConfiguration.Replace("\"shouldLoopToOppositeBorder\": false", "\"shouldLoopToOppositeBorder\": true", StringComparison.Ordinal));
        WriteReleaseRecord();
        var loopingBorder = RuntimeLaunchValidator.Validate(server, production: true);
        Assert(loopingBorder is not null && loopingBorder.Contains("finite-world semantics", StringComparison.OrdinalIgnoreCase),
            "A hash-consistent looping World Border configuration must not launch the family runtime.");

        File.WriteAllText(worldBorderConfiguration, canonicalWorldBorderConfiguration);
        WriteReleaseRecord();
        Assert(RuntimeLaunchValidator.Validate(server, production: true) is null,
            "A complete release record bound to the family mode and finite-world configuration should pass static launch validation.");

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            server.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            File.WriteAllText(serverProperties,
                "level-seed=6246468738900744\nonline-mode=true\nwhite-list=true\nenforce-whitelist=true\n" +
                $"enable-rcon=false\ngamemode=survival\nserver-ip=\nserver-port={server.Port}\n");
            WriteReleaseRecord();
            var responder = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();
                _ = await ReadPacket(stream);
                _ = await ReadPacket(stream);
                const string json = "{\"version\":{\"name\":\"Generic responder\",\"protocol\":1},\"players\":{\"max\":8,\"online\":0},\"description\":{\"text\":\"not authenticated\"}}";
                using var response = new MemoryStream();
                response.WriteByte(0);
                var bytes = Encoding.UTF8.GetBytes(json);
                MinecraftPingClient.WriteVarInt(response, bytes.Length);
                response.Write(bytes);
                await WritePacket(stream, response.ToArray());
                var ping = await ReadPacket(stream);
                await WritePacket(stream, ping);
            });
            var options = new MineDeckOptions { DataDirectory = Path.Combine(root, "state"), Minecraft = server };
            var supervisor = new ServerSupervisor(options, new MinecraftPingClient(), new MockBridgeGateway(),
                new AtomicJsonStateStore(options.DataDirectory), new StatusStore());
            var attached = await supervisor.StartProductionAsync(CancellationToken.None);
            Assert(!attached.Accepted && attached.Message.Contains("expected release manifest", StringComparison.OrdinalIgnoreCase),
                "A statically valid release must not trust an already-online responder without authenticated Bridge identity evidence.");
            Assert(!StartupMode.Parse(new[] { "--play" }).ShouldOpenOfficialLauncher(attached.Accepted, productionOnline: true),
                "An unverified already-online production responder must not authorize Play.");
            await responder;
        }
        finally { listener.Stop(); }

        var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        server.Port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();
        File.WriteAllText(serverProperties,
            "level-seed=6246468738900744\nonline-mode=true\nwhite-list=true\nenforce-whitelist=true\n" +
            $"enable-rcon=false\ngamemode=survival\nserver-ip=\nserver-port={server.Port}\n");
        server.ExecutablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Test app host path is unavailable.");
        server.Arguments = $"--fake-minecraft {server.Port}";
        WriteReleaseRecord();
        var launchOptions = new MineDeckOptions { DataDirectory = Path.Combine(root, "launched-state"), Minecraft = server };
        var launchSupervisor = new ServerSupervisor(launchOptions, new MinecraftPingClient(), new MockBridgeGateway(),
            new AtomicJsonStateStore(launchOptions.DataDirectory), new StatusStore());
        var launched = await launchSupervisor.StartProductionAsync(CancellationToken.None);
        Assert(!launched.Accepted && launched.Message.Contains("expected release manifest", StringComparison.OrdinalIgnoreCase),
            "A newly launched production responder must not be accepted before authenticated Bridge identity verification.");
        Assert(!StartupMode.Parse(new[] { "--play" }).ShouldOpenOfficialLauncher(launched.Accepted, productionOnline: true),
            "A newly launched but identity-unverified production server must not authorize Play.");

        var core = Path.Combine(serverRoot, "mods", "minescape-test.jar");
        File.AppendAllText(core, "changed");
        var changed = RuntimeLaunchValidator.Validate(server, production: true);
        Assert(changed is not null && changed.Contains("hash changed", StringComparison.OrdinalIgnoreCase),
            "A changed managed jar must close the production launch gate.");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static async Task TestAtomicStore()
{
    var root = Path.Combine(Path.GetTempPath(), "minedeck-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var store = new AtomicJsonStateStore(root);
        await store.UpdateAsync(s => s with { ProductionProcessId = 1234 });
        await store.UpdateAsync(s => s with { ProductionProcessId = 5678 });
        var state = await store.ReadAsync();
        Assert(state.SchemaVersion == 1 && state.ProductionProcessId == 5678, "Latest atomic state was not recovered.");
        Assert(File.Exists(Path.Combine(root, "state.previous.json")), "Previous complete state was not retained.");
    }
    finally { Directory.Delete(root, recursive: true); }
}

static Task TestSingleInstance()
{
    var root = Path.Combine(Path.GetTempPath(), "minedeck-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        using (var first = new SingleInstanceBroker("test-instance", root))
        using (var second = new SingleInstanceBroker("test-instance", root))
        {
            Assert(first.TryAcquire(), "First supervisor should own the lock.");
            Assert(!second.TryAcquire(), "Second supervisor must not own the lock.");
        }
        using var third = new SingleInstanceBroker("test-instance", root);
        Assert(third.TryAcquire(), "Lock should be recoverable after the owner closes.");
    }
    finally { Directory.Delete(root, recursive: true); }
    return Task.CompletedTask;
}

static async Task TestSupervisorIpc()
{
    var root = Path.Combine(Path.GetTempPath(), "minedeck-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var instance = "ipc-test-" + Guid.NewGuid().ToString("N");
        using var owner = new SingleInstanceBroker(instance, root);
        Assert(owner.TryAcquire(), "IPC test supervisor did not acquire its lock.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var listen = owner.ListenAsync((command, _) => Task.FromResult(new SupervisorReply(command.Action == "status", "attached")), cancellation.Token);
        using var attached = new SingleInstanceBroker(instance, root);
        await Task.Delay(50);
        var reply = await attached.SendAsync(new SupervisorCommand("status"), cancellation.Token);
        Assert(reply.Accepted && reply.Message == "attached", "Attached command did not reach the supervisor.");
        cancellation.Cancel();
        try { await listen; } catch (OperationCanceledException) { }
    }
    finally { Directory.Delete(root, recursive: true); }
}

static async Task TestCurrentBridgeContract()
{
    var root = Path.Combine(Path.GetTempPath(), "minedeck-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var token = "bridge-test-token-0123456789-ABCDEFGHIJ";
    var tokenFile = Path.Combine(root, "bridge.token");
    await File.WriteAllTextAsync(tokenFile, token);
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var captures = new ConcurrentBag<HttpCapture>();
    var server = Task.Run(async () =>
    {
        var handlers = new List<Task>();
        for (var index = 0; index < 7; index++)
        {
            var client = await listener.AcceptTcpClientAsync();
            handlers.Add(HandleBridgeRequest(client, token, captures));
        }
        await Task.WhenAll(handlers);
    });

    try
    {
        var options = new MineDeckOptions();
        options.Bridge.BaseUrl = $"http://127.0.0.1:{port}";
        options.Bridge.TokenFile = tokenFile;
        options.Bridge.TokenEnvironmentVariable = string.Empty;
        options.Bridge.TimeoutSeconds = 3;
        using var bridge = new HttpBridgeGateway(options);
        var health = await bridge.HealthAsync(CancellationToken.None);
        Assert(health.Available && health.Version == "1.0-test" && health.WorldEpoch == "epoch-a", "Current health schema was not translated.");
        Assert(!health.ManifestVerified, "World epoch must not be mislabeled as a manifest hash.");

        var snapshot = await bridge.SnapshotAsync(CancellationToken.None);
        Assert(snapshot.Ok && snapshot.Data is not null, snapshot.Error ?? "Snapshot failed.");
        var snapshotData = snapshot.Data ?? throw new InvalidOperationException("Snapshot data missing.");
        Assert(snapshotData.PendingPlayers.Count == 1 && snapshotData.Players.Count == 1, "Players and whitelist were not composed correctly.");
        Assert(snapshotData.Activity.Count == 1 && snapshotData.UnsupportedCapabilities.Count > 0, "Assist tickets or capability gaps were lost.");

        var player = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert((await bridge.ApprovePlayerAsync(new(player, "Not assigned", null), CancellationToken.None)).Accepted, "UUID whitelist POST was rejected.");
        Assert((await bridge.RevokePlayerAsync(new(player, null), CancellationToken.None)).Accepted, "UUID whitelist DELETE was rejected.");
        Assert((await bridge.RequestFamilyAssistAsync(new(player, "START_AT_DAWN", "family test"), CancellationToken.None)).Accepted, "Family Assist POST was rejected.");
        await server;

        Assert(captures.Count == 7, "Unexpected Bridge request count.");
        Assert(captures.All(x => x.Authorization == $"Bearer {token}"), "A Bridge request did not use the Bearer scheme.");
        Assert(captures.Any(x => x.Method == "POST" && x.Path == "/v1/whitelist" && x.Body.Contains("11111111-1111-1111-1111-111111111111", StringComparison.Ordinal)), "UUID-only whitelist body was not sent.");
        Assert(captures.Any(x => x.Method == "DELETE" && x.Path.EndsWith("11111111-1111-1111-1111-111111111111", StringComparison.Ordinal)), "Whitelist delete route was incorrect.");
    }
    finally
    {
        listener.Stop();
        Directory.Delete(root, recursive: true);
    }
}

static async Task HandleBridgeRequest(TcpClient client, string token, ConcurrentBag<HttpCapture> captures)
{
    using (client)
    await using (var stream = client.GetStream())
    using (var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true))
    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { NewLine = "\r\n", AutoFlush = true })
    {
        var requestLine = await reader.ReadLineAsync() ?? throw new InvalidDataException("Missing HTTP request line.");
        var requestParts = requestLine.Split(' ');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            var split = line.IndexOf(':');
            if (split > 0) headers[line[..split]] = line[(split + 1)..].Trim();
        }
        var contentLength = headers.TryGetValue("Content-Length", out var value) ? int.Parse(value) : 0;
        var body = string.Empty;
        if (contentLength > 0)
        {
            var bodyBuffer = new char[contentLength];
            var offset = 0;
            while (offset < bodyBuffer.Length)
            {
                var read = await reader.ReadAsync(bodyBuffer.AsMemory(offset));
                if (read == 0) break;
                offset += read;
            }
            body = new string(bodyBuffer, 0, offset);
        }
        else if (headers.TryGetValue("Transfer-Encoding", out var transfer) && transfer.Equals("chunked", StringComparison.OrdinalIgnoreCase))
        {
            var chunks = new StringBuilder();
            while (true)
            {
                var sizeLine = await reader.ReadLineAsync() ?? throw new InvalidDataException("Missing HTTP chunk size.");
                var size = Convert.ToInt32(sizeLine.Split(';')[0], 16);
                if (size == 0) { _ = await reader.ReadLineAsync(); break; }
                var chunk = new char[size];
                var offset = 0;
                while (offset < size)
                {
                    var read = await reader.ReadAsync(chunk.AsMemory(offset));
                    if (read == 0) throw new EndOfStreamException();
                    offset += read;
                }
                chunks.Append(chunk);
                _ = await reader.ReadLineAsync();
            }
            body = chunks.ToString();
        }
        var capture = new HttpCapture(requestParts[0], requestParts[1], headers.GetValueOrDefault("Authorization", ""), body);
        captures.Add(capture);

        var json = (capture.Method, capture.Path) switch
        {
            ("GET", "/v1/health") => "{\"status\":\"ONLINE\",\"bridgeVersion\":\"1.0-test\",\"minecraftVersion\":\"26.2\",\"worldEpoch\":\"epoch-a\",\"onlinePlayers\":1,\"observedAt\":\"2026-07-27T00:00:00Z\",\"detail\":\"healthy\"}",
            ("GET", "/v1/players") => "{\"players\":[{\"uuid\":\"11111111-1111-1111-1111-111111111111\",\"currentName\":\"KnownPlayer\",\"online\":true,\"accessRole\":\"FAMILY\",\"sessionMode\":\"FAMILY_PLAY\",\"countsTowardFamilyExploration\":true},{\"uuid\":\"22222222-2222-2222-2222-222222222222\",\"currentName\":\"NewPlayer\",\"online\":false,\"accessRole\":\"UNASSIGNED\",\"sessionMode\":\"FAMILY_PLAY\",\"countsTowardFamilyExploration\":true}]}",
            ("GET", "/v1/whitelist") => "{\"entries\":[{\"uuid\":\"11111111-1111-1111-1111-111111111111\",\"displayName\":\"\",\"status\":\"PENDING_NATIVE_SYNC\",\"changedAt\":\"2026-07-27T00:00:00Z\"}]}",
            ("GET", "/v1/family-assist") => "{\"tickets\":[{\"ticketId\":\"33333333-3333-3333-3333-333333333333\",\"playerId\":\"11111111-1111-1111-1111-111111111111\",\"action\":\"MATCHA_HINT\",\"reason\":\"test\",\"status\":\"PENDING_RELEASE_GATE_ADAPTER\",\"createdAt\":\"2026-07-27T00:00:00Z\"}]}",
            ("POST", "/v1/whitelist") => "{\"uuid\":\"11111111-1111-1111-1111-111111111111\",\"displayName\":\"\",\"status\":\"PENDING_NATIVE_SYNC\",\"changedAt\":\"2026-07-27T00:00:00Z\"}",
            ("DELETE", _) => "{\"removed\":true,\"uuid\":\"11111111-1111-1111-1111-111111111111\"}",
            ("POST", "/v1/family-assist") => "{\"ticketId\":\"44444444-4444-4444-4444-444444444444\",\"playerId\":\"11111111-1111-1111-1111-111111111111\",\"action\":\"START_AT_DAWN\",\"reason\":\"family test\",\"status\":\"PENDING_RELEASE_GATE_ADAPTER\",\"createdAt\":\"2026-07-27T00:00:00Z\"}",
            _ => "{\"error\":{\"code\":\"not_found\",\"message\":\"test route missing\"}}"
        };
        var bytes = Encoding.UTF8.GetBytes(json);
        await writer.WriteAsync("HTTP/1.1 200 OK\r\n");
        await writer.WriteAsync("Content-Type: application/json\r\n");
        await writer.WriteAsync($"Content-Length: {bytes.Length}\r\n");
        await writer.WriteAsync("Connection: close\r\n\r\n");
        await writer.FlushAsync();
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }
}

static async Task<byte[]> ReadPacket(Stream stream)
{
    var length = await ReadVarInt(stream);
    var payload = new byte[length];
    await stream.ReadExactlyAsync(payload);
    return payload;
}

static async Task RunSingleMinecraftResponder(int port)
{
    var listener = new TcpListener(IPAddress.Loopback, port);
    listener.Start();
    try
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        _ = await ReadPacket(stream);
        _ = await ReadPacket(stream);
        const string json = "{\"version\":{\"name\":\"Launched test server\",\"protocol\":1},\"players\":{\"max\":8,\"online\":0},\"description\":{\"text\":\"identity pending\"}}";
        using var response = new MemoryStream();
        response.WriteByte(0);
        var bytes = Encoding.UTF8.GetBytes(json);
        MinecraftPingClient.WriteVarInt(response, bytes.Length);
        response.Write(bytes);
        await WritePacket(stream, response.ToArray());
        var ping = await ReadPacket(stream);
        await WritePacket(stream, ping);
    }
    finally { listener.Stop(); }
}

static async Task<int> ReadVarInt(Stream stream)
{
    var result = 0;
    var buffer = new byte[1];
    for (var position = 0; position < 35; position += 7)
    {
        await stream.ReadExactlyAsync(buffer);
        result |= (buffer[0] & 0x7f) << position;
        if ((buffer[0] & 0x80) == 0) return result;
    }
    throw new InvalidDataException("Oversized VarInt.");
}

static async Task WritePacket(Stream stream, byte[] payload)
{
    using var frame = new MemoryStream();
    MinecraftPingClient.WriteVarInt(frame, payload.Length);
    frame.Write(payload);
    await stream.WriteAsync(frame.ToArray());
    await stream.FlushAsync();
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertThrows<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException(message);
}

static string Sha512(string path)
{
    using var input = File.OpenRead(path);
    return Convert.ToHexString(SHA512.HashData(input)).ToLowerInvariant();
}

sealed record HttpCapture(string Method, string Path, string Authorization, string Body);
