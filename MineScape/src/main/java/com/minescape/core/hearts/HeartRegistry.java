package com.minescape.core.hearts;

import com.minescape.core.state.DurableProperties;

import java.io.IOException;
import java.time.Instant;
import java.util.Objects;
import java.util.Optional;
import java.util.OptionalInt;
import java.util.UUID;

/** Durable state machine with event-id replay protection. */
public final class HeartRegistry {
    private final DurableProperties state;

    public HeartRegistry(DurableProperties state) {
        this.state = Objects.requireNonNull(state, "state");
    }

    public synchronized HeartState get(UUID playerId) {
        String prefix = playerPrefix(playerId);
        String capacity = state.get(prefix + "capacity");
        if (capacity == null) {
            return HeartState.newCharacter();
        }
        int parsedCapacity = Integer.parseInt(capacity);
        int blocked = Integer.parseInt(state.getOrDefault(prefix + "blocked", "0"));
        int generation = Integer.parseInt(state.getOrDefault(prefix + "generation", "1"));
        boolean retirement = Boolean.parseBoolean(state.getOrDefault(prefix + "retirement_pending", "false"));
        return new HeartState(parsedCapacity, blocked, generation, retirement);
    }

    public synchronized HeartTransition recordDeath(DeathEvent event) throws IOException {
        Objects.requireNonNull(event, "event");
        HeartState before = get(event.playerId());
        String eventKey = "hearts.event.death." + event.eventId();
        if (state.contains(eventKey)) {
            return HeartTransition.unchanged(HeartTransition.Outcome.DUPLICATE_EVENT, before);
        }
        if (event.characterGeneration() != before.generation()) {
            state.put(eventKey, "stale-generation");
            return HeartTransition.unchanged(HeartTransition.Outcome.STALE_GENERATION, before);
        }
        if (!event.consumesPermanentHeart()) {
            state.put(eventKey, "ignored-context");
            return HeartTransition.unchanged(HeartTransition.Outcome.IGNORED_CONTEXT, before);
        }
        if (before.retirementPending()) {
            state.put(eventKey, "already-retired");
            return HeartTransition.unchanged(HeartTransition.Outcome.ALREADY_RETIRED, before);
        }

        HeartState after = before.blockOne();
        state.update(editor -> {
            write(editor, event.playerId(), after);
            editor.put(eventKey, event.occurredAt().toString());
            return null;
        });
        var outcome = after.retirementPending()
                ? HeartTransition.Outcome.RETIREMENT_PENDING
                : HeartTransition.Outcome.DEATH_BLOCKED_SLOT;
        return new HeartTransition(outcome, before, after);
    }

    /**
     * Establishes or verifies the monotonic vanilla death-stat baseline at a safe join/respawn
     * boundary. A gap without a write-ahead receipt is ambiguous and therefore release-fatal.
     */
    public synchronized void reconcileDeathSequence(UUID playerId, int observedSequence) throws IOException {
        if (observedSequence < 0) {
            throw new IllegalArgumentException("observed death sequence cannot be negative");
        }
        if (pendingDeathConsumption(playerId).isPresent()) {
            return;
        }
        OptionalInt completed = completedDeathSequence(playerId);
        if (completed.isEmpty()) {
            state.put(deathSequenceKey(playerId), Integer.toString(observedSequence));
            return;
        }
        if (completed.getAsInt() != observedSequence) {
            throw new IllegalStateException(
                    "vanilla death sequence differs from the durable MineScape sequence");
        }
    }

    public synchronized OptionalInt completedDeathSequence(UUID playerId) {
        String encoded = state.get(deathSequenceKey(playerId));
        return encoded == null ? OptionalInt.empty() : OptionalInt.of(Integer.parseInt(encoded));
    }

    public synchronized Optional<DeathConsumptionTransaction> pendingDeathConsumption(UUID playerId) {
        String encoded = state.get(deathPendingKey(playerId));
        if (encoded == null) {
            return Optional.empty();
        }
        String[] fields = encoded.split("\\|", -1);
        if (fields.length != 9) {
            throw new IllegalStateException("corrupt durable death receipt");
        }
        return Optional.of(new DeathConsumptionTransaction(
                UUID.fromString(fields[0]), Integer.parseInt(fields[1]), Integer.parseInt(fields[2]),
                com.minescape.core.session.SessionMode.valueOf(fields[3]),
                Boolean.parseBoolean(fields[4]), Boolean.parseBoolean(fields[5]),
                Boolean.parseBoolean(fields[6]), Boolean.parseBoolean(fields[7]), Instant.parse(fields[8])));
    }

    /** Writes the exact death context before stats or heart state are mutated by MineScape. */
    public synchronized DeathConsumptionTransaction beginDeathConsumption(
            UUID playerId, DeathEvent event) throws IOException {
        Objects.requireNonNull(playerId, "playerId");
        Objects.requireNonNull(event, "event");
        if (!playerId.equals(event.playerId())) {
            throw new IllegalArgumentException("death receipt player mismatch");
        }
        Optional<DeathConsumptionTransaction> pending = pendingDeathConsumption(playerId);
        DeathConsumptionTransaction proposed = DeathConsumptionTransaction.from(event);
        if (pending.isPresent()) {
            if (!pending.get().equals(proposed)) {
                throw new IllegalStateException("a different death transaction is already pending");
            }
            return pending.get();
        }
        int completed = completedDeathSequence(playerId)
                .orElseThrow(() -> new IllegalStateException("death sequence baseline is not established"));
        if (event.vanillaDeathSequence() <= completed) {
            throw new IllegalStateException("death sequence was already completed or rolled back");
        }
        if (event.vanillaDeathSequence() != completed + 1) {
            throw new IllegalStateException("death sequence contains an unjournaled gap");
        }
        HeartState current = get(playerId);
        if (event.characterGeneration() != current.generation()) {
            throw new IllegalStateException("death transaction generation mismatch");
        }
        state.put(deathPendingKey(playerId), String.join("|",
                event.eventId().toString(), Integer.toString(event.characterGeneration()),
                Integer.toString(event.vanillaDeathSequence()), event.sessionMode().name(),
                Boolean.toString(event.survival()), Boolean.toString(event.creative()),
                Boolean.toString(event.pvp()), Boolean.toString(event.administrative()),
                event.occurredAt().toString()));
        return proposed;
    }

    /** Commits the ordinal only after the exact event outcome and vanilla stats are durable. */
    public synchronized void completeDeathConsumption(
            UUID playerId, UUID eventId, int vanillaDeathSequence) throws IOException {
        DeathConsumptionTransaction pending = pendingDeathConsumption(playerId)
                .orElseThrow(() -> new IllegalStateException("no death transaction is pending"));
        if (!pending.eventId().equals(eventId)
                || pending.vanillaDeathSequence() != vanillaDeathSequence) {
            throw new IllegalArgumentException("death transaction identity mismatch");
        }
        if (!state.contains("hearts.event.death." + eventId)) {
            throw new IllegalStateException("death outcome is not durable");
        }
        state.update(editor -> {
            editor.put(deathSequenceKey(playerId), Integer.toString(vanillaDeathSequence));
            editor.remove(deathPendingKey(playerId));
            return null;
        });
    }

    public synchronized HeartTransition consumeCrystal(CrystalUseEvent event, CrystalProofVerifier verifier) throws IOException {
        Objects.requireNonNull(event, "event");
        Objects.requireNonNull(verifier, "verifier");
        HeartState before = get(event.playerId());
        String eventKey = "hearts.event.crystal." + event.eventId();
        if (state.contains(eventKey)) {
            return HeartTransition.unchanged(HeartTransition.Outcome.DUPLICATE_EVENT, before);
        }
        if (event.characterGeneration() != before.generation()) {
            state.put(eventKey, "stale-generation");
            return HeartTransition.unchanged(HeartTransition.Outcome.STALE_GENERATION, before);
        }
        if (!verifier.isGenuine(event)) {
            state.put(eventKey, "invalid-proof");
            return HeartTransition.unchanged(HeartTransition.Outcome.INVALID_CRYSTAL, before);
        }
        if (before.retirementPending()) {
            state.put(eventKey, "already-retired");
            return HeartTransition.unchanged(HeartTransition.Outcome.ALREADY_RETIRED, before);
        }

        HeartState after = before.renewWithGenuineCrystal();
        state.update(editor -> {
            write(editor, event.playerId(), after);
            editor.put(eventKey, event.occurredAt().toString());
            return null;
        });
        return new HeartTransition(HeartTransition.Outcome.CRYSTAL_RENEWED, before, after);
    }

    public synchronized HeartTransition beginNextGeneration(UUID playerId) throws IOException {
        HeartState before = get(playerId);
        HeartState after = before.beginNextGeneration();
        state.update(editor -> {
            write(editor, playerId, after);
            editor.put("hearts.generation.event." + playerId + "." + after.generation(), "begun");
            return null;
        });
        return new HeartTransition(HeartTransition.Outcome.NEW_GENERATION, before, after);
    }

    public synchronized Optional<CrystalConsumptionTransaction> pendingCrystalConsumption(UUID playerId) {
        String encoded = state.get(crystalPendingKey(playerId));
        if (encoded == null) {
            return Optional.empty();
        }
        String[] fields = encoded.split("\\|", -1);
        if (fields.length != 4) {
            throw new IllegalStateException("corrupt durable Crystal consumption receipt");
        }
        return Optional.of(new CrystalConsumptionTransaction(
                UUID.fromString(fields[0]), Integer.parseInt(fields[1]), Integer.parseInt(fields[2]), fields[3]));
    }

    /**
     * Persists the inventory-side expectation before any heart or item mutation. An existing
     * receipt always wins so restart recovery cannot create a second event.
     */
    public synchronized CrystalConsumptionTransaction beginCrystalConsumption(
            UUID playerId, UUID eventId, int generation, int inventoryCountBefore,
            String playerDataSha512Before) throws IOException {
        Optional<CrystalConsumptionTransaction> existing = pendingCrystalConsumption(playerId);
        if (existing.isPresent()) {
            return existing.get();
        }
        HeartState current = get(playerId);
        if (generation != current.generation() || current.retirementPending()) {
            throw new IllegalStateException("Crystal transaction does not match the active character");
        }
        CrystalConsumptionTransaction transaction =
                new CrystalConsumptionTransaction(
                        eventId, generation, inventoryCountBefore, playerDataSha512Before);
        state.put(crystalPendingKey(playerId), String.join("|",
                eventId.toString(), Integer.toString(generation), Integer.toString(inventoryCountBefore),
                playerDataSha512Before));
        return transaction;
    }

    /** Clears only the exact durable receipt after player inventory has been synchronously saved. */
    public synchronized void completeCrystalConsumption(UUID playerId, UUID eventId) throws IOException {
        CrystalConsumptionTransaction current = pendingCrystalConsumption(playerId)
                .orElseThrow(() -> new IllegalStateException("no Crystal transaction is pending"));
        if (!current.eventId().equals(eventId)) {
            throw new IllegalArgumentException("Crystal transaction id mismatch");
        }
        state.remove(crystalPendingKey(playerId));
    }

    private static void write(DurableProperties.Editor editor, UUID playerId, HeartState value) {
        String prefix = playerPrefix(playerId);
        editor.put(prefix + "capacity", Integer.toString(value.capacity()));
        editor.put(prefix + "blocked", Integer.toString(value.blocked()));
        editor.put(prefix + "generation", Integer.toString(value.generation()));
        editor.put(prefix + "retirement_pending", Boolean.toString(value.retirementPending()));
    }

    private static String playerPrefix(UUID playerId) {
        return "hearts.player." + playerId + ".";
    }

    private static String crystalPendingKey(UUID playerId) {
        return playerPrefix(playerId) + "crystal_pending";
    }

    private static String deathPendingKey(UUID playerId) {
        return playerPrefix(playerId) + "death_pending";
    }

    private static String deathSequenceKey(UUID playerId) {
        return playerPrefix(playerId) + "death_sequence";
    }
}
