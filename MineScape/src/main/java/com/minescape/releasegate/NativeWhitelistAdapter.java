package com.minescape.releasegate;

import java.util.UUID;

@ReleaseGate("Prove UUID-backed native Fabric/Minecraft whitelist mutation and authenticated pending-player capture without disabling whitelist")
public interface NativeWhitelistAdapter {
    void approve(UUID playerId, String currentName);

    void revoke(UUID playerId);
}
