package com.minescape.core.hearts;

import com.minescape.core.session.SessionMode;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

public record DeathEvent(
        UUID eventId,
        UUID playerId,
        int characterGeneration,
        int vanillaDeathSequence,
        SessionMode sessionMode,
        boolean survival,
        boolean creative,
        boolean pvp,
        boolean administrative,
        Instant occurredAt) {

    public DeathEvent {
        Objects.requireNonNull(eventId, "eventId");
        Objects.requireNonNull(playerId, "playerId");
        Objects.requireNonNull(sessionMode, "sessionMode");
        Objects.requireNonNull(occurredAt, "occurredAt");
        if (vanillaDeathSequence < 1) {
            throw new IllegalArgumentException("vanillaDeathSequence must be positive");
        }
    }

    public boolean consumesPermanentHeart() {
        return (sessionMode == SessionMode.FAMILY || sessionMode == SessionMode.HEART_QA)
                && survival && !creative && !pvp && !administrative;
    }
}
