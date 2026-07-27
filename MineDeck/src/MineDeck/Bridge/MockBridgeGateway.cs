using MineDeck.Models;

namespace MineDeck.Bridge;

// An explicit dashboard-development adapter. It never performs a Minecraft mutation.
public sealed class MockBridgeGateway : IBridgeGateway
{
    private static BridgeResponse ReadOnly() => new(false, "Mock Bridge is read-only; no Minecraft mutation occurred.");
    public Task<BridgeHealth> HealthAsync(CancellationToken ct) => Task.FromResult(new BridgeHealth(true, "Explicit mock adapter", "mock", "mock-only", false));
    public Task<ApiEnvelope<BridgeSnapshot>> SnapshotAsync(CancellationToken ct) => Task.FromResult(ApiEnvelope<BridgeSnapshot>.Success(EmptyBridgeSnapshot.Create()));
    public Task<BridgeResponse> ApprovePlayerAsync(PlayerDecision r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> DenyPlayerAsync(PlayerDenial r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> RevokePlayerAsync(PlayerDenial r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> RequestFamilyAssistAsync(FamilyAssistRequest r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> PardonCharacterAsync(CharacterDecision r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> BeginNewCharacterAsync(NewCharacterRequest r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> RegisterSettlementAsync(SettlementRegistration r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> CorrectRewardAsync(RewardCorrection r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> RequestStewardLeaseAsync(StewardLeaseRequest r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> EndStewardLeaseAsync(CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> StopProductionAsync(CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> StopMineJammerAsync(CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> ComparePromotionAsync(PromotionRequest r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<BridgeResponse> CommitPromotionAsync(PromotionRequest r, CancellationToken ct) => Task.FromResult(ReadOnly());
    public Task<ApiEnvelope<BackupPreparation>> PrepareBackupAsync(BackupPreparationRequest r, CancellationToken ct) => Task.FromResult(ApiEnvelope<BackupPreparation>.Failure("Mock Bridge cannot quiesce a world."));
    public Task<BridgeResponse> CompleteBackupAsync(BackupCompleteRequest r, CancellationToken ct) => Task.FromResult(ReadOnly());
}
