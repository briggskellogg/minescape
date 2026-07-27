package com.minescape.bridge.api;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

public record FamilyAssistTicket(
        UUID ticketId,
        FamilyAssistRequest request,
        Status status,
        Instant createdAt) {

    public enum Status {
        PENDING_RELEASE_GATE_ADAPTER,
        APPLIED,
        REJECTED
    }

    public FamilyAssistTicket {
        Objects.requireNonNull(ticketId, "ticketId");
        Objects.requireNonNull(request, "request");
        Objects.requireNonNull(status, "status");
        Objects.requireNonNull(createdAt, "createdAt");
    }
}
