package com.minescape.core.hearts;

import com.minescape.core.session.SessionMode;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

/** Durable write-ahead receipt for one exact vanilla death ordinal. */
public record DeathConsumptionTransaction(
        UUID eventId,
        int generation,
        int vanillaDeathSequence,
        SessionMode sessionMode,
        boolean survival,
        boolean creative,
        boolean pvp,
        boolean administrative,
        Instant occurredAt) {

    public DeathConsumptionTransaction {
        Objects.requireNonNull(eventId, "eventId");
        Objects.requireNonNull(sessionMode, "sessionMode");
        Objects.requireNonNull(occurredAt, "occurredAt");
        if (generation < 1) {
            throw new IllegalArgumentException("generation must be positive");
        }
        if (vanillaDeathSequence < 1) {
            throw new IllegalArgumentException("vanillaDeathSequence must be positive");
        }
    }

    public static DeathConsumptionTransaction from(DeathEvent event) {
        return new DeathConsumptionTransaction(
                event.eventId(), event.characterGeneration(), event.vanillaDeathSequence(),
                event.sessionMode(), event.survival(), event.creative(), event.pvp(),
                event.administrative(), event.occurredAt());
    }

    public DeathEvent toEvent(UUID playerId) {
        return new DeathEvent(
                eventId, playerId, generation, vanillaDeathSequence, sessionMode,
                survival, creative, pvp, administrative, occurredAt);
    }
}
