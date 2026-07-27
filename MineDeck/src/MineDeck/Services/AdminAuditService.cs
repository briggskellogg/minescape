using MineDeck.Models;
using MineDeck.State;

namespace MineDeck.Services;

public sealed class AdminAuditService
{
    private readonly AtomicJsonStateStore _state;
    public AdminAuditService(AtomicJsonStateStore state) => _state = state;

    public Task AppendAsync(string kind, string summary, string outcome, CancellationToken cancellationToken) =>
        _state.UpdateAsync(state =>
        {
            var entry = new ActivityEvent(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, kind, summary, outcome);
            return state with { LocalAudit = state.LocalAudit.Append(entry).TakeLast(2_000).ToArray() };
        }, cancellationToken);
}
