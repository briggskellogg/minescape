package com.minescape.releasegate;

import com.minescape.core.hearts.CrystalUseEvent;
import com.minescape.core.hearts.DeathEvent;
import com.minescape.core.hearts.HeartState;

import java.util.UUID;
import java.util.function.Consumer;

@ReleaseGate("Prove Matcha 1.02 death semantics, genuine Crystal consumption, attributes, respawn grace, PvP exclusion, and retirement lockout")
public interface HeartLifecycleAdapter {
    void installDeathSink(Consumer<DeathEvent> deathSink);

    void installCrystalUseSink(Consumer<CrystalUseEvent> crystalSink);

    void applyUsableMaximumHealth(UUID playerId, HeartState state);

    void enterRetirementPending(UUID playerId, HeartState state);
}
