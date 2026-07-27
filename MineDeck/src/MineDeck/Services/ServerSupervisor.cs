using System.Diagnostics;
using MineDeck.Bridge;
using MineDeck.Configuration;
using MineDeck.Models;
using MineDeck.Protocol;
using MineDeck.State;

namespace MineDeck.Services;

public sealed class ServerSupervisor
{
    private readonly MineDeckOptions _options;
    private readonly MinecraftPingClient _ping;
    private readonly IBridgeGateway _bridge;
    private readonly AtomicJsonStateStore _state;
    private readonly StatusStore _status;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ServerSupervisor(MineDeckOptions options, MinecraftPingClient ping, IBridgeGateway bridge, AtomicJsonStateStore state, StatusStore status)
    {
        _options = options;
        _ping = ping;
        _bridge = bridge;
        _state = state;
        _status = status;
    }

    public Task<OperationResponse> StartProductionAsync(CancellationToken cancellationToken) => StartAsync(_options.Minecraft, production: true, cancellationToken);
    public Task<OperationResponse> StartMineJammerAsync(CancellationToken cancellationToken) => StartAsync(_options.MineJammer, production: false, cancellationToken);

    public async Task<OperationResponse> StopProductionAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ping = await PingAsync(_options.Minecraft, cancellationToken).ConfigureAwait(false);
            if (!ping.Online) return new(true, "MineScape is already offline.");
            _status.Begin(OperationKind.Stopping, "Requesting a clean MineScape shutdown…");
            var response = await _bridge.StopProductionAsync(cancellationToken).ConfigureAwait(false);
            if (!response.Accepted)
            {
                _status.Fault(response.Message);
                return new(false, response.Message);
            }
            if (!await WaitForAsync(_options.Minecraft, online: false, TimeSpan.FromSeconds(90), cancellationToken).ConfigureAwait(false))
            {
                _status.Fault("MineScape accepted shutdown but remained reachable after 90 seconds.");
                return new(false, "Timed out waiting for MineScape to stop. It was not force-killed.");
            }
            await _state.UpdateAsync(s => s with { ProductionProcessId = null, ProductionStartedAt = null }, cancellationToken).ConfigureAwait(false);
            _status.EndOperation("MineScape stopped cleanly.");
            return new(true, "MineScape stopped cleanly.");
        }
        finally { _gate.Release(); }
    }

    public async Task<OperationResponse> StopMineJammerAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var ping = await PingAsync(_options.MineJammer, cancellationToken).ConfigureAwait(false);
            if (!ping.Online) return new(true, "MineJammer is already offline.");
            _status.Begin(OperationKind.Stopping, "Requesting a clean MineJammer shutdown…");
            var response = await _bridge.StopMineJammerAsync(cancellationToken).ConfigureAwait(false);
            if (!response.Accepted)
            {
                _status.Fault(response.Message);
                return new(false, response.Message);
            }
            if (!await WaitForAsync(_options.MineJammer, false, TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false))
            {
                _status.Fault("MineJammer accepted shutdown but remained reachable.");
                return new(false, "Timed out waiting for MineJammer to stop. It was not force-killed.");
            }
            await _state.UpdateAsync(s => s with { MineJammerProcessId = null }, cancellationToken).ConfigureAwait(false);
            _status.EndOperation("MineJammer stopped cleanly.");
            return new(true, "MineJammer stopped cleanly.");
        }
        finally { _gate.Release(); }
    }

    public Task<bool> WaitForProductionOnlineAsync(CancellationToken cancellationToken) =>
        WaitForAsync(_options.Minecraft, true, TimeSpan.FromSeconds(_options.Minecraft.StartupTimeoutSeconds), cancellationToken);

    private async Task<OperationResponse> StartAsync(MinecraftOptions server, bool production, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var label = production ? "MineScape" : "MineJammer";
            // Production integrity is checked before trusting anything that happens to answer
            // on the configured Minecraft port. Otherwise an unrelated/manual server could
            // bypass the release record, launcher, and EULA gates through the idempotent path.
            if (production)
            {
                var productionValidation = RuntimeLaunchValidator.Validate(server, production: true);
                if (productionValidation is not null)
                {
                    _status.Fault(productionValidation);
                    return new(false, productionValidation);
                }
            }

            if ((await PingAsync(server, cancellationToken).ConfigureAwait(false)).Online)
            {
                if (production)
                {
                    var identityError = await ValidateProductionIdentityAsync(cancellationToken).ConfigureAwait(false);
                    if (identityError is not null)
                    {
                        _status.Fault(identityError);
                        return new(false, identityError);
                    }
                }
                return new(true, production ? "MineScape is already online." : "MineJammer is already online.");
            }

            // MineJammer remains convenient to attach to while it is already answering a
            // genuine ping, but a new laboratory process still requires its launcher/EULA.
            if (!production)
            {
                var laboratoryValidation = RuntimeLaunchValidator.Validate(server, production: false);
                if (laboratoryValidation is not null)
                {
                    _status.Fault(laboratoryValidation);
                    return new(false, laboratoryValidation);
                }
            }

            _status.Begin(OperationKind.Starting, $"Starting {label}…");
            var startInfo = new ProcessStartInfo
            {
                FileName = server.ExecutablePath,
                Arguments = server.Arguments,
                WorkingDirectory = server.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };
            var process = Process.Start(startInfo);
            if (process is null)
            {
                _status.Fault($"Windows did not start {label}.");
                return new(false, $"Windows did not start {label}.");
            }

            var now = DateTimeOffset.UtcNow;
            await _state.UpdateAsync(s => production
                ? s with { ProductionProcessId = process.Id, ProductionStartedAt = now }
                : s with { MineJammerProcessId = process.Id }, cancellationToken).ConfigureAwait(false);

            var timeout = TimeSpan.FromSeconds(server.StartupTimeoutSeconds);
            if (!await WaitForAsync(server, online: true, timeout, cancellationToken).ConfigureAwait(false))
            {
                var reason = process.HasExited
                    ? $"{label} exited with code {process.ExitCode} before answering a Minecraft ping."
                    : $"{label} did not answer a Minecraft ping within {timeout.TotalSeconds:0} seconds.";
                _status.Fault(reason);
                return new(false, reason);
            }

            if (production)
            {
                var identityError = await ValidateProductionIdentityAsync(cancellationToken).ConfigureAwait(false);
                if (identityError is not null)
                {
                    _status.Fault(identityError);
                    return new(false, identityError);
                }
            }

            _status.EndOperation($"{label} is online and answering Minecraft pings.");
            return new(true, $"{label} is online.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _status.Fault(exception.Message);
            return new(false, exception.Message);
        }
        finally { _gate.Release(); }
    }

    private async Task<string?> ValidateProductionIdentityAsync(CancellationToken cancellationToken)
    {
        var health = await _bridge.HealthAsync(cancellationToken).ConfigureAwait(false);
        if (!health.Available)
            return $"A Minecraft server answered on the production port, but the authenticated MineScape Bridge did not: {health.Message}";
        if (string.IsNullOrWhiteSpace(health.Version) || string.IsNullOrWhiteSpace(health.WorldEpoch))
            return "The authenticated MineScape Bridge did not provide a version and world epoch; the existing server was not trusted.";
        if (!health.ManifestVerified)
            return "The authenticated MineScape Bridge cannot yet prove the expected release manifest, version, and world epoch; the existing server was not trusted.";
        return null;
    }

    private async Task<bool> WaitForAsync(MinecraftOptions server, bool online, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if ((await PingAsync(server, cancellationToken).ConfigureAwait(false)).Online == online) return true;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    private Task<PingResult> PingAsync(MinecraftOptions server, CancellationToken cancellationToken) =>
        _ping.PingAsync(server.Host, server.Port, server.ProtocolVersion, TimeSpan.FromSeconds(3), cancellationToken);

}
