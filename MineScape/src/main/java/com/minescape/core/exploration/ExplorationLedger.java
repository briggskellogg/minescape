package com.minescape.core.exploration;

import com.minescape.core.state.DurableProperties;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.Base64;
import java.util.Objects;
import java.util.Set;
import java.util.UUID;

/**
 * Family exploration is the only input to fog of war. Steward and MineJammer observations are
 * retained under separate provenance keys and can never reveal a family tile.
 */
public final class ExplorationLedger {
    private final DurableProperties state;

    public ExplorationLedger(DurableProperties state) {
        this.state = Objects.requireNonNull(state, "state");
    }

    public synchronized void observe(
            ExplorationSource source,
            UUID actorId,
            ChunkCoordinate chunk,
            Instant observedAt) throws IOException {
        Objects.requireNonNull(source, "source");
        Objects.requireNonNull(chunk, "chunk");
        Objects.requireNonNull(observedAt, "observedAt");
        String coordinate = encode(chunk);
        String actor = actorId == null ? "system" : actorId.toString();
        state.update(editor -> {
            editor.put("exploration.audit." + source.name().toLowerCase() + "." + actor + "." + coordinate,
                    observedAt.toString());
            if (source == ExplorationSource.FAMILY) {
                if (actorId == null) {
                    throw new IllegalArgumentException("family observations require an actor UUID");
                }
                editor.put("exploration.family." + actorId + "." + coordinate, observedAt.toString());
                editor.put("exploration.family_union." + coordinate, observedAt.toString());
            }
            return null;
        });
    }

    public FogState fogState(ChunkCoordinate chunk, Set<ChunkCoordinate> currentlyVisibleToFamily) {
        Objects.requireNonNull(chunk, "chunk");
        Objects.requireNonNull(currentlyVisibleToFamily, "currentlyVisibleToFamily");
        if (currentlyVisibleToFamily.contains(chunk)) {
            return FogState.CURRENTLY_VISIBLE;
        }
        return state.contains("exploration.family_union." + encode(chunk))
                ? FogState.PREVIOUSLY_EXPLORED
                : FogState.NEVER_EXPLORED;
    }

    public boolean wasObservedBy(ExplorationSource source, ChunkCoordinate chunk) {
        String prefix = "exploration.audit." + source.name().toLowerCase() + ".";
        String suffix = "." + encode(chunk);
        return state.withPrefix(prefix).keySet().stream().anyMatch(key -> key.endsWith(suffix));
    }

    private static String encode(ChunkCoordinate chunk) {
        String dimension = Base64.getUrlEncoder().withoutPadding()
                .encodeToString(chunk.dimension().getBytes(StandardCharsets.UTF_8));
        return dimension + "." + chunk.x() + "." + chunk.z();
    }
}
