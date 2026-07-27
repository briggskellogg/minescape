namespace MineDeck.Services;

public sealed class SupervisorCommandHandler
{
    private readonly ServerSupervisor _servers;
    private readonly StatusStore _status;

    public SupervisorCommandHandler(ServerSupervisor servers, StatusStore status)
    {
        _servers = servers;
        _status = status;
    }

    public async Task<SupervisorReply> HandleAsync(SupervisorCommand command, CancellationToken cancellationToken)
    {
        var result = command.Action.ToLowerInvariant() switch
        {
            "start" => await _servers.StartProductionAsync(cancellationToken).ConfigureAwait(false),
            "stop" => await _servers.StopProductionAsync(cancellationToken).ConfigureAwait(false),
            "start-minejammer" => await _servers.StartMineJammerAsync(cancellationToken).ConfigureAwait(false),
            "stop-minejammer" => await _servers.StopMineJammerAsync(cancellationToken).ConfigureAwait(false),
            "status" => new Models.OperationResponse(true, _status.Current.Message),
            _ => new Models.OperationResponse(false, "Unknown supervisor command.")
        };
        return new(result.Accepted, result.Message);
    }
}
