package com.minescape.core.loot;

import java.util.Objects;

/** Generated only by the exact Matcha 1.02 archive audit. */
public record FrozenMatchaFoodProfile(
        String itemId,
        String exactComponentFingerprint,
        String sourceArchiveSha512) {
    public FrozenMatchaFoodProfile {
        if (Objects.requireNonNull(itemId, "itemId").isBlank()
                || Objects.requireNonNull(exactComponentFingerprint, "exactComponentFingerprint").isBlank()
                || Objects.requireNonNull(sourceArchiveSha512, "sourceArchiveSha512").isBlank()) {
            throw new IllegalArgumentException("frozen Matcha food profile fields cannot be blank");
        }
    }
}
