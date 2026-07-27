package com.minescape.core.loot;

import java.util.Objects;

/** Stable identity for one generated structure start in one world epoch. */
public record StructureIdentity(
        String worldEpoch,
        long worldSeed,
        String dimension,
        String structureType,
        int startChunkX,
        int startChunkZ) {

    public StructureIdentity {
        require(worldEpoch, "worldEpoch");
        require(dimension, "dimension");
        require(structureType, "structureType");
    }

    public String canonicalKey() {
        return String.join("|", worldEpoch, Long.toString(worldSeed), dimension, structureType,
                Integer.toString(startChunkX), Integer.toString(startChunkZ));
    }

    private static void require(String value, String name) {
        if (Objects.requireNonNull(value, name).isBlank()) {
            throw new IllegalArgumentException(name + " cannot be blank");
        }
    }
}
