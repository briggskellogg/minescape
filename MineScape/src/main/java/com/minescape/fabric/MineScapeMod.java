package com.minescape.fabric;

import com.minescape.fabric.network.HeartSyncPayload;
import net.fabricmc.api.ModInitializer;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerLifecycleEvents;
import net.fabricmc.fabric.api.networking.v1.PayloadTypeRegistry;
import net.fabricmc.loader.api.FabricLoader;

import java.io.IOException;
import java.nio.file.Path;
import java.util.logging.Level;
import java.util.logging.Logger;

public final class MineScapeMod implements ModInitializer {
    public static final String MOD_ID = "minescape";
    public static final String VERSION = "0.1.0";
    private static final Logger LOGGER = Logger.getLogger("MineScape");
    private static volatile MineScapeRuntime runtime;

    @Override
    public void onInitialize() {
        PayloadTypeRegistry.clientboundPlay().register(HeartSyncPayload.TYPE, HeartSyncPayload.CODEC);
        FabricHeartLifecycle.install(() -> runtime);
        ServerLifecycleEvents.SERVER_STARTED.register(server -> {
            Path config = FabricLoader.getInstance().getConfigDir().resolve(MOD_ID);
            Path state = FabricLoader.getInstance().getGameDir().resolve("minescape-state");
            try {
                runtime = MineScapeRuntime.start(server, config, state);
                LOGGER.info("MineScape Bridge/Core online on loopback");
            } catch (IOException failure) {
                LOGGER.log(Level.SEVERE, "MineScape Bridge/Core failed to start; stopping fail-closed", failure);
                server.halt(false);
            }
        });
        ServerLifecycleEvents.SERVER_STOPPING.register(server -> {
            MineScapeRuntime current = runtime;
            runtime = null;
            if (current != null) {
                current.close();
            }
        });
    }
}
