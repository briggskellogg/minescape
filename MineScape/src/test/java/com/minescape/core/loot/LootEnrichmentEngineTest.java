package com.minescape.core.loot;

import com.minescape.core.state.DurableProperties;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

import java.nio.file.Path;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class LootEnrichmentEngineTest {
    @TempDir
    Path temporaryDirectory;

    @Test
    void selectionIsDeterministicRegardlessOfDiscoveryOrder() {
        LootEnrichmentEngine engine = new LootEnrichmentEngine(1);
        StructureIdentity structure = structure();
        List<ContainerIdentity> containers = containers();
        List<BonusLootTable> tables = tables();

        LootEnrichmentPlan first = engine.plan(new LootEnrichmentRequest(structure, containers, tables)).orElseThrow();
        LootEnrichmentPlan reordered = engine.plan(new LootEnrichmentRequest(
                structure,
                List.of(containers.get(1), containers.get(0)),
                List.of(tables.get(1), tables.get(0)))).orElseThrow();

        assertEquals(first, reordered);
    }

    @Test
    void structureIsEnrichedAtMostOnceAndProvenanceSurvivesRestart() throws Exception {
        DurableProperties durable = DurableProperties.open(temporaryDirectory.resolve("loot.properties"));
        FakeContainers fake = new FakeContainers();
        LootEnrichmentCoordinator coordinator = new LootEnrichmentCoordinator(
                new LootEnrichmentEngine(1), new LootProvenanceLedger(durable), fake,
                Clock.fixed(Instant.parse("2026-07-27T00:00:00Z"), ZoneOffset.UTC));
        LootEnrichmentRequest request = new LootEnrichmentRequest(structure(), containers(), tables());

        assertEquals(LootEnrichmentCoordinator.Result.APPLIED, coordinator.enrich(request));
        assertEquals(LootEnrichmentCoordinator.Result.ALREADY_APPLIED, coordinator.enrich(request));
        assertEquals(1, fake.appliedTables.size());

        LootEnrichmentCoordinator afterRestart = new LootEnrichmentCoordinator(
                new LootEnrichmentEngine(1),
                new LootProvenanceLedger(DurableProperties.open(temporaryDirectory.resolve("loot.properties"))),
                fake, Clock.systemUTC());
        assertEquals(LootEnrichmentCoordinator.Result.ALREADY_APPLIED, afterRestart.enrich(request));
        assertEquals(1, fake.appliedTables.size());
    }

    @Test
    void containerMarkerRecoversCrashBetweenWorldSaveAndLedgerSave() throws Exception {
        LootEnrichmentRequest request = new LootEnrichmentRequest(structure(), containers(), tables());
        LootEnrichmentPlan plan = new LootEnrichmentEngine(1).plan(request).orElseThrow();
        FakeContainers fake = new FakeContainers();
        fake.markers.add("minescape:loot/" + plan.provenanceId());
        DurableProperties durable = DurableProperties.open(temporaryDirectory.resolve("crash.properties"));
        LootEnrichmentCoordinator coordinator = new LootEnrichmentCoordinator(
                new LootEnrichmentEngine(1), new LootProvenanceLedger(durable), fake, Clock.systemUTC());

        assertEquals(LootEnrichmentCoordinator.Result.ALREADY_APPLIED, coordinator.enrich(request));
        assertTrue(new LootProvenanceLedger(durable).containsStructure(request.structure()));
        assertEquals(0, fake.appliedTables.size());
    }

    private static StructureIdentity structure() {
        return new StructureIdentity("v1", 6246468738900744L, "minecraft:overworld",
                "terralith:fortified_village", 128, -77);
    }

    private static List<ContainerIdentity> containers() {
        return List.of(
                new ContainerIdentity("minecraft:overworld", 2050, 72, -1218, "terralith:fort/library"),
                new ContainerIdentity("minecraft:overworld", 2062, 70, -1225, "terralith:fort/kitchen"));
    }

    private static List<BonusLootTable> tables() {
        return List.of(
                new BonusLootTable("minecraft:chests/village/village_plains_house", "matcha-1.02-sha512", 3, false, false),
                new BonusLootTable("minecraft:chests/village/village_weaponsmith", "matcha-1.02-sha512", 1, false, false));
    }

    private static final class FakeContainers implements LootContainerAdapter {
        private final Set<String> markers = new HashSet<>();
        private final List<String> appliedTables = new ArrayList<>();

        @Override
        public boolean hasProvenanceMarker(ContainerIdentity container, String provenanceMarker) {
            return markers.contains(provenanceMarker);
        }

        @Override
        public ApplyResult addOneReferenceRollIfUnmarked(
                ContainerIdentity container, String bonusLootTableId, String provenanceMarker) {
            if (!markers.add(provenanceMarker)) {
                return ApplyResult.ALREADY_MARKED;
            }
            appliedTables.add(bonusLootTableId);
            return ApplyResult.APPLIED;
        }
    }
}
