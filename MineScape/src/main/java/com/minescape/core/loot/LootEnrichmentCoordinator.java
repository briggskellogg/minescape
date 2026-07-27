package com.minescape.core.loot;

import java.io.IOException;
import java.time.Clock;
import java.util.Objects;

/** Coordinates external container marking with the durable provenance index. */
public final class LootEnrichmentCoordinator {
    private final LootEnrichmentEngine engine;
    private final LootProvenanceLedger ledger;
    private final LootContainerAdapter containers;
    private final Clock clock;

    public LootEnrichmentCoordinator(
            LootEnrichmentEngine engine,
            LootProvenanceLedger ledger,
            LootContainerAdapter containers,
            Clock clock) {
        this.engine = Objects.requireNonNull(engine, "engine");
        this.ledger = Objects.requireNonNull(ledger, "ledger");
        this.containers = Objects.requireNonNull(containers, "containers");
        this.clock = Objects.requireNonNull(clock, "clock");
    }

    public synchronized Result enrich(LootEnrichmentRequest request) throws IOException {
        if (ledger.containsStructure(request.structure())) {
            return Result.ALREADY_APPLIED;
        }
        var optionalPlan = engine.plan(request);
        if (optionalPlan.isEmpty()) {
            return Result.NOT_ELIGIBLE;
        }
        LootEnrichmentPlan plan = optionalPlan.get();
        String marker = "minescape:loot/" + plan.provenanceId();
        if (containers.hasProvenanceMarker(plan.targetContainer(), marker)) {
            ledger.record(plan, clock.instant(), "recovered-container-marker");
            return Result.ALREADY_APPLIED;
        }
        LootContainerAdapter.ApplyResult applied = containers.addOneReferenceRollIfUnmarked(
                plan.targetContainer(), plan.bonusTable().tableId(), marker);
        if (applied == LootContainerAdapter.ApplyResult.APPLIED
                || applied == LootContainerAdapter.ApplyResult.ALREADY_MARKED) {
            ledger.record(plan, clock.instant(), applied.name().toLowerCase());
            return applied == LootContainerAdapter.ApplyResult.APPLIED ? Result.APPLIED : Result.ALREADY_APPLIED;
        }
        return applied == LootContainerAdapter.ApplyResult.CONTAINER_MISSING
                ? Result.CONTAINER_MISSING
                : Result.FAILED;
    }

    public enum Result {
        APPLIED,
        ALREADY_APPLIED,
        NOT_ELIGIBLE,
        CONTAINER_MISSING,
        FAILED
    }
}
