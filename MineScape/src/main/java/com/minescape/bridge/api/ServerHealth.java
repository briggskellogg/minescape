package com.minescape.bridge.api;

import java.time.Instant;
import java.util.Objects;

public record ServerHealth(
        Status status,
        String bridgeVersion,
        String minecraftVersion,
        String worldEpoch,
        int onlinePlayers,
        Instant observedAt,
        String detail) {

    public enum Status {
        STARTING,
        ONLINE,
        DEGRADED,
        STOPPING
    }

    public ServerHealth {
        Objects.requireNonNull(status, "status");
        Objects.requireNonNull(bridgeVersion, "bridgeVersion");
        Objects.requireNonNull(minecraftVersion, "minecraftVersion");
        Objects.requireNonNull(worldEpoch, "worldEpoch");
        Objects.requireNonNull(observedAt, "observedAt");
        Objects.requireNonNull(detail, "detail");
    }
}
