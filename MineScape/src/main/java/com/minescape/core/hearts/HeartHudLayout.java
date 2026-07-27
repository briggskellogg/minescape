package com.minescape.core.hearts;

import java.util.ArrayList;
import java.util.List;

/** Pure coordinate model matching Minecraft 26.2's vanilla heart-row arithmetic. */
public final class HeartHudLayout {
    private HeartHudLayout() {
    }

    public static Layout calculate(
            int guiWidth,
            int guiHeight,
            int capacity,
            int blocked,
            double vanillaLayoutHealthPoints,
            double absorptionHealthPoints) {
        if (capacity < HeartState.INITIAL_CAPACITY || capacity > HeartState.MAXIMUM_CAPACITY) {
            throw new IllegalArgumentException("capacity must be between 10 and 30");
        }
        if (blocked < 0 || blocked > capacity) {
            throw new IllegalArgumentException("blocked must be between zero and capacity");
        }
        int usable = capacity - blocked;
        if (usable < 1) {
            throw new IllegalArgumentException("retired characters do not have a playable HUD layout");
        }
        if (!Double.isFinite(vanillaLayoutHealthPoints)
                || vanillaLayoutHealthPoints < usable * 2.0D) {
            throw new IllegalArgumentException("vanilla layout health cannot be below usable health");
        }
        if (!Double.isFinite(absorptionHealthPoints) || absorptionHealthPoints < 0.0D) {
            throw new IllegalArgumentException("absorption health cannot be negative");
        }

        // Hud.extractPlayerHealth in 26.2 first ceils absorption, then computes
        // ceil((layout health + absorption) / 2 / 10).
        int absorptionPoints = (int) Math.ceil(absorptionHealthPoints);
        int vanillaRows = Math.max(1,
                (int) Math.ceil((vanillaLayoutHealthPoints + absorptionPoints) / 20.0D));
        int rowSpacing = Math.max(10 - (vanillaRows - 2), 3);
        int vanillaHealthSlots = (int) Math.ceil(vanillaLayoutHealthPoints / 2.0D);
        int absorptionSlots = (int) Math.ceil(absorptionPoints / 2.0D);
        int firstBlockedIndex = vanillaHealthSlots + absorptionSlots;
        int originX = guiWidth / 2 - 91;
        int originY = guiHeight - 39;

        List<Slot> slots = new ArrayList<>(blocked);
        for (int offset = 0; offset < blocked; offset++) {
            int index = firstBlockedIndex + offset;
            slots.add(new Slot(index, originX + (index % 10) * 8,
                    originY - (index / 10) * rowSpacing));
        }
        return new Layout(vanillaRows, rowSpacing, firstBlockedIndex, List.copyOf(slots));
    }

    public record Layout(int vanillaRows, int rowSpacing, int firstBlockedIndex, List<Slot> blockedSlots) {
    }

    public record Slot(int index, int x, int y) {
    }
}
