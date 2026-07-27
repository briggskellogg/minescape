package com.minescape.core.exploration;

import com.minescape.core.state.DurableProperties;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

import java.nio.file.Path;
import java.time.Instant;
import java.util.Set;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class ExplorationLedgerTest {
    @TempDir
    Path temporaryDirectory;

    @Test
    void stewardAndMineJammerNeverRevealFamilyFog() throws Exception {
        ExplorationLedger ledger = new ExplorationLedger(
                DurableProperties.open(temporaryDirectory.resolve("exploration.properties")));
        ChunkCoordinate chunk = new ChunkCoordinate("minecraft:overworld", 700, -400);

        ledger.observe(ExplorationSource.MINEJAMMER, null, chunk, Instant.now());
        ledger.observe(ExplorationSource.STEWARD, UUID.randomUUID(), chunk, Instant.now());

        assertEquals(FogState.NEVER_EXPLORED, ledger.fogState(chunk, Set.of()));
        assertTrue(ledger.wasObservedBy(ExplorationSource.MINEJAMMER, chunk));
        assertTrue(ledger.wasObservedBy(ExplorationSource.STEWARD, chunk));
    }

    @Test
    void familyChunksMoveFromCurrentToRememberedWithoutLosingHistory() throws Exception {
        ExplorationLedger ledger = new ExplorationLedger(
                DurableProperties.open(temporaryDirectory.resolve("family.properties")));
        ChunkCoordinate chunk = new ChunkCoordinate("minecraft:the_nether", -12, 9);
        UUID child = UUID.randomUUID();

        ledger.observe(ExplorationSource.FAMILY, child, chunk, Instant.now());
        assertEquals(FogState.CURRENTLY_VISIBLE, ledger.fogState(chunk, Set.of(chunk)));
        assertEquals(FogState.PREVIOUSLY_EXPLORED, ledger.fogState(chunk, Set.of()));

        ExplorationLedger afterRestart = new ExplorationLedger(
                DurableProperties.open(temporaryDirectory.resolve("family.properties")));
        assertEquals(FogState.PREVIOUSLY_EXPLORED, afterRestart.fogState(chunk, Set.of()));
    }
}
