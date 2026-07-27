package com.minescape.releasegate;

import com.minescape.core.wards.CivicWard;

@ReleaseGate("Prove only registered civic anchors reject breaking, pistons, explosions and environmental replacement; surrounding builds stay editable")
public interface WardProtectionAdapter {
    void protect(CivicWard ward);

    void unprotect(CivicWard ward);
}
