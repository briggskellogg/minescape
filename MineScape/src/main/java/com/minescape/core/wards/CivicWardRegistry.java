package com.minescape.core.wards;

import com.minescape.core.state.DurableProperties;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.util.List;
import java.util.Objects;
import java.util.Optional;
import java.util.UUID;

public final class CivicWardRegistry {
    private final DurableProperties state;

    public CivicWardRegistry(DurableProperties state) {
        this.state = Objects.requireNonNull(state, "state");
    }

    public synchronized void register(CivicWard ward) throws IOException {
        state.put(key(ward.wardId()), encode(ward));
    }

    public Optional<CivicWard> find(UUID wardId) {
        String value = state.get(key(wardId));
        return value == null ? Optional.empty() : Optional.of(decode(value));
    }

    public List<CivicWard> all() {
        return state.withPrefix("wards.").values().stream().map(CivicWardRegistry::decode).toList();
    }

    public boolean isImmutableAnchor(String dimension, int x, int y, int z) {
        return all().stream().anyMatch(ward -> ward.active() && ward.immutable()
                && ward.dimension().equals(dimension)
                && ward.x() == x && ward.y() == y && ward.z() == z);
    }

    private static String encode(CivicWard ward) {
        return String.join("|",
                ward.wardId().toString(), b64(ward.settlementId()), b64(ward.buildingId()), b64(ward.dimension()),
                Integer.toString(ward.x()), Integer.toString(ward.y()), Integer.toString(ward.z()),
                Integer.toString(ward.radius()), Boolean.toString(ward.immutable()), Boolean.toString(ward.active()));
    }

    private static CivicWard decode(String value) {
        String[] fields = value.split("\\|", -1);
        if (fields.length != 10) {
            throw new IllegalStateException("invalid durable civic ward record");
        }
        return new CivicWard(UUID.fromString(fields[0]), unb64(fields[1]), unb64(fields[2]), unb64(fields[3]),
                Integer.parseInt(fields[4]), Integer.parseInt(fields[5]), Integer.parseInt(fields[6]),
                Integer.parseInt(fields[7]), Boolean.parseBoolean(fields[8]), Boolean.parseBoolean(fields[9]));
    }

    private static String key(UUID id) {
        return "wards." + id;
    }

    private static String b64(String value) {
        return Base64.getUrlEncoder().withoutPadding().encodeToString(value.getBytes(StandardCharsets.UTF_8));
    }

    private static String unb64(String value) {
        return new String(Base64.getUrlDecoder().decode(value), StandardCharsets.UTF_8);
    }
}
