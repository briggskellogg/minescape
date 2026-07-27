package com.minescape.fabric.network;

import com.minescape.core.hearts.HeartState;
import net.fabricmc.fabric.api.networking.v1.ServerPlayNetworking;
import net.minecraft.server.level.ServerPlayer;

/** Exact Fabric 26.2 packet sender; lifecycle adapters call this after authoritative transitions. */
public final class HeartSyncSender {
    private HeartSyncSender() {
    }

    public static void send(ServerPlayer player, HeartState state) {
        if (ServerPlayNetworking.canSend(player, HeartSyncPayload.TYPE)) {
            ServerPlayNetworking.send(player, new HeartSyncPayload(state));
        }
    }
}
