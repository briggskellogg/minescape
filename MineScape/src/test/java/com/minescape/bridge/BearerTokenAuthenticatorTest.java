package com.minescape.bridge;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class BearerTokenAuthenticatorTest {
    private static final String TOKEN = "0123456789abcdef0123456789abcdef01234567";

    @Test
    void acceptsOnlyTheExactBearerCredential() {
        BearerTokenAuthenticator auth = new BearerTokenAuthenticator(TOKEN);

        assertTrue(auth.authenticate("Bearer " + TOKEN));
        assertFalse(auth.authenticate(null));
        assertFalse(auth.authenticate(TOKEN));
        assertFalse(auth.authenticate("bearer " + TOKEN));
        assertFalse(auth.authenticate("Bearer " + TOKEN + "x"));
        assertFalse(auth.authenticate("Bearer wrong"));
    }
}
