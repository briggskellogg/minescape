package com.minescape.core.loot;

import java.util.Objects;
import java.util.UUID;

public record LootEnrichmentPlan(
        UUID provenanceId,
        int algorithmVersion,
        StructureIdentity structure,
        ContainerIdentity targetContainer,
        BonusLootTable bonusTable) {

    public LootEnrichmentPlan {
        Objects.requireNonNull(provenanceId, "provenanceId");
        Objects.requireNonNull(structure, "structure");
        Objects.requireNonNull(targetContainer, "targetContainer");
        Objects.requireNonNull(bonusTable, "bonusTable");
    }
}
