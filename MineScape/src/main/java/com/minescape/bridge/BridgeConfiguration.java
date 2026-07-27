package com.minescape.bridge;

import com.minescape.core.io.AtomicFiles;

import java.io.IOException;
import java.net.InetAddress;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.SecureRandom;
import java.util.Base64;
import java.util.Properties;

public record BridgeConfiguration(InetAddress bindAddress, int port, int maxRequestBytes, String bearerToken) {
    private static final SecureRandom RANDOM = new SecureRandom();

    public BridgeConfiguration {
        if (!bindAddress.isLoopbackAddress()) {
            throw new IllegalArgumentException("MineScape Bridge must bind to loopback");
        }
        if (port < 1 || port > 65_535) {
            throw new IllegalArgumentException("port out of range");
        }
        if (maxRequestBytes < 256 || maxRequestBytes > 1_048_576) {
            throw new IllegalArgumentException("unsafe request-size limit");
        }
        if (bearerToken == null || bearerToken.length() < 32) {
            throw new IllegalArgumentException("bearer token must contain at least 32 characters");
        }
    }

    public static BridgeConfiguration loadOrCreate(Path configDirectory) throws IOException {
        Files.createDirectories(configDirectory);
        Path settingsFile = configDirectory.resolve("bridge.properties");
        Path tokenFile = configDirectory.resolve("bridge.token");

        Properties settings = new Properties();
        if (Files.exists(settingsFile)) {
            try (var input = Files.newInputStream(settingsFile)) {
                settings.load(input);
            }
        } else {
            String defaults = "bridge.bind=127.0.0.1\nbridge.port=8765\nbridge.max_request_bytes=16384\n";
            AtomicFiles.writeUtf8(settingsFile, defaults);
            settings.load(new java.io.StringReader(defaults));
        }

        String token;
        if (Files.exists(tokenFile)) {
            token = Files.readString(tokenFile, StandardCharsets.UTF_8).trim();
        } else {
            byte[] random = new byte[32];
            RANDOM.nextBytes(random);
            token = Base64.getUrlEncoder().withoutPadding().encodeToString(random);
            AtomicFiles.writeUtf8(tokenFile, token + System.lineSeparator());
        }

        InetAddress bind = InetAddress.getByName(settings.getProperty("bridge.bind", "127.0.0.1"));
        int port = Integer.parseInt(settings.getProperty("bridge.port", "8765"));
        int requestLimit = Integer.parseInt(settings.getProperty("bridge.max_request_bytes", "16384"));
        return new BridgeConfiguration(bind, port, requestLimit, token);
    }
}
