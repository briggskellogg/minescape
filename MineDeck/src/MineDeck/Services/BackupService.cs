using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MineDeck.Bridge;
using MineDeck.Configuration;
using MineDeck.Models;
using MineDeck.State;

namespace MineDeck.Services;

public sealed class BackupService
{
    private readonly MineDeckOptions _options;
    private readonly IBridgeGateway _bridge;
    private readonly AtomicJsonStateStore _state;
    private readonly StatusStore _status;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BackupService(MineDeckOptions options, IBridgeGateway bridge, AtomicJsonStateStore state, StatusStore status)
    {
        _options = options;
        _bridge = bridge;
        _state = state;
        _status = status;
    }

    public async Task<ApiEnvelope<BackupRecord>> CreateAsync(string reason, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(reason)) return ApiEnvelope<BackupRecord>.Failure("A backup reason is required.");
            _status.Begin(OperationKind.Backup, "Quiescing MineScape for a consistent backup…");
            var preparation = await _bridge.PrepareBackupAsync(new(reason), cancellationToken).ConfigureAwait(false);
            if (!preparation.Ok || preparation.Data is null)
            {
                _status.Fault(preparation.Error ?? "Bridge did not provide a backup token.");
                return ApiEnvelope<BackupRecord>.Failure(preparation.Error ?? "Bridge did not provide a backup token.", preparation.AdapterAvailable);
            }
            if (preparation.Data.ExpiresAt <= DateTimeOffset.UtcNow)
                return ApiEnvelope<BackupRecord>.Failure("Bridge backup token was already expired.");

            var configured = _options.Backups.SourceDirectories.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var required = preparation.Data.RequiredPaths.Select(Path.GetFullPath).ToArray();
            if (required.Any(path => !configured.Contains(path)))
                return ApiEnvelope<BackupRecord>.Failure("Bridge requested a path outside the configured backup allowlist.");
            if (required.Any(path => !Directory.Exists(path)))
                return ApiEnvelope<BackupRecord>.Failure("One or more Bridge-required backup paths do not exist.");

            Directory.CreateDirectory(_options.Backups.DestinationDirectory);
            var id = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..32];
            var temp = Path.Combine(_options.Backups.DestinationDirectory, $".{id}.partial");
            var final = Path.Combine(_options.Backups.DestinationDirectory, $"minescape-{id}.zip");
            try
            {
                await Task.Run(() => CreateArchive(temp, required, preparation.Data, reason), cancellationToken).ConfigureAwait(false);
                var hash = await HashAsync(temp, cancellationToken).ConfigureAwait(false);
                File.Move(temp, final);
                var info = new FileInfo(final);
                var record = new BackupRecord(id, DateTimeOffset.UtcNow, info.Name, hash, info.Length, preparation.Data.WorldEpoch, reason, true);
                var completion = await _bridge.CompleteBackupAsync(new(preparation.Data.Token, hash, info.Length), cancellationToken).ConfigureAwait(false);
                if (!completion.Accepted)
                    return ApiEnvelope<BackupRecord>.Failure($"Archive exists and is hashed, but Bridge could not close its token: {completion.Message}");
                await _state.UpdateAsync(s => s with { Backups = s.Backups.Append(record).ToArray() }, cancellationToken).ConfigureAwait(false);
                _status.EndOperation("Backup created and verified.");
                return ApiEnvelope<BackupRecord>.Success(record);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            _status.Fault(exception.Message);
            return ApiEnvelope<BackupRecord>.Failure(exception.Message);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken) =>
        (await _state.ReadAsync(cancellationToken).ConfigureAwait(false)).Backups.OrderByDescending(x => x.CreatedAt).ToArray();

    public async Task<ApiEnvelope<BackupRecord>> VerifyAsync(string id, CancellationToken cancellationToken)
    {
        var state = await _state.ReadAsync(cancellationToken).ConfigureAwait(false);
        var record = state.Backups.SingleOrDefault(x => x.Id == id);
        if (record is null) return ApiEnvelope<BackupRecord>.Failure("Unknown backup id.");
        var path = Path.Combine(_options.Backups.DestinationDirectory, record.FileName);
        if (!File.Exists(path)) return ApiEnvelope<BackupRecord>.Failure("Backup archive is missing.");
        var actual = await HashAsync(path, cancellationToken).ConfigureAwait(false);
        return string.Equals(actual, record.Sha256, StringComparison.OrdinalIgnoreCase)
            ? ApiEnvelope<BackupRecord>.Success(record)
            : ApiEnvelope<BackupRecord>.Failure("Backup hash mismatch.");
    }

    private static void CreateArchive(string destination, IReadOnlyList<string> sources, BackupPreparation preparation, string reason)
    {
        using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);
        var manifest = archive.CreateEntry("minedeck-backup.json", CompressionLevel.SmallestSize);
        using (var writer = new StreamWriter(manifest.Open()))
            writer.Write(JsonSerializer.Serialize(new { preparation.WorldEpoch, preparation.Token, reason, createdAt = DateTimeOffset.UtcNow }));

        for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            var root = sources[sourceIndex];
            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                var entryName = $"source-{sourceIndex}/{relative}";
                archive.CreateEntryFromFile(path, entryName, CompressionLevel.Fastest);
            }
        }
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
