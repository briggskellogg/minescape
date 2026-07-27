package com.minescape.fabric.network;

import com.minescape.core.hearts.HeartState;
import net.minecraft.network.RegistryFriendlyByteBuf;
import net.minecraft.network.codec.ByteBufCodecs;
import net.minecraft.network.codec.StreamCodec;
import net.minecraft.network.protocol.common.custom.CustomPacketPayload;
import net.minecraft.resources.Identifier;

public record HeartSyncPayload(int capacity, int blocked, int generation, boolean retirementPending)
        implements CustomPacketPayload {

    public static final Identifier ID = Identifier.fromNamespaceAndPath("minescape", "heart_state");
    public static final Type<HeartSyncPayload> TYPE = new Type<>(ID);
    public static final StreamCodec<RegistryFriendlyByteBuf, HeartSyncPayload> CODEC = StreamCodec.composite(
            ByteBufCodecs.VAR_INT, HeartSyncPayload::capacity,
            ByteBufCodecs.VAR_INT, HeartSyncPayload::blocked,
            ByteBufCodecs.VAR_INT, HeartSyncPayload::generation,
            ByteBufCodecs.BOOL, HeartSyncPayload::retirementPending,
            HeartSyncPayload::new);

    public HeartSyncPayload(HeartState state) {
        this(state.capacity(), state.blocked(), state.generation(), state.retirementPending());
    }

    @Override
    public Type<? extends CustomPacketPayload> type() {
        return TYPE;
    }
}
