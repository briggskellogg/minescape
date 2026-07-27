package com.minescape.core.loot;

import java.util.Objects;

/** The external archive audit, not a guessed namespace, establishes these safety flags. */
public record BonusLootTable(
        String tableId,
        String sourceArchiveSha512,
        int selectionWeight,
        boolean containsDivineFavour,
        boolean containsDivineFragment) {

    public BonusLootTable {
        if (Objects.requireNonNull(tableId, "tableId").isBlank()) {
            throw new IllegalArgumentException("tableId cannot be blank");
        }
        if (Objects.requireNonNull(sourceArchiveSha512, "sourceArchiveSha512").isBlank()) {
            throw new IllegalArgumentException("source archive hash cannot be blank");
        }
        if (selectionWeight < 1) {
            throw new IllegalArgumentException("selection weight must be positive");
        }
        if (containsDivineFavour || containsDivineFragment) {
            throw new IllegalArgumentException("MineScape enrichment cannot inject Divine Favour or Divine Fragments");
        }
    }
}
