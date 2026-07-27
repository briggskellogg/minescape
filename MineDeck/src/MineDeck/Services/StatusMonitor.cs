using MineDeck.Bridge;
using MineDeck.Configuration;
using MineDeck.Models;
using MineDeck.Protocol;
using MineDeck.State;

namespace MineDeck.Services;

public sealed class StatusMonitor : BackgroundService
{
    private readonly MineDeckOptions _options;
    private readonly MinecraftPingClient _ping;
    private readonly IBridgeGateway _bridge;
    private readonly StatusStore _status;
    private readonly AtomicJsonStateStore _state;

    public StatusMonitor(MineDeckOptions options, MinecraftPingClient ping, IBridgeGateway bridge, StatusStore status, AtomicJsonStateStore state)
    {
        _options = options;
        _ping = ping;
        _bridge = bridge;
        _status = status;
        _state = state;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var productionTask = _ping.PingAsync(_options.Minecraft.Host, _options.Minecraft.Port, _options.Minecraft.ProtocolVersion, TimeSpan.FromSeconds(3), stoppingToken);
            var jammerTask = _ping.PingAsync(_options.MineJammer.Host, _options.MineJammer.Port, _options.MineJammer.ProtocolVersion, TimeSpan.FromSeconds(2), stoppingToken);
            var bridgeTask = _bridge.HealthAsync(stoppingToken);
            await Task.WhenAll(productionTask, jammerTask, bridgeTask).ConfigureAwait(false);
            var persisted = await _state.ReadAsync(stoppingToken).ConfigureAwait(false);
            var free = GetFreeBytes(_options.DataDirectory);
            var production = productionTask.Result;
            var bridge = bridgeTask.Result;

            _status.Update(current =>
            {
                var fault = _status.FatalFault;
                var state = StatusStore.Classify(production, current.Operation, fault);
                var message = fault ?? (current.Operation == OperationKind.None
                    ? production.Online ? $"Online · {production.OnlinePlayers}/{production.MaxPlayers} players · {production.LatencyMilliseconds} ms" : "Offline"
                    : current.Message);
                return current with
                {
                    State = state,
                    Message = message,
                    Production = production,
                    MineJammer = jammerTask.Result,
                    BridgeAvailable = bridge.Available,
                    BridgeMessage = bridge.Message,
                    StartedAt = persisted.ProductionStartedAt,
                    LastBackupAt = persisted.Backups.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.CreatedAt,
                    FreeDiskBytes = free,
                    ManifestState = bridge.ManifestVerified
                        ? "Verified by Bridge"
                        : bridge.Available ? "Not exposed by current Bridge" : "Unavailable"
                };
            });

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
        }
    }

    private static long GetFreeBytes(string path)
    {
        try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace; }
        catch (Exception) when (OperatingSystem.IsWindows()) { return 0; }
    }
}
