package com.minescape.core.loot;

import java.util.List;
import java.util.Objects;

public record LootEnrichmentRequest(
        StructureIdentity structure,
        List<ContainerIdentity> eligibleContainers,
        List<BonusLootTable> allowedBonusTables) {

    public LootEnrichmentRequest {
        Objects.requireNonNull(structure, "structure");
        eligibleContainers = List.copyOf(Objects.requireNonNull(eligibleContainers, "eligibleContainers"));
        allowedBonusTables = List.copyOf(Objects.requireNonNull(allowedBonusTables, "allowedBonusTables"));
    }
}
