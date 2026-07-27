package com.minescape.bridge.api;

import java.util.Objects;
import java.util.UUID;

public record FamilyAssistRequest(UUID playerId, Action action, String reason) {
    public enum Action {
        SET_EASY,
        SET_NORMAL,
        START_AT_DAWN,
        RESCUE_TO_PUBLIC_SPAWN,
        MATCHA_HINT
    }

    public FamilyAssistRequest {
        Objects.requireNonNull(playerId, "playerId");
        Objects.requireNonNull(action, "action");
        if (reason == null || reason.isBlank() || reason.length() > 500) {
            throw new IllegalArgumentException("reason must contain 1-500 characters");
        }
    }
}
