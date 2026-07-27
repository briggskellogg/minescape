package com.minescape.fabric;

import net.minecraft.world.level.GameType;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class DeathModeClassifierTest {
    @Test
    void onlyExactSurvivalConsumesPermanentHearts() {
        assertTrue(DeathModeClassifier.isExactSurvival(GameType.SURVIVAL));
        assertFalse(DeathModeClassifier.isExactSurvival(GameType.ADVENTURE));
        assertFalse(DeathModeClassifier.isExactSurvival(GameType.CREATIVE));
        assertFalse(DeathModeClassifier.isExactSurvival(GameType.SPECTATOR));
    }
}
