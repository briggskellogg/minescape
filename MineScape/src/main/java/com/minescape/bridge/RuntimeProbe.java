package com.minescape.bridge;

import com.minescape.bridge.api.PlayerSummary;
import com.minescape.bridge.api.ServerHealth;

import java.util.List;

/** Implemented by the thin Fabric adapter; safe fallbacks make adapter absence visible as degraded. */
public interface RuntimeProbe {
    ServerHealth health();

    List<PlayerSummary> players();
}
