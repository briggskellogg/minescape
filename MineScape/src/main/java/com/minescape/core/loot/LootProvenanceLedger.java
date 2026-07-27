package com.minescape.core.loot;

import com.minescape.core.state.DurableProperties;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.Objects;

public final class LootProvenanceLedger {
    private final DurableProperties state;

    public LootProvenanceLedger(DurableProperties state) {
        this.state = Objects.requireNonNull(state, "state");
    }

    public boolean containsStructure(StructureIdentity structure) {
        return state.contains(structureKey(structure));
    }

    public synchronized void record(LootEnrichmentPlan plan, Instant appliedAt, String result) throws IOException {
        String value = String.join("|",
                plan.provenanceId().toString(),
                Integer.toString(plan.algorithmVersion()),
                plan.targetContainer().canonicalKey(),
                plan.bonusTable().tableId(),
                plan.bonusTable().sourceArchiveSha512(),
                appliedAt.toString(),
                result);
        state.put(structureKey(plan.structure()), value);
    }

    private static String structureKey(StructureIdentity structure) {
        UUIDLike key = UUIDLike.from(structure.canonicalKey());
        return "loot.structure." + key.value();
    }

    private record UUIDLike(String value) {
        static UUIDLike from(String value) {
            return new UUIDLike(java.util.UUID.nameUUIDFromBytes(value.getBytes(StandardCharsets.UTF_8)).toString());
        }
    }
}
