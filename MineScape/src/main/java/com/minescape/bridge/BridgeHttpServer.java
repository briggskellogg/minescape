package com.minescape.bridge;

import com.minescape.bridge.api.FamilyAssistRequest;
import com.minescape.bridge.api.FamilyAssistTicket;
import com.minescape.bridge.api.PlayerSummary;
import com.minescape.bridge.api.ServerHealth;
import com.minescape.bridge.api.WhitelistEntry;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;

import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Loopback-only control plane. It intentionally has no arbitrary command/console route. */
public final class BridgeHttpServer implements AutoCloseable {
    private final BridgeConfiguration configuration;
    private final BridgeBackend backend;
    private final BearerTokenAuthenticator authenticator;
    private final HttpServer server;
    private final ExecutorService executor;

    public BridgeHttpServer(BridgeConfiguration configuration, BridgeBackend backend) throws IOException {
        this.configuration = Objects.requireNonNull(configuration, "configuration");
        this.backend = Objects.requireNonNull(backend, "backend");
        this.authenticator = new BearerTokenAuthenticator(configuration.bearerToken());
        this.server = HttpServer.create(new InetSocketAddress(configuration.bindAddress(), configuration.port()), 0);
        this.executor = Executors.newVirtualThreadPerTaskExecutor();
        server.setExecutor(executor);
        server.createContext("/v1/", this::handle);
    }

    public void start() {
        server.start();
    }

    public InetSocketAddress address() {
        return server.getAddress();
    }

    @Override
    public void close() {
        server.stop(1);
        executor.close();
    }

    private void handle(HttpExchange exchange) throws IOException {
        try {
            if (!exchange.getRemoteAddress().getAddress().isLoopbackAddress()) {
                sendError(exchange, 403, "loopback_required", "Bridge accepts only local host clients");
                return;
            }
            if (!authenticator.authenticate(exchange.getRequestHeaders().getFirst("Authorization"))) {
                exchange.getResponseHeaders().set("WWW-Authenticate", "Bearer realm=\"MineScape Bridge\"");
                sendError(exchange, 401, "unauthorized", "A valid local Bridge token is required");
                return;
            }
            route(exchange);
        } catch (RequestTooLargeException problem) {
            sendError(exchange, 413, "request_too_large", problem.getMessage());
        } catch (IllegalArgumentException problem) {
            sendError(exchange, 400, "invalid_request", problem.getMessage());
        } catch (Exception problem) {
            sendError(exchange, 500, "bridge_failure", "Bridge could not complete the typed operation");
        } finally {
            exchange.close();
        }
    }

    private void route(HttpExchange exchange) throws IOException {
        String method = exchange.getRequestMethod();
        String path = exchange.getRequestURI().getPath();
        if (method.equals("GET") && path.equals("/v1/health")) {
            send(exchange, 200, health(backend.health()));
            return;
        }
        if (method.equals("GET") && path.equals("/v1/players")) {
            send(exchange, 200, Map.of("players", backend.players().stream().map(this::player).toList()));
            return;
        }
        if (path.equals("/v1/whitelist")) {
            if (method.equals("GET")) {
                send(exchange, 200, Map.of("entries", backend.whitelist().stream().map(this::whitelist).toList()));
                return;
            }
            if (method.equals("POST")) {
                Map<String, Object> body = body(exchange);
                UUID playerId = UUID.fromString(requiredString(body, "uuid"));
                String name = optionalString(body, "displayName");
                send(exchange, 202, whitelist(backend.addToWhitelist(playerId, name)));
                return;
            }
            methodNotAllowed(exchange, "GET, POST");
            return;
        }
        if (path.startsWith("/v1/whitelist/") && method.equals("DELETE")) {
            UUID playerId = UUID.fromString(path.substring("/v1/whitelist/".length()));
            boolean removed = backend.removeFromWhitelist(playerId);
            send(exchange, removed ? 200 : 404, Map.of("removed", removed, "uuid", playerId.toString()));
            return;
        }
        if (path.equals("/v1/family-assist")) {
            if (method.equals("GET")) {
                send(exchange, 200, Map.of("tickets",
                        backend.familyAssistTickets().stream().map(this::assist).toList()));
                return;
            }
            if (method.equals("POST")) {
                Map<String, Object> body = body(exchange);
                FamilyAssistRequest request = new FamilyAssistRequest(
                        UUID.fromString(requiredString(body, "playerId")),
                        FamilyAssistRequest.Action.valueOf(requiredString(body, "action")),
                        requiredString(body, "reason"));
                send(exchange, 202, assist(backend.requestFamilyAssist(request)));
                return;
            }
            methodNotAllowed(exchange, "GET, POST");
            return;
        }
        sendError(exchange, 404, "not_found", "No such Bridge capability");
    }

    private Map<String, Object> body(HttpExchange exchange) throws IOException {
        byte[] bytes = exchange.getRequestBody().readNBytes(configuration.maxRequestBytes() + 1);
        if (bytes.length > configuration.maxRequestBytes()) {
            throw new RequestTooLargeException();
        }
        return Json.parseFlatObject(new String(bytes, StandardCharsets.UTF_8));
    }

    private static String requiredString(Map<String, Object> body, String name) {
        Object value = body.get(name);
        if (!(value instanceof String string) || string.isBlank()) {
            throw new IllegalArgumentException("property '" + name + "' must be a nonblank string");
        }
        return string;
    }

    private static String optionalString(Map<String, Object> body, String name) {
        Object value = body.get(name);
        if (value == null) {
            return "";
        }
        if (!(value instanceof String string)) {
            throw new IllegalArgumentException("property '" + name + "' must be a string");
        }
        return string;
    }

    private Map<String, Object> health(ServerHealth health) {
        Map<String, Object> result = new LinkedHashMap<>();
        result.put("status", health.status().name());
        result.put("bridgeVersion", health.bridgeVersion());
        result.put("minecraftVersion", health.minecraftVersion());
        result.put("worldEpoch", health.worldEpoch());
        result.put("onlinePlayers", health.onlinePlayers());
        result.put("observedAt", health.observedAt().toString());
        result.put("detail", health.detail());
        return result;
    }

    private Map<String, Object> player(PlayerSummary player) {
        Map<String, Object> result = new LinkedHashMap<>();
        result.put("uuid", player.playerId().toString());
        result.put("currentName", player.currentName());
        result.put("online", player.online());
        result.put("accessRole", player.accessRole());
        result.put("sessionMode", player.sessionMode().name());
        result.put("countsTowardFamilyExploration", player.countsTowardFamilyExploration());
        return result;
    }

    private Map<String, Object> whitelist(WhitelistEntry entry) {
        Map<String, Object> result = new LinkedHashMap<>();
        result.put("uuid", entry.playerId().toString());
        result.put("displayName", entry.displayName());
        result.put("status", entry.status().name());
        result.put("changedAt", entry.changedAt().toString());
        return result;
    }

    private Map<String, Object> assist(FamilyAssistTicket ticket) {
        Map<String, Object> result = new LinkedHashMap<>();
        result.put("ticketId", ticket.ticketId().toString());
        result.put("playerId", ticket.request().playerId().toString());
        result.put("action", ticket.request().action().name());
        result.put("reason", ticket.request().reason());
        result.put("status", ticket.status().name());
        result.put("createdAt", ticket.createdAt().toString());
        return result;
    }

    private static void methodNotAllowed(HttpExchange exchange, String allow) throws IOException {
        exchange.getResponseHeaders().set("Allow", allow);
        sendError(exchange, 405, "method_not_allowed", "The capability does not accept this method");
    }

    private static void sendError(HttpExchange exchange, int status, String code, String message) throws IOException {
        send(exchange, status, Map.of("error", Map.of("code", code, "message", message)));
    }

    private static void send(HttpExchange exchange, int status, Object body) throws IOException {
        byte[] bytes = Json.encode(body).getBytes(StandardCharsets.UTF_8);
        exchange.getResponseHeaders().set("Content-Type", "application/json; charset=utf-8");
        exchange.getResponseHeaders().set("Cache-Control", "no-store");
        exchange.getResponseHeaders().set("X-Content-Type-Options", "nosniff");
        exchange.sendResponseHeaders(status, bytes.length);
        exchange.getResponseBody().write(bytes);
    }

    private static final class RequestTooLargeException extends IllegalArgumentException {
        RequestTooLargeException() {
            super("request body exceeds the configured limit");
        }
    }
}
