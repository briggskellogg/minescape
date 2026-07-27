package com.minescape.bridge.api;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

public record WhitelistEntry(
        UUID playerId,
        String displayName,
        Status status,
        Instant changedAt) {

    public enum Status {
        ACTIVE,
        PENDING_NATIVE_SYNC,
        SUSPENDED
    }

    public WhitelistEntry {
        Objects.requireNonNull(playerId, "playerId");
        displayName = displayName == null ? "" : displayName;
        Objects.requireNonNull(status, "status");
        Objects.requireNonNull(changedAt, "changedAt");
    }
}
