package com.minescape.fabric;

import com.minescape.bridge.BridgeConfiguration;
import com.minescape.bridge.BridgeHttpServer;
import com.minescape.bridge.DurableBridgeBackend;
import com.minescape.core.exploration.ExplorationLedger;
import com.minescape.core.hearts.HeartRegistry;
import com.minescape.core.loot.LootEnrichmentEngine;
import com.minescape.core.loot.LootProvenanceLedger;
import com.minescape.core.session.SessionMode;
import com.minescape.core.state.DurableProperties;
import com.minescape.core.wards.CivicWardRegistry;

import java.io.IOException;
import java.io.Reader;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Clock;
import java.util.Locale;
import java.util.Properties;

final class MineScapeRuntime implements AutoCloseable {
    private final BridgeHttpServer bridge;
    final HeartRegistry hearts;
    final ExplorationLedger exploration;
    final CivicWardRegistry wards;
    final LootEnrichmentEngine lootEngine;
    final LootProvenanceLedger lootLedger;
    final SessionMode sessionMode;

    private MineScapeRuntime(
            BridgeHttpServer bridge,
            HeartRegistry hearts,
            ExplorationLedger exploration,
            CivicWardRegistry wards,
            LootEnrichmentEngine lootEngine,
            LootProvenanceLedger lootLedger,
            SessionMode sessionMode) {
        this.bridge = bridge;
        this.hearts = hearts;
        this.exploration = exploration;
        this.wards = wards;
        this.lootEngine = lootEngine;
        this.lootLedger = lootLedger;
        this.sessionMode = sessionMode;
    }

    static MineScapeRuntime start(Object minecraftServer, Path configDirectory, Path stateDirectory)
            throws IOException {
        DurableProperties durableState = DurableProperties.open(stateDirectory.resolve("minescape-state.properties"));
        BridgeConfiguration bridgeConfiguration = BridgeConfiguration.loadOrCreate(configDirectory);
        DurableBridgeBackend backend = new DurableBridgeBackend(
                durableState, new FabricRuntimeProbe(minecraftServer, Clock.systemUTC()), Clock.systemUTC());
        BridgeHttpServer bridge = new BridgeHttpServer(bridgeConfiguration, backend);
        MineScapeRuntime runtime = new MineScapeRuntime(
                bridge,
                new HeartRegistry(durableState),
                new ExplorationLedger(durableState),
                new CivicWardRegistry(durableState),
                new LootEnrichmentEngine(1),
                new LootProvenanceLedger(durableState),
                loadSessionMode(configDirectory.resolve("bridge.properties")));
        bridge.start();
        return runtime;
    }

    private static SessionMode loadSessionMode(Path settingsFile) throws IOException {
        Properties properties = new Properties();
        try (Reader input = Files.newBufferedReader(settingsFile)) {
            properties.load(input);
        }
        String configured = properties.getProperty("instance.mode", "minejammer")
                .trim().toLowerCase(Locale.ROOT);
        return switch (configured) {
            case "family" -> SessionMode.FAMILY;
            case "heart_qa" -> SessionMode.HEART_QA;
            case "minejammer" -> SessionMode.MINEJAMMER;
            default -> throw new IOException(
                    "instance.mode must be family, heart_qa, or minejammer; refusing unsafe default");
        };
    }

    @Override
    public void close() {
        bridge.close();
    }
}
