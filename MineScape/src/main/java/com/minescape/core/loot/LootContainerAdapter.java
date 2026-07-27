package com.minescape.core.loot;

/**
 * Release-gate adapter contract. The implementation must store the provenance marker in the same
 * block-entity transaction as the one extra table-reference roll.
 */
public interface LootContainerAdapter {
    boolean hasProvenanceMarker(ContainerIdentity container, String provenanceMarker);

    ApplyResult addOneReferenceRollIfUnmarked(
            ContainerIdentity container,
            String bonusLootTableId,
            String provenanceMarker);

    enum ApplyResult {
        APPLIED,
        ALREADY_MARKED,
        CONTAINER_MISSING,
        FAILED
    }
}
