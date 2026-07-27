package com.minescape.core.steward;

import com.minescape.core.state.DurableProperties;

import java.io.IOException;
import java.time.Duration;
import java.time.Instant;
import java.util.Objects;
import java.util.Optional;
import java.util.UUID;

public final class StewardRegistry {
    private static final Duration MAXIMUM_LEASE = Duration.ofMinutes(30);
    private final DurableProperties state;
    private final UUID parentId;

    public StewardRegistry(DurableProperties state, UUID parentId) {
        this.state = Objects.requireNonNull(state, "state");
        this.parentId = Objects.requireNonNull(parentId, "parentId");
    }

    public synchronized StewardLease arm(UUID playerId, Duration duration, String reason, Instant now)
            throws IOException {
        requireParent(playerId);
        if (duration.isNegative() || duration.isZero() || duration.compareTo(MAXIMUM_LEASE) > 0) {
            throw new IllegalArgumentException("Steward lease must be between one instant and 30 minutes");
        }
        StewardLease lease = new StewardLease(
                UUID.randomUUID(), playerId, StewardLease.State.ARMED_SURVIVAL,
                now, now.plus(duration), reason);
        write(lease);
        return lease;
    }

    public synchronized StewardLease grantCreative(UUID playerId, UUID leaseId, Instant now) throws IOException {
        StewardLease active = get(playerId).orElseThrow(() -> new IllegalStateException("no armed lease"));
        if (!active.leaseId().equals(leaseId) || !active.activeAt(now)) {
            throw new IllegalStateException("lease is absent, expired, or mismatched");
        }
        StewardLease updated = active.withState(StewardLease.State.CREATIVE);
        write(updated);
        return updated;
    }

    public synchronized Optional<StewardLease> get(UUID playerId) {
        String raw = state.get("steward.active." + playerId);
        if (raw == null) {
            return Optional.empty();
        }
        String[] fields = raw.split("\\|", -1);
        return Optional.of(new StewardLease(
                UUID.fromString(fields[0]),
                playerId,
                StewardLease.State.valueOf(fields[1]),
                Instant.parse(fields[2]),
                Instant.parse(fields[3]),
                fields[4]));
    }

    public synchronized void end(UUID playerId, UUID leaseId) throws IOException {
        StewardLease active = get(playerId).orElseThrow(() -> new IllegalStateException("no active lease"));
        if (!active.leaseId().equals(leaseId)) {
            throw new IllegalArgumentException("lease id mismatch");
        }
        write(active.withState(StewardLease.State.ENDED));
    }

    private void write(StewardLease lease) throws IOException {
        String encoded = String.join("|",
                lease.leaseId().toString(), lease.state().name(), lease.issuedAt().toString(),
                lease.expiresAt().toString(), lease.reason().replace("|", "/"));
        state.put("steward.active." + lease.playerId(), encoded);
    }

    private void requireParent(UUID playerId) {
        if (!parentId.equals(playerId)) {
            throw new SecurityException("only the enrolled parent can hold a Steward lease");
        }
    }
}
