package com.minescape.fabric.client;

import com.minescape.fabric.network.HeartSyncPayload;

import java.util.concurrent.atomic.AtomicReference;

final class ClientHeartState {
    private static final AtomicReference<HeartSyncPayload> CURRENT = new AtomicReference<>();

    private ClientHeartState() {
    }

    static HeartSyncPayload get() {
        return CURRENT.get();
    }

    static void accept(HeartSyncPayload state) {
        if (state.capacity() < 10 || state.capacity() > 30
                || state.blocked() < 0 || state.blocked() > state.capacity()
                || state.generation() < 1
                || state.retirementPending() != (state.blocked() == state.capacity())) {
            return;
        }
        CURRENT.set(state);
    }

    static void clear() {
        CURRENT.set(null);
    }
}
