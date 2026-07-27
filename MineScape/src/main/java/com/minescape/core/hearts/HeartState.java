package com.minescape.core.hearts;

/** Immutable, server-authoritative state for one character generation. */
public record HeartState(int capacity, int blocked, int generation, boolean retirementPending) {
    public static final int INITIAL_CAPACITY = 10;
    public static final int MAXIMUM_CAPACITY = 30;

    public HeartState {
        if (capacity < INITIAL_CAPACITY || capacity > MAXIMUM_CAPACITY) {
            throw new IllegalArgumentException("capacity must be between 10 and 30");
        }
        if (blocked < 0 || blocked > capacity) {
            throw new IllegalArgumentException("blocked must be between zero and capacity");
        }
        if (generation < 1) {
            throw new IllegalArgumentException("generation must be positive");
        }
        if (retirementPending != (blocked == capacity)) {
            throw new IllegalArgumentException("retirementPending must exactly represent all slots blocked");
        }
    }

    public static HeartState newCharacter() {
        return new HeartState(INITIAL_CAPACITY, 0, 1, false);
    }

    public int usable() {
        return capacity - blocked;
    }

    /** Applies one already-validated permanent death. */
    public HeartState blockOne() {
        if (retirementPending) {
            return this;
        }
        int nextBlocked = blocked + 1;
        return new HeartState(capacity, nextBlocked, generation, nextBlocked == capacity);
    }

    /** Called only after the item adapter proves a genuine frozen-Matcha Crystal Heart was consumed once. */
    public HeartState renewWithGenuineCrystal() {
        if (retirementPending) {
            throw new IllegalStateException("a retired character cannot consume a renewal");
        }
        int nextCapacity = Math.min(MAXIMUM_CAPACITY, capacity + 1);
        return new HeartState(nextCapacity, 0, generation, false);
    }

    /** Starts generation N+1 after an authenticated retirement transaction archives the old character. */
    public HeartState beginNextGeneration() {
        if (!retirementPending) {
            throw new IllegalStateException("only a retirement-pending character can start a new generation");
        }
        return new HeartState(INITIAL_CAPACITY, 0, generation + 1, false);
    }
}
