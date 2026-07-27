package com.minescape.core.loot;

import java.util.Map;
import java.util.Objects;
import java.util.Optional;
import java.util.function.Function;
import java.util.stream.Collectors;

/** Plans exact component repair for otherwise componentless copies of frozen Matcha foods. */
public final class FoodNormalizationEngine {
    private final Map<String, FrozenMatchaFoodProfile> profiles;

    public FoodNormalizationEngine(Iterable<FrozenMatchaFoodProfile> profiles) {
        this.profiles = java.util.stream.StreamSupport.stream(profiles.spliterator(), false)
                .collect(Collectors.toUnmodifiableMap(FrozenMatchaFoodProfile::itemId, Function.identity()));
    }

    public Optional<NormalizationPlan> plan(FoodStackDescriptor original) {
        Objects.requireNonNull(original, "original");
        FrozenMatchaFoodProfile profile = profiles.get(original.itemId());
        if (profile == null || !original.componentFingerprint().isBlank()) {
            return Optional.empty();
        }
        FoodStackDescriptor normalized = new FoodStackDescriptor(
                original.itemId(), original.count(), profile.exactComponentFingerprint());
        return Optional.of(new NormalizationPlan(original, normalized, profile.sourceArchiveSha512()));
    }

    public record NormalizationPlan(
            FoodStackDescriptor original,
            FoodStackDescriptor normalized,
            String sourceArchiveSha512) {
        public NormalizationPlan {
            if (!original.itemId().equals(normalized.itemId()) || original.count() != normalized.count()) {
                throw new IllegalArgumentException("normalization may change components only");
            }
        }
    }
}
