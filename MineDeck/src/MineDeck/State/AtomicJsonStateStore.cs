using System.Text.Json;
using MineDeck.Models;

namespace MineDeck.State;

public sealed class AtomicJsonStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
    private readonly string _backupPath;

    public AtomicJsonStateStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "state.json");
        _backupPath = Path.Combine(dataDirectory, "state.previous.json");
    }

    public async Task<MineDeckState> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task<MineDeckState> UpdateAsync(Func<MineDeckState, MineDeckState> update, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await ReadUnsafeAsync(cancellationToken).ConfigureAwait(false);
            var next = update(current);
            await WriteUnsafeAsync(next, cancellationToken).ConfigureAwait(false);
            return next;
        }
        finally { _gate.Release(); }
    }

    private async Task<MineDeckState> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        foreach (var candidate in new[] { _path, _backupPath })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                await using var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read);
                return await JsonSerializer.DeserializeAsync<MineDeckState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? MineDeckState.Empty;
            }
            catch (JsonException) when (candidate == _path)
            {
                // The previous complete file is the recovery source after an interrupted write.
            }
        }
        return MineDeckState.Empty;
    }

    private async Task WriteUnsafeAsync(MineDeckState state, CancellationToken cancellationToken)
    {
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (File.Exists(_path)) File.Replace(temp, _path, _backupPath, ignoreMetadataErrors: true);
            else File.Move(temp, _path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
