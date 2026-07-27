package com.minescape.releasegate;

import com.minescape.core.loot.LootContainerAdapter;
import com.minescape.core.loot.LootEnrichmentRequest;

import java.util.function.Consumer;

@ReleaseGate("Prove complete Terralith 2.6.4 structure-start identity and post-original-roll container composition without table shadowing")
public interface StructureLootAdapter extends LootContainerAdapter {
    void installEligibleStructureSink(Consumer<LootEnrichmentRequest> requestSink);
}
