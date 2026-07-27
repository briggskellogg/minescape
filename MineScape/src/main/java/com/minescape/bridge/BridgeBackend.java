package com.minescape.bridge;

import com.minescape.bridge.api.FamilyAssistRequest;
import com.minescape.bridge.api.FamilyAssistTicket;
import com.minescape.bridge.api.PlayerSummary;
import com.minescape.bridge.api.ServerHealth;
import com.minescape.bridge.api.WhitelistEntry;

import java.io.IOException;
import java.util.List;
import java.util.UUID;

public interface BridgeBackend {
    ServerHealth health();

    List<PlayerSummary> players();

    List<WhitelistEntry> whitelist();

    WhitelistEntry addToWhitelist(UUID playerId, String displayName) throws IOException;

    boolean removeFromWhitelist(UUID playerId) throws IOException;

    List<FamilyAssistTicket> familyAssistTickets();

    FamilyAssistTicket requestFamilyAssist(FamilyAssistRequest request) throws IOException;
}
