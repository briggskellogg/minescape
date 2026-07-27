package com.minescape.core.hearts;

@FunctionalInterface
public interface CrystalProofVerifier {
    boolean isGenuine(CrystalUseEvent event);
}
