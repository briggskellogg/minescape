package com.minescape.core.steward;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

/** Narrow authority record. Client/profile metadata is never authority. */
public record StewardLease(
        UUID leaseId,
        UUID playerId,
        State state,
        Instant issuedAt,
        Instant expiresAt,
        String reason) {

    public enum State {
        ARMED_SURVIVAL,
        CREATIVE,
        ENDED
    }

    public StewardLease {
        Objects.requireNonNull(leaseId, "leaseId");
        Objects.requireNonNull(playerId, "playerId");
        Objects.requireNonNull(state, "state");
        Objects.requireNonNull(issuedAt, "issuedAt");
        Objects.requireNonNull(expiresAt, "expiresAt");
        if (!expiresAt.isAfter(issuedAt)) {
            throw new IllegalArgumentException("lease expiry must follow issue time");
        }
        if (reason == null || reason.isBlank()) {
            throw new IllegalArgumentException("an audited reason is required");
        }
    }

    public boolean activeAt(Instant now) {
        return state != State.ENDED && now.isBefore(expiresAt);
    }

    public StewardLease withState(State next) {
        return new StewardLease(leaseId, playerId, next, issuedAt, expiresAt, reason);
    }
}
