package com.minescape.core.wards;

import java.util.Objects;
import java.util.UUID;

public record CivicWard(
        UUID wardId,
        String settlementId,
        String buildingId,
        String dimension,
        int x,
        int y,
        int z,
        int radius,
        boolean immutable,
        boolean active) {

    public CivicWard {
        Objects.requireNonNull(wardId, "wardId");
        requireText(settlementId, "settlementId");
        requireText(buildingId, "buildingId");
        requireText(dimension, "dimension");
        if (radius < 1 || radius > 128) {
            throw new IllegalArgumentException("ward radius must be between 1 and 128");
        }
    }

    private static void requireText(String value, String field) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(field + " cannot be blank");
        }
    }
}
