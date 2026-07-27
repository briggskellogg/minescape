package com.minescape.fabric;

import net.minecraft.world.level.GameType;

/** Keeps Adventure out of the permanent-heart Survival contract. */
final class DeathModeClassifier {
    private DeathModeClassifier() {
    }

    static boolean isExactSurvival(GameType gameType) {
        return gameType == GameType.SURVIVAL;
    }
}
