package com.minescape.fabric.client;

import com.minescape.fabric.network.HeartSyncPayload;
import com.minescape.core.hearts.HeartHudLayout;
import net.fabricmc.api.ClientModInitializer;
import net.fabricmc.fabric.api.client.networking.v1.ClientPlayConnectionEvents;
import net.fabricmc.fabric.api.client.networking.v1.ClientPlayNetworking;
import net.fabricmc.fabric.api.client.rendering.v1.hud.HudElementRegistry;
import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.RenderPipelines;
import net.minecraft.resources.Identifier;

public final class MineScapeClientMod implements ClientModInitializer {
    private static final Identifier HEART_CONTAINER = Identifier.withDefaultNamespace("hud/heart/container");

    @Override
    public void onInitializeClient() {
        ClientPlayNetworking.registerGlobalReceiver(HeartSyncPayload.TYPE,
                (payload, context) -> ClientHeartState.accept(payload));
        ClientPlayConnectionEvents.DISCONNECT.register((handler, client) -> ClientHeartState.clear());

        HudElementRegistry.addLast(Identifier.fromNamespaceAndPath("minescape", "mortal_hearts"),
                (graphics, tickCounter) -> {
                    HeartSyncPayload state = ClientHeartState.get();
                    var player = Minecraft.getInstance().player;
                    if (state == null || state.blocked() == 0 || player == null || player.isSpectator()) {
                        return;
                    }

                    // Vanilla continues to draw every usable slot with the active resource pack's
                    // normal art. We add only the permanent blocked slots, in the same 10-wide
                    // rows and native HUD position, so Matcha's heart art is never replaced.
                    double usableHealthPoints = (state.capacity() - state.blocked()) * 2.0D;
                    double vanillaLayoutHealthPoints = Math.max(usableHealthPoints, player.getHealth());
                    HeartHudLayout.Layout layout = HeartHudLayout.calculate(
                            graphics.guiWidth(), graphics.guiHeight(), state.capacity(), state.blocked(),
                            vanillaLayoutHealthPoints, player.getAbsorptionAmount());
                    for (HeartHudLayout.Slot slot : layout.blockedSlots()) {
                        int x = slot.x();
                        int y = slot.y();

                        graphics.blitSprite(RenderPipelines.GUI_TEXTURED, HEART_CONTAINER, x, y, 9, 9);

                        // Opaque near-black inner silhouette; the resource-pack container remains
                        // visible around it, making a blocked slot distinct from ordinary damage.
                        int black = 0xFF08090C;
                        graphics.fill(x + 1, y + 1, x + 4, y + 3, black);
                        graphics.fill(x + 5, y + 1, x + 8, y + 3, black);
                        graphics.fill(x + 1, y + 2, x + 8, y + 5, black);
                        graphics.fill(x + 2, y + 5, x + 7, y + 7, black);
                        graphics.fill(x + 3, y + 7, x + 6, y + 8, black);
                    }
                });
    }
}
