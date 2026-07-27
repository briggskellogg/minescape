package com.minescape.releasegate;

import com.minescape.core.steward.StewardLease;

@ReleaseGate("Prove inventory snapshot/isolation, no leakage or damage, visible lease state, timeout restore, and child ineligibility")
public interface StewardGameAdapter {
    void enter(StewardLease lease);

    void restoreAndExit(StewardLease lease);
}
