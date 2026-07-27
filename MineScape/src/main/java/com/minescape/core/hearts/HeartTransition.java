package com.minescape.core.hearts;

public record HeartTransition(Outcome outcome, HeartState before, HeartState after) {
    public enum Outcome {
        DEATH_BLOCKED_SLOT,
        RETIREMENT_PENDING,
        CRYSTAL_RENEWED,
        NEW_GENERATION,
        IGNORED_CONTEXT,
        DUPLICATE_EVENT,
        STALE_GENERATION,
        INVALID_CRYSTAL,
        ALREADY_RETIRED
    }

    public static HeartTransition unchanged(Outcome outcome, HeartState state) {
        return new HeartTransition(outcome, state, state);
    }
}
