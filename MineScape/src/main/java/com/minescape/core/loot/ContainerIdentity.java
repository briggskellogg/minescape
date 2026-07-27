package com.minescape.core.loot;

import java.util.Objects;

public record ContainerIdentity(String dimension, int x, int y, int z, String originalLootTable) {
    public ContainerIdentity {
        if (Objects.requireNonNull(dimension, "dimension").isBlank()) {
            throw new IllegalArgumentException("dimension cannot be blank");
        }
        if (Objects.requireNonNull(originalLootTable, "originalLootTable").isBlank()) {
            throw new IllegalArgumentException("originalLootTable cannot be blank");
        }
    }

    public String canonicalKey() {
        return dimension + "|" + x + "|" + y + "|" + z + "|" + originalLootTable;
    }
}
