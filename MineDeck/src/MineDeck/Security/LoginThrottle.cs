using System.Collections.Concurrent;

namespace MineDeck.Security;

public sealed class LoginThrottle
{
    private sealed record Attempt(int Count, DateTimeOffset WindowStart, DateTimeOffset? LockedUntil);
    private readonly ConcurrentDictionary<string, Attempt> _attempts = new(StringComparer.Ordinal);

    public bool CanAttempt(string key, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (!_attempts.TryGetValue(key, out var attempt) || attempt.LockedUntil is null) return true;
        if (attempt.LockedUntil <= DateTimeOffset.UtcNow)
        {
            _attempts.TryRemove(key, out _);
            return true;
        }
        retryAfter = attempt.LockedUntil.Value - DateTimeOffset.UtcNow;
        return false;
    }

    public void RecordFailure(string key)
    {
        _attempts.AddOrUpdate(key,
            _ => new Attempt(1, DateTimeOffset.UtcNow, null),
            (_, existing) =>
            {
                var now = DateTimeOffset.UtcNow;
                if (now - existing.WindowStart > TimeSpan.FromMinutes(10)) return new Attempt(1, now, null);
                var count = existing.Count + 1;
                return new Attempt(count, existing.WindowStart, count >= 5 ? now.AddMinutes(5) : null);
            });
    }

    public void RecordSuccess(string key) => _attempts.TryRemove(key, out _);
}
