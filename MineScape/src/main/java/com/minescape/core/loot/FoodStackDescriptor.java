package com.minescape.core.loot;

import java.util.Objects;

/** Game-independent description used to prove normalization does not alter item identity or count. */
public record FoodStackDescriptor(String itemId, int count, String componentFingerprint) {
    public FoodStackDescriptor {
        if (Objects.requireNonNull(itemId, "itemId").isBlank()) {
            throw new IllegalArgumentException("itemId cannot be blank");
        }
        if (count < 1) {
            throw new IllegalArgumentException("count must be positive");
        }
        componentFingerprint = componentFingerprint == null ? "" : componentFingerprint;
    }
}
