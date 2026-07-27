package com.minescape.bridge;

import com.minescape.bridge.api.FamilyAssistRequest;
import com.minescape.bridge.api.FamilyAssistTicket;
import com.minescape.bridge.api.PlayerSummary;
import com.minescape.bridge.api.ServerHealth;
import com.minescape.bridge.api.WhitelistEntry;
import com.minescape.core.state.DurableProperties;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.time.Clock;
import java.time.Instant;
import java.util.Base64;
import java.util.List;
import java.util.Objects;
import java.util.UUID;

public final class DurableBridgeBackend implements BridgeBackend {
    private final DurableProperties state;
    private final RuntimeProbe runtime;
    private final Clock clock;

    public DurableBridgeBackend(DurableProperties state, RuntimeProbe runtime, Clock clock) {
        this.state = Objects.requireNonNull(state, "state");
        this.runtime = Objects.requireNonNull(runtime, "runtime");
        this.clock = Objects.requireNonNull(clock, "clock");
    }

    @Override
    public ServerHealth health() {
        return runtime.health();
    }

    @Override
    public List<PlayerSummary> players() {
        return runtime.players();
    }

    @Override
    public List<WhitelistEntry> whitelist() {
        return state.withPrefix("access.whitelist.").values().stream()
                .map(DurableBridgeBackend::decodeWhitelist)
                .toList();
    }

    @Override
    public synchronized WhitelistEntry addToWhitelist(UUID playerId, String displayName) throws IOException {
        WhitelistEntry entry = new WhitelistEntry(
                playerId,
                displayName,
                WhitelistEntry.Status.PENDING_NATIVE_SYNC,
                clock.instant());
        state.put("access.whitelist." + playerId, encodeWhitelist(entry));
        return entry;
    }

    @Override
    public synchronized boolean removeFromWhitelist(UUID playerId) throws IOException {
        String key = "access.whitelist." + playerId;
        if (!state.contains(key)) {
            return false;
        }
        state.remove(key);
        return true;
    }

    @Override
    public List<FamilyAssistTicket> familyAssistTickets() {
        return state.withPrefix("assist.ticket.").values().stream()
                .map(DurableBridgeBackend::decodeTicket)
                .toList();
    }

    @Override
    public synchronized FamilyAssistTicket requestFamilyAssist(FamilyAssistRequest request) throws IOException {
        FamilyAssistTicket ticket = new FamilyAssistTicket(
                UUID.randomUUID(), request,
                FamilyAssistTicket.Status.PENDING_RELEASE_GATE_ADAPTER,
                clock.instant());
        state.put("assist.ticket." + ticket.ticketId(), encodeTicket(ticket));
        return ticket;
    }

    private static String encodeWhitelist(WhitelistEntry entry) {
        return String.join("|", entry.playerId().toString(), b64(entry.displayName()),
                entry.status().name(), entry.changedAt().toString());
    }

    private static WhitelistEntry decodeWhitelist(String value) {
        String[] fields = value.split("\\|", -1);
        return new WhitelistEntry(UUID.fromString(fields[0]), unb64(fields[1]),
                WhitelistEntry.Status.valueOf(fields[2]), Instant.parse(fields[3]));
    }

    private static String encodeTicket(FamilyAssistTicket ticket) {
        return String.join("|", ticket.ticketId().toString(), ticket.request().playerId().toString(),
                ticket.request().action().name(), b64(ticket.request().reason()), ticket.status().name(),
                ticket.createdAt().toString());
    }

    private static FamilyAssistTicket decodeTicket(String value) {
        String[] fields = value.split("\\|", -1);
        FamilyAssistRequest request = new FamilyAssistRequest(
                UUID.fromString(fields[1]), FamilyAssistRequest.Action.valueOf(fields[2]), unb64(fields[3]));
        return new FamilyAssistTicket(UUID.fromString(fields[0]), request,
                FamilyAssistTicket.Status.valueOf(fields[4]), Instant.parse(fields[5]));
    }

    private static String b64(String value) {
        String safe = value == null ? "" : value;
        return Base64.getUrlEncoder().withoutPadding().encodeToString(safe.getBytes(StandardCharsets.UTF_8));
    }

    private static String unb64(String value) {
        return new String(Base64.getUrlDecoder().decode(value), StandardCharsets.UTF_8);
    }
}
