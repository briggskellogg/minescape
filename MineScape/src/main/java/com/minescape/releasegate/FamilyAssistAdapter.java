package com.minescape.releasegate;

import com.minescape.bridge.api.FamilyAssistTicket;

@ReleaseGate("Prove typed Easy/Normal, dawn, public-spawn rescue and read-only Matcha hint operations with audit and no console surface")
public interface FamilyAssistAdapter {
    FamilyAssistTicket apply(FamilyAssistTicket ticket);
}
