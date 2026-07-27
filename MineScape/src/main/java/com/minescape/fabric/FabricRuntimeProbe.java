package com.minescape.fabric;

import com.minescape.bridge.RuntimeProbe;
import com.minescape.bridge.api.PlayerSummary;
import com.minescape.bridge.api.ServerHealth;
import com.minescape.core.session.SessionMode;

import java.lang.reflect.Method;
import java.time.Clock;
import java.util.ArrayList;
import java.util.Collection;
import java.util.List;
import java.util.UUID;

/** Uses only stable lifecycle typing; changing game accessor names degrade health instead of crashing. */
final class FabricRuntimeProbe implements RuntimeProbe {
    private final Object minecraftServer;
    private final Clock clock;

    FabricRuntimeProbe(Object minecraftServer, Clock clock) {
        this.minecraftServer = minecraftServer;
        this.clock = clock;
    }

    @Override
    public ServerHealth health() {
        try {
            return new ServerHealth(ServerHealth.Status.ONLINE, MineScapeMod.VERSION, "26.2",
                    "v1-unsealed", players().size(), clock.instant(), "Fabric server lifecycle is online");
        } catch (RuntimeException adapterFailure) {
            return new ServerHealth(ServerHealth.Status.DEGRADED, MineScapeMod.VERSION, "26.2",
                    "v1-unsealed", 0, clock.instant(), "26.2 player-list adapter requires release-gate review");
        }
    }

    @Override
    public List<PlayerSummary> players() {
        try {
            Object playerList = invoke(minecraftServer, "getPlayerList");
            Object raw = invoke(playerList, "getPlayers");
            if (!(raw instanceof Collection<?> players)) {
                throw new IllegalStateException("getPlayers did not return a collection");
            }
            List<PlayerSummary> result = new ArrayList<>();
            for (Object player : players) {
                UUID uuid = (UUID) invoke(player, "getUUID");
                Object profile = invoke(player, "getGameProfile");
                String name = stringAccessor(profile, "name", "getName");
                result.add(new PlayerSummary(uuid, name, true, "ENROLLED", SessionMode.FAMILY, true));
            }
            return List.copyOf(result);
        } catch (ReflectiveOperationException failure) {
            throw new IllegalStateException("Minecraft 26.2 runtime accessor mismatch", failure);
        }
    }

    private static Object invoke(Object target, String method) throws ReflectiveOperationException {
        Method accessor = target.getClass().getMethod(method);
        return accessor.invoke(target);
    }

    private static String stringAccessor(Object target, String... names) throws ReflectiveOperationException {
        for (String name : names) {
            try {
                Object value = invoke(target, name);
                if (value != null) {
                    return value.toString();
                }
            } catch (NoSuchMethodException ignored) {
                // Try the next official/authlib accessor spelling.
            }
        }
        throw new NoSuchMethodException("profile name accessor");
    }
}
