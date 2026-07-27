package com.minescape.core.hearts;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

public record CrystalUseEvent(
        UUID eventId,
        UUID playerId,
        int characterGeneration,
        String itemIdentity,
        String sourceArchiveSha512,
        Instant occurredAt) {

    public CrystalUseEvent {
        Objects.requireNonNull(eventId, "eventId");
        Objects.requireNonNull(playerId, "playerId");
        Objects.requireNonNull(itemIdentity, "itemIdentity");
        Objects.requireNonNull(sourceArchiveSha512, "sourceArchiveSha512");
        Objects.requireNonNull(occurredAt, "occurredAt");
    }
}
