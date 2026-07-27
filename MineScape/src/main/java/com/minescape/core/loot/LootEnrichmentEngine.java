package com.minescape.core.loot;

import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Comparator;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

/** Pure deterministic planner. It never evaluates or replaces the original Terralith roll. */
public final class LootEnrichmentEngine {
    private final int algorithmVersion;

    public LootEnrichmentEngine(int algorithmVersion) {
        if (algorithmVersion < 1) {
            throw new IllegalArgumentException("algorithmVersion must be positive");
        }
        this.algorithmVersion = algorithmVersion;
    }

    public Optional<LootEnrichmentPlan> plan(LootEnrichmentRequest request) {
        if (request.eligibleContainers().isEmpty() || request.allowedBonusTables().isEmpty()) {
            return Optional.empty();
        }
        List<ContainerIdentity> containers = request.eligibleContainers().stream()
                .distinct()
                .sorted(Comparator.comparing(ContainerIdentity::canonicalKey))
                .toList();
        List<BonusLootTable> tables = request.allowedBonusTables().stream()
                .sorted(Comparator.comparing(BonusLootTable::tableId))
                .toList();
        String seedMaterial = "minescape-loot-v" + algorithmVersion + "|" + request.structure().canonicalKey();
        byte[] digest = sha256(seedMaterial);
        int containerIndex = positiveInt(digest, 0) % containers.size();
        int totalWeight = tables.stream().mapToInt(BonusLootTable::selectionWeight).sum();
        int weightedSelection = positiveInt(digest, 4) % totalWeight;
        BonusLootTable selectedTable = selectWeighted(tables, weightedSelection);
        UUID provenance = UUID.nameUUIDFromBytes((seedMaterial + "|" + containers.get(containerIndex).canonicalKey()
                + "|" + selectedTable.tableId()).getBytes(StandardCharsets.UTF_8));
        return Optional.of(new LootEnrichmentPlan(
                provenance, algorithmVersion, request.structure(), containers.get(containerIndex), selectedTable));
    }

    private static BonusLootTable selectWeighted(List<BonusLootTable> tables, int selection) {
        int remaining = selection;
        for (BonusLootTable table : tables) {
            if (remaining < table.selectionWeight()) {
                return table;
            }
            remaining -= table.selectionWeight();
        }
        throw new IllegalStateException("weighted selection exceeded table weights");
    }

    private static int positiveInt(byte[] bytes, int offset) {
        return ByteBuffer.wrap(bytes, offset, Integer.BYTES).getInt() & Integer.MAX_VALUE;
    }

    private static byte[] sha256(String value) {
        try {
            return MessageDigest.getInstance("SHA-256").digest(value.getBytes(StandardCharsets.UTF_8));
        } catch (NoSuchAlgorithmException impossible) {
            throw new IllegalStateException("Java runtime lacks SHA-256", impossible);
        }
    }
}
