package com.minescape.core.hearts;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class HeartHudLayoutTest {
    @Test
    void everyPlayableStateUsesVanillaRowsAndCoordinates() {
        int width = 427;
        int height = 240;
        double[] absorptionValues = {0.0D, 0.5D, 1.0D, 2.0D, 4.0D, 9.5D, 20.0D};

        for (int capacity = 10; capacity <= 30; capacity++) {
            for (int blocked = 0; blocked < capacity; blocked++) {
                int usable = capacity - blocked;
                for (double absorption : absorptionValues) {
                    double vanillaHealth = usable * 2.0D;
                    int absorptionPoints = (int) Math.ceil(absorption);
                    int expectedRows = Math.max(1,
                            (int) Math.ceil((vanillaHealth + absorptionPoints) / 20.0D));
                    int expectedSpacing = Math.max(10 - (expectedRows - 2), 3);
                    int vanillaSlots = usable + (int) Math.ceil(absorptionPoints / 2.0D);

                    HeartHudLayout.Layout layout = HeartHudLayout.calculate(
                            width, height, capacity, blocked, vanillaHealth, absorption);

                    assertEquals(expectedRows, layout.vanillaRows());
                    assertEquals(expectedSpacing, layout.rowSpacing());
                    assertEquals(vanillaSlots, layout.firstBlockedIndex());
                    assertEquals(blocked, layout.blockedSlots().size());
                    for (int offset = 0; offset < blocked; offset++) {
                        HeartHudLayout.Slot slot = layout.blockedSlots().get(offset);
                        int index = vanillaSlots + offset;
                        assertEquals(index, slot.index());
                        assertEquals(width / 2 - 91 + (index % 10) * 8, slot.x());
                        assertEquals(height - 39 - (index / 10) * expectedSpacing, slot.y());
                        assertTrue(index >= vanillaSlots, "blocked slots must not cover absorption");
                    }
                }
            }
        }
    }
}
