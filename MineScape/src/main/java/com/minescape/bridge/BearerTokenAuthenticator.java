package com.minescape.bridge;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Objects;

/** Exact Bearer scheme with a timing-safe token comparison. */
public final class BearerTokenAuthenticator {
    private static final String PREFIX = "Bearer ";
    private final byte[] expected;

    public BearerTokenAuthenticator(String expectedToken) {
        Objects.requireNonNull(expectedToken, "expectedToken");
        this.expected = expectedToken.getBytes(StandardCharsets.UTF_8);
    }

    public boolean authenticate(String authorizationHeader) {
        if (authorizationHeader == null || !authorizationHeader.startsWith(PREFIX)) {
            return false;
        }
        byte[] supplied = authorizationHeader.substring(PREFIX.length()).getBytes(StandardCharsets.UTF_8);
        return MessageDigest.isEqual(expected, supplied);
    }
}
