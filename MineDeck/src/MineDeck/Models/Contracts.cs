using System.Text.Json;

namespace MineDeck.Models;

public enum LifecycleState { Offline, Starting, Online, Stopping, Maintenance, Fault }
public enum OperationKind { None, Starting, Stopping, Backup, Restore, Promotion, Maintenance }

public sealed record PingResult(
    bool Online,
    string? Version,
    int OnlinePlayers,
    int MaxPlayers,
    long LatencyMilliseconds,
    string? Motd,
    string? Error,
    DateTimeOffset CheckedAt);

public sealed record RuntimeStatus(
    LifecycleState State,
    OperationKind Operation,
    string Message,
    PingResult Production,
    PingResult MineJammer,
    bool BridgeAvailable,
    string? BridgeMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastBackupAt,
    long FreeDiskBytes,
    string? ManifestState);

public sealed record PendingPlayer(
    Guid Uuid,
    string CurrentName,
    DateTimeOffset? RequestedAt,
    string? Source,
    DateTimeOffset? ExpiresAt);

public sealed record EnrolledPlayer(
    Guid Uuid,
    string CurrentName,
    string Role,
    string AccessState,
    bool Online,
    string SessionMode,
    bool CountsTowardFamilyExploration);

public sealed record CharacterSummary(
    Guid PlayerUuid,
    string PlayerName,
    int Generation,
    int Capacity,
    int Blocked,
    int Usable,
    string State,
    DateTimeOffset? LastDeathAt,
    DateTimeOffset? LastCrystalRenewalAt);

public sealed record FogSummary(
    string Dimension,
    long CurrentlyVisibleChunks,
    long PreviouslyExploredChunks,
    long NeverExploredChunks,
    DateTimeOffset UpdatedAt,
    string? MaskedMapUrl);

public sealed record SettlementSummary(
    string Id,
    string Name,
    string Dimension,
    int CenterX,
    int CenterZ,
    string State,
    int BuildingCount,
    int CivicWardCount,
    string Source);

public sealed record ProgressRoot(string Id, string Title, int Completed, int Total);

public sealed record PlayerProgress(
    Guid PlayerUuid,
    string PlayerName,
    IReadOnlyList<ProgressRoot> Roots,
    int Completed,
    int Total);

public sealed record LootProvenance(
    string EventId,
    DateTimeOffset At,
    string Dimension,
    int X,
    int Y,
    int Z,
    string Structure,
    string OriginalTable,
    string? MatchaBonusTable,
    string ContainerId,
    bool ResolvedOnce);

public sealed record StewardshipEvent(
    string EventId,
    DateTimeOffset At,
    Guid ActorUuid,
    string Kind,
    string DirectCause,
    string Confidence,
    string? Note,
    string? ReversesEventId);

public sealed record RewardEvent(
    string EventId,
    DateTimeOffset At,
    Guid PlayerUuid,
    string Kind,
    int Quantity,
    string State,
    string Source);

public sealed record ActivityEvent(string EventId, DateTimeOffset At, string Kind, string Summary, string Outcome);

public sealed record BridgeSnapshot(
    IReadOnlyList<PendingPlayer> PendingPlayers,
    IReadOnlyList<EnrolledPlayer> Players,
    IReadOnlyList<CharacterSummary> Characters,
    IReadOnlyList<FogSummary> Fog,
    IReadOnlyList<SettlementSummary> Settlements,
    IReadOnlyList<PlayerProgress> Progress,
    IReadOnlyList<LootProvenance> Loot,
    IReadOnlyList<StewardshipEvent> Stewardship,
    IReadOnlyList<RewardEvent> Rewards,
    IReadOnlyList<ActivityEvent> Activity,
    string NaturalEconomy,
    bool RawBlueMapBlocked,
    IReadOnlyList<string> UnsupportedCapabilities);

public sealed record BridgeHealth(bool Available, string Message, string? Version, string? WorldEpoch, bool ManifestVerified);
public sealed record BridgeResponse(bool Accepted, string Message, string? OperationId = null);
public sealed record PlayerDecision(Guid Uuid, string Role, string? Note);
public sealed record PlayerDenial(Guid Uuid, string? Note);
public sealed record CharacterDecision(Guid PlayerUuid, int Generation, string Reason, string? Evidence);
public sealed record NewCharacterRequest(Guid PlayerUuid, int RetiredGeneration, string Reason);
public sealed record SettlementRegistration(string Name, string Dimension, int MinX, int MinZ, int MaxX, int MaxZ);
public sealed record RewardCorrection(Guid PlayerUuid, int Generation, string Item, int Quantity, string Reason, string Evidence);
public sealed record StewardLeaseRequest(Guid PlayerUuid, int Minutes, string Reason, bool Creative);
public sealed record PromotionRequest(string ReservationId, string CandidateId, string Reason, string ExpectedProductionHash);
public sealed record BackupPreparationRequest(string Reason);
public sealed record BackupPreparation(string Token, DateTimeOffset ExpiresAt, IReadOnlyList<string> RequiredPaths, string WorldEpoch);
public sealed record BackupCompleteRequest(string Token, string ArchiveHash, long Bytes);
public sealed record FamilyAssistRequest(Guid PlayerId, string Action, string Reason);

public sealed record BackupRecord(
    string Id,
    DateTimeOffset CreatedAt,
    string FileName,
    string Sha256,
    long Bytes,
    string WorldEpoch,
    string Reason,
    bool Verified);

public sealed record MineDeckState(
    int SchemaVersion,
    int? ProductionProcessId,
    int? MineJammerProcessId,
    DateTimeOffset? ProductionStartedAt,
    IReadOnlyList<BackupRecord> Backups,
    IReadOnlyList<ActivityEvent> LocalAudit)
{
    public static MineDeckState Empty => new(1, null, null, null, Array.Empty<BackupRecord>(), Array.Empty<ActivityEvent>());
}

public sealed record ApiEnvelope<T>(bool Ok, T? Data, string? Error, bool AdapterAvailable = true)
{
    public static ApiEnvelope<T> Success(T data, bool adapterAvailable = true) => new(true, data, null, adapterAvailable);
    public static ApiEnvelope<T> Failure(string error, bool adapterAvailable = true) => new(false, default, error, adapterAvailable);
}

public sealed record LoginRequest(string Password);
public sealed record LoginResponse(string CsrfToken, DateTimeOffset ExpiresAt);
public sealed record BackupRequest(string Reason);
public sealed record LaunchRequest(bool OpenDashboard = false);
public sealed record OperationResponse(bool Accepted, string Message);
public sealed record SensitiveRequest<T>(string Password, T Request);

public static class EmptyBridgeSnapshot
{
    public static BridgeSnapshot Create() => new(
        Array.Empty<PendingPlayer>(), Array.Empty<EnrolledPlayer>(), Array.Empty<CharacterSummary>(),
        Array.Empty<FogSummary>(), Array.Empty<SettlementSummary>(), Array.Empty<PlayerProgress>(),
        Array.Empty<LootProvenance>(), Array.Empty<StewardshipEvent>(), Array.Empty<RewardEvent>(),
        Array.Empty<ActivityEvent>(), "Natural Matcha economy: 1x (locked)", true,
        new[] { "pending-player denial", "characters/hearts", "fog/map", "settlements/wards", "progress", "loot provenance", "rewards", "backups", "Steward leases", "promotion", "clean stop" });
}
