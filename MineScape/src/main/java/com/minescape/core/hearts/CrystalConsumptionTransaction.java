package com.minescape.core.hearts;

import java.util.UUID;
import java.util.regex.Pattern;

/** Durable cross-store receipt used to reconcile a rare Crystal renewal with player inventory. */
public record CrystalConsumptionTransaction(
        UUID eventId, int generation, int inventoryCountBefore, String playerDataSha512Before) {
    private static final Pattern SHA512 = Pattern.compile("[0-9a-f]{128}");

    public CrystalConsumptionTransaction {
        if (eventId == null) {
            throw new IllegalArgumentException("eventId is required");
        }
        if (generation < 1) {
            throw new IllegalArgumentException("generation must be positive");
        }
        if (inventoryCountBefore < 1) {
            throw new IllegalArgumentException("inventoryCountBefore must be positive");
        }
        if (playerDataSha512Before == null || !SHA512.matcher(playerDataSha512Before).matches()) {
            throw new IllegalArgumentException("playerDataSha512Before must be a lowercase SHA-512");
        }
    }
}
