package com.minescape.bridge.api;

import com.minescape.core.session.SessionMode;

import java.util.Objects;
import java.util.UUID;

public record PlayerSummary(
        UUID playerId,
        String currentName,
        boolean online,
        String accessRole,
        SessionMode sessionMode,
        boolean countsTowardFamilyExploration) {

    public PlayerSummary {
        Objects.requireNonNull(playerId, "playerId");
        Objects.requireNonNull(currentName, "currentName");
        Objects.requireNonNull(accessRole, "accessRole");
        Objects.requireNonNull(sessionMode, "sessionMode");
    }
}
