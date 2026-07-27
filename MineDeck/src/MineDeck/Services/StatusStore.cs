using MineDeck.Models;

namespace MineDeck.Services;

public sealed class StatusStore
{
    private readonly object _sync = new();
    private string? _fatalFault;
    private RuntimeStatus _value = new(
        LifecycleState.Offline, OperationKind.None, "Waiting for first ping.",
        Offline("Not checked yet."), Offline("Not checked yet."), false, "Not checked yet.",
        null, null, 0, "Unknown");

    public event EventHandler<RuntimeStatus>? Changed;
    public RuntimeStatus Current { get { lock (_sync) return _value; } }
    public string? FatalFault { get { lock (_sync) return _fatalFault; } }

    public void Update(Func<RuntimeStatus, RuntimeStatus> change)
    {
        RuntimeStatus next;
        lock (_sync) { _value = next = change(_value); }
        Changed?.Invoke(this, next);
    }

    public void Begin(OperationKind operation, string message)
    {
        lock (_sync) _fatalFault = null;
        Update(x => x with
        {
            Operation = operation,
            State = operation switch
            {
                OperationKind.Starting => LifecycleState.Starting,
                OperationKind.Stopping => LifecycleState.Stopping,
                _ => LifecycleState.Maintenance
            },
            Message = message
        });
    }

    public void EndOperation(string message)
    {
        lock (_sync) _fatalFault = null;
        Update(x => x with { Operation = OperationKind.None, Message = message });
    }

    public void Fault(string message)
    {
        lock (_sync) _fatalFault = message;
        Update(x => x with { Operation = OperationKind.None, State = LifecycleState.Fault, Message = message });
    }

    public static LifecycleState Classify(PingResult ping, OperationKind operation, string? fatalFault)
    {
        if (!string.IsNullOrWhiteSpace(fatalFault)) return LifecycleState.Fault;
        if (operation == OperationKind.Starting) return LifecycleState.Starting;
        if (operation == OperationKind.Stopping) return LifecycleState.Stopping;
        if (operation != OperationKind.None) return LifecycleState.Maintenance;
        return ping.Online ? LifecycleState.Online : LifecycleState.Offline;
    }

    private static PingResult Offline(string reason) => new(false, null, 0, 0, 0, null, reason, DateTimeOffset.MinValue);
}
