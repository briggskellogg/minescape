package com.minescape.core.exploration;

import java.util.Objects;

public record ChunkCoordinate(String dimension, int x, int z) {
    public ChunkCoordinate {
        if (Objects.requireNonNull(dimension, "dimension").isBlank()) {
            throw new IllegalArgumentException("dimension cannot be blank");
        }
    }
}
