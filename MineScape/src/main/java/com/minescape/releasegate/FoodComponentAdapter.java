package com.minescape.releasegate;

import com.minescape.core.loot.FoodNormalizationEngine;

@ReleaseGate("Generate exact Matcha 1.02 component profiles and apply only to componentless Terralith-emitted copies without changing item, count, slot, probability or original roll")
public interface FoodComponentAdapter {
    void applyExactComponents(FoodNormalizationEngine.NormalizationPlan plan);
}
