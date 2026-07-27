using MineDeck.Models;

namespace MineDeck.Bridge;

public interface IBridgeGateway
{
    Task<BridgeHealth> HealthAsync(CancellationToken cancellationToken);
    Task<ApiEnvelope<BridgeSnapshot>> SnapshotAsync(CancellationToken cancellationToken);
    Task<BridgeResponse> ApprovePlayerAsync(PlayerDecision request, CancellationToken cancellationToken);
    Task<BridgeResponse> DenyPlayerAsync(PlayerDenial request, CancellationToken cancellationToken);
    Task<BridgeResponse> RevokePlayerAsync(PlayerDenial request, CancellationToken cancellationToken);
    Task<BridgeResponse> RequestFamilyAssistAsync(FamilyAssistRequest request, CancellationToken cancellationToken);
    Task<BridgeResponse> PardonCharacterAsync(CharacterDecision request, CancellationToken cancellationToken);
    Task<BridgeResponse> BeginNewCharacterAsync(NewCharacterRequest request, CancellationToken cancellationToken);
    Task<BridgeResponse> RegisterSettlementAsync(SettlementRegistration request, CancellationToken cancellationToken);
    Task<BridgeResponse> CorrectRewardAsync(RewardCorrection request, CancellationToken cancellationToken);
    Task<BridgeResponse> RequestStewardLeaseAsync(StewardLeaseRequest request, CancellationToken cancellationToken);
    Task<BridgeResponse> EndStewardLeaseAsync(CancellationToken cancellationToken);
    Task<BridgeResponse> StopProductionAsync(CancellationToken cancellationToken);
    Task<BridgeResponse> StopMineJammerAsync(CancellationToken cancellationToken);
    Task<BridgeResponse> ComparePromotionAsync(PromotionRequest request, CancellationToken cancellationToken);
    Task<BridgeResponse> CommitPromotionAsync(PromotionRequest request, CancellationToken cancellationToken);
    Task<ApiEnvelope<BackupPreparation>> PrepareBackupAsync(BackupPreparationRequest request, CancellationToken cancellationToken);
    Task<BridgeResponse> CompleteBackupAsync(BackupCompleteRequest request, CancellationToken cancellationToken);
}
