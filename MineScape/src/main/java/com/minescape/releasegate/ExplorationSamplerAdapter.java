package com.minescape.releasegate;

import com.minescape.core.exploration.ChunkCoordinate;
import com.minescape.core.exploration.ExplorationSource;

import java.util.UUID;

@ReleaseGate("Prove boat, horse, minecart, portal and elytra visibility sampling and exclude Steward/Creative/MineJammer")
public interface ExplorationSamplerAdapter {
    void observe(ExplorationSource source, UUID actorId, ChunkCoordinate chunk);
}
