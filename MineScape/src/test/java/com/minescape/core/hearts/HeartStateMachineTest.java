package com.minescape.core.hearts;

import com.minescape.core.session.SessionMode;
import com.minescape.core.state.DurableProperties;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

import java.nio.file.Path;
import java.time.Instant;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class HeartStateMachineTest {
    @TempDir
    Path temporaryDirectory;

    @Test
    void deathsBlockVisibleSlotsAndCrystalFullyRenewsPlusOne() throws Exception {
        HeartRegistry registry = registry();
        UUID player = UUID.randomUUID();

        for (int death = 0; death < 3; death++) {
            registry.recordDeath(validDeath(player, 1, death + 1, UUID.randomUUID()));
        }
        HeartState scarred = registry.get(player);
        assertEquals(10, scarred.capacity());
        assertEquals(3, scarred.blocked());
        assertEquals(7, scarred.usable());

        CrystalUseEvent crystal = new CrystalUseEvent(
                UUID.randomUUID(), player, 1, "matcha:genuine_crystal_heart", "sha512-pinned-matcha-1.02", Instant.now());
        HeartTransition transition = registry.consumeCrystal(crystal, proof -> true);

        assertEquals(HeartTransition.Outcome.CRYSTAL_RENEWED, transition.outcome());
        assertEquals(11, transition.after().capacity());
        assertEquals(0, transition.after().blocked());
        assertEquals(11, transition.after().usable());
    }

    @Test
    void tenthDeathEntersRetirementBeforeAZeroHealthCharacterIsRepresented() throws Exception {
        HeartRegistry registry = registry();
        UUID player = UUID.randomUUID();
        HeartTransition finalTransition = null;
        for (int death = 0; death < 10; death++) {
            finalTransition = registry.recordDeath(validDeath(player, 1, death + 1, UUID.randomUUID()));
        }

        assertEquals(HeartTransition.Outcome.RETIREMENT_PENDING, finalTransition.outcome());
        assertEquals(0, finalTransition.after().usable());
        assertTrue(finalTransition.after().retirementPending());
        assertThrows(IllegalStateException.class, finalTransition.after()::renewWithGenuineCrystal);

        HeartTransition next = registry.beginNextGeneration(player);
        assertEquals(2, next.after().generation());
        assertEquals(10, next.after().usable());
        assertFalse(next.after().retirementPending());
    }

    @Test
    void duplicatePvpAndAdministrativeDeathsCannotConsumeSlots() throws Exception {
        HeartRegistry registry = registry();
        UUID player = UUID.randomUUID();
        UUID eventId = UUID.randomUUID();
        DeathEvent first = validDeath(player, 1, 1, eventId);

        registry.recordDeath(first);
        assertEquals(HeartTransition.Outcome.DUPLICATE_EVENT, registry.recordDeath(first).outcome());

        DeathEvent pvp = new DeathEvent(UUID.randomUUID(), player, 1, 2, SessionMode.FAMILY,
                true, false, true, false, Instant.now());
        DeathEvent steward = new DeathEvent(UUID.randomUUID(), player, 1, 3, SessionMode.STEWARD,
                true, false, false, false, Instant.now());
        registry.recordDeath(pvp);
        registry.recordDeath(steward);

        assertEquals(1, registry.get(player).blocked());
    }

    @Test
    void capacityStopsAtThirtyButRenewalStillClearsScars() {
        HeartState state = new HeartState(30, 8, 4, false).renewWithGenuineCrystal();
        assertEquals(30, state.capacity());
        assertEquals(0, state.blocked());
    }

    @Test
    void everyUnlockedSlotMustBlackOutBeforeAnElevenHeartCharacterRetires() throws Exception {
        HeartRegistry registry = registry();
        UUID player = UUID.randomUUID();
        CrystalUseEvent crystal = new CrystalUseEvent(
                UUID.randomUUID(), player, 1, "matcha:genuine_crystal_heart",
                "sha512-pinned-matcha-1.02", Instant.now());
        registry.consumeCrystal(crystal, proof -> true);

        for (int death = 0; death < 10; death++) {
            HeartTransition transition = registry.recordDeath(
                    validDeath(player, 1, death + 1, UUID.randomUUID()));
            assertFalse(transition.after().retirementPending());
        }
        HeartState oneLeft = registry.get(player);
        assertEquals(11, oneLeft.capacity());
        assertEquals(10, oneLeft.blocked());
        assertEquals(1, oneLeft.usable());

        HeartTransition finalDeath = registry.recordDeath(validDeath(player, 1, 11, UUID.randomUUID()));
        assertEquals(HeartTransition.Outcome.RETIREMENT_PENDING, finalDeath.outcome());
        assertEquals(11, finalDeath.after().blocked());
        assertEquals(0, finalDeath.after().usable());
    }

    @Test
    void crystalInventoryReceiptSurvivesRestartAndClearsOnlyAfterExactCompletion() throws Exception {
        Path statePath = temporaryDirectory.resolve("transaction-state.properties");
        UUID player = UUID.randomUUID();
        UUID event = UUID.randomUUID();
        HeartRegistry first = new HeartRegistry(DurableProperties.open(statePath));
        String baseline = "a".repeat(128);
        CrystalConsumptionTransaction begun = first.beginCrystalConsumption(player, event, 1, 3, baseline);
        assertEquals(3, begun.inventoryCountBefore());
        assertEquals(baseline, begun.playerDataSha512Before());

        HeartRegistry restarted = new HeartRegistry(DurableProperties.open(statePath));
        assertEquals(begun, restarted.pendingCrystalConsumption(player).orElseThrow());
        assertEquals(begun, restarted.beginCrystalConsumption(
                player, UUID.randomUUID(), 1, 99, "b".repeat(128)));
        assertThrows(IllegalArgumentException.class,
                () -> restarted.completeCrystalConsumption(player, UUID.randomUUID()));
        restarted.completeCrystalConsumption(player, event);
        assertTrue(restarted.pendingCrystalConsumption(player).isEmpty());
    }

    @Test
    void disposableHeartQaUsesTheSameScarRuleWithoutCallingItFamilyExploration() throws Exception {
        HeartRegistry registry = registry();
        UUID player = UUID.randomUUID();
        DeathEvent qaDeath = new DeathEvent(UUID.randomUUID(), player, 1, 1, SessionMode.HEART_QA,
                true, false, false, false, Instant.now());
        assertEquals(HeartTransition.Outcome.DEATH_BLOCKED_SLOT, registry.recordDeath(qaDeath).outcome());
    }

    @Test
    void durableDeathReceiptDeduplicatesCallbackRespawnAndRestart() throws Exception {
        Path statePath = temporaryDirectory.resolve("death-transaction.properties");
        UUID player = UUID.randomUUID();
        UUID eventId = UUID.randomUUID();
        HeartRegistry first = new HeartRegistry(DurableProperties.open(statePath));
        first.reconcileDeathSequence(player, 0);
        DeathEvent event = validDeath(player, 1, 1, eventId);

        DeathConsumptionTransaction receipt = first.beginDeathConsumption(player, event);
        assertEquals(1, receipt.vanillaDeathSequence());
        assertEquals(HeartTransition.Outcome.DEATH_BLOCKED_SLOT, first.recordDeath(event).outcome());

        HeartRegistry restarted = new HeartRegistry(DurableProperties.open(statePath));
        assertEquals(receipt, restarted.pendingDeathConsumption(player).orElseThrow());
        assertEquals(HeartTransition.Outcome.DUPLICATE_EVENT,
                restarted.recordDeath(receipt.toEvent(player)).outcome());
        restarted.completeDeathConsumption(player, eventId, 1);
        restarted.reconcileDeathSequence(player, 1);

        assertEquals(1, restarted.get(player).blocked());
        assertEquals(1, restarted.completedDeathSequence(player).orElseThrow());
        assertTrue(restarted.pendingDeathConsumption(player).isEmpty());
        assertThrows(IllegalStateException.class,
                () -> restarted.beginDeathConsumption(player, event));
    }

    @Test
    void unjournaledDeathGapAndStatsRollbackFailClosed() throws Exception {
        HeartRegistry registry = registry();
        UUID player = UUID.randomUUID();
        registry.reconcileDeathSequence(player, 4);

        assertThrows(IllegalStateException.class,
                () -> registry.reconcileDeathSequence(player, 5));
        assertThrows(IllegalStateException.class,
                () -> registry.reconcileDeathSequence(player, 3));
        assertThrows(IllegalStateException.class,
                () -> registry.beginDeathConsumption(
                        player, validDeath(player, 1, 6, UUID.randomUUID())));
    }

    private HeartRegistry registry() throws Exception {
        return new HeartRegistry(DurableProperties.open(temporaryDirectory.resolve("state.properties")));
    }

    private static DeathEvent validDeath(
            UUID player, int generation, int deathSequence, UUID eventId) {
        return new DeathEvent(eventId, player, generation, deathSequence, SessionMode.FAMILY,
                true, false, false, false, Instant.now());
    }
}
