package com.minescape.bridge;

import com.minescape.bridge.api.FamilyAssistRequest;
import com.minescape.bridge.api.FamilyAssistTicket;
import com.minescape.bridge.api.PlayerSummary;
import com.minescape.bridge.api.ServerHealth;
import com.minescape.bridge.api.WhitelistEntry;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class BridgeHttpServerTest {
    private static final String TOKEN = "0123456789abcdef0123456789abcdef01234567";
    private final StubBackend backend = new StubBackend();
    private BridgeHttpServer server;
    private URI base;

    @BeforeEach
    void start() throws Exception {
        int port;
        try (ServerSocket reservation = new ServerSocket(0, 1, InetAddress.getLoopbackAddress())) {
            port = reservation.getLocalPort();
        }
        server = new BridgeHttpServer(
                new BridgeConfiguration(InetAddress.getLoopbackAddress(), port, 16_384, TOKEN), backend);
        server.start();
        base = URI.create("http://127.0.0.1:" + server.address().getPort());
    }

    @AfterEach
    void stop() {
        server.close();
    }

    @Test
    void healthRejectsMissingCredentialAndAcceptsExactBearerToken() throws Exception {
        HttpClient client = HttpClient.newHttpClient();
        HttpResponse<String> denied = client.send(
                HttpRequest.newBuilder(base.resolve("/v1/health")).GET().build(),
                HttpResponse.BodyHandlers.ofString());
        HttpResponse<String> accepted = client.send(
                authorized(base.resolve("/v1/health")).GET().build(),
                HttpResponse.BodyHandlers.ofString());

        assertEquals(401, denied.statusCode());
        assertEquals(200, accepted.statusCode());
        assertTrue(accepted.body().contains("\"status\":\"ONLINE\""));
    }

    @Test
    void uuidWhitelistAndFamilyAssistUseTypedJsonCapabilities() throws Exception {
        HttpClient client = HttpClient.newHttpClient();
        UUID playerId = UUID.randomUUID();
        String whitelistBody = "{\"uuid\":\"" + playerId + "\"}";
        HttpResponse<String> whitelist = client.send(
                authorized(base.resolve("/v1/whitelist"))
                        .header("Content-Type", "application/json")
                        .POST(HttpRequest.BodyPublishers.ofString(whitelistBody)).build(),
                HttpResponse.BodyHandlers.ofString());

        String assistBody = "{\"playerId\":\"" + playerId
                + "\",\"action\":\"START_AT_DAWN\",\"reason\":\"family session\"}";
        HttpResponse<String> assist = client.send(
                authorized(base.resolve("/v1/family-assist"))
                        .header("Content-Type", "application/json")
                        .POST(HttpRequest.BodyPublishers.ofString(assistBody)).build(),
                HttpResponse.BodyHandlers.ofString());

        assertEquals(202, whitelist.statusCode());
        assertEquals(playerId, backend.whitelist.getFirst().playerId());
        assertEquals(202, assist.statusCode());
        assertEquals(FamilyAssistRequest.Action.START_AT_DAWN,
                backend.tickets.getFirst().request().action());
    }

    private static HttpRequest.Builder authorized(URI uri) {
        return HttpRequest.newBuilder(uri).header("Authorization", "Bearer " + TOKEN);
    }

    private static final class StubBackend implements BridgeBackend {
        private final List<WhitelistEntry> whitelist = new ArrayList<>();
        private final List<FamilyAssistTicket> tickets = new ArrayList<>();

        @Override
        public ServerHealth health() {
            return new ServerHealth(ServerHealth.Status.ONLINE, "test", "26.2", "test-epoch",
                    0, Instant.EPOCH, "test");
        }

        @Override
        public List<PlayerSummary> players() {
            return List.of();
        }

        @Override
        public List<WhitelistEntry> whitelist() {
            return List.copyOf(whitelist);
        }

        @Override
        public WhitelistEntry addToWhitelist(UUID playerId, String displayName) {
            WhitelistEntry entry = new WhitelistEntry(
                    playerId, displayName, WhitelistEntry.Status.PENDING_NATIVE_SYNC, Instant.EPOCH);
            whitelist.add(entry);
            return entry;
        }

        @Override
        public boolean removeFromWhitelist(UUID playerId) {
            return whitelist.removeIf(entry -> entry.playerId().equals(playerId));
        }

        @Override
        public List<FamilyAssistTicket> familyAssistTickets() {
            return List.copyOf(tickets);
        }

        @Override
        public FamilyAssistTicket requestFamilyAssist(FamilyAssistRequest request) {
            FamilyAssistTicket ticket = new FamilyAssistTicket(
                    UUID.randomUUID(), request,
                    FamilyAssistTicket.Status.PENDING_RELEASE_GATE_ADAPTER, Instant.EPOCH);
            tickets.add(ticket);
            return ticket;
        }
    }
}
