package com.minescape.core.state;

import com.minescape.core.io.AtomicFiles;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Objects;
import java.util.Properties;
import java.util.function.Function;

/** Small synchronized durable key/value store. Mutations are persisted atomically before returning. */
public final class DurableProperties {
    private final Path path;
    private final Properties values;

    private DurableProperties(Path path, Properties values) {
        this.path = path;
        this.values = values;
    }

    public static DurableProperties open(Path path) throws IOException {
        Objects.requireNonNull(path, "path");
        Properties loaded = new Properties();
        if (Files.exists(path)) {
            try (var input = Files.newInputStream(path)) {
                loaded.load(input);
            }
        }
        return new DurableProperties(path.toAbsolutePath().normalize(), loaded);
    }

    public synchronized String get(String key) {
        return values.getProperty(key);
    }

    public synchronized String getOrDefault(String key, String fallback) {
        return values.getProperty(key, fallback);
    }

    public synchronized boolean contains(String key) {
        return values.containsKey(key);
    }

    public synchronized Map<String, String> withPrefix(String prefix) {
        Map<String, String> result = new LinkedHashMap<>();
        values.stringPropertyNames().stream()
                .filter(key -> key.startsWith(prefix))
                .sorted()
                .forEach(key -> result.put(key, values.getProperty(key)));
        return Map.copyOf(result);
    }

    public synchronized void put(String key, String value) throws IOException {
        values.setProperty(key, value);
        persist();
    }

    public synchronized void remove(String key) throws IOException {
        values.remove(key);
        persist();
    }

    public synchronized <T> T update(Function<Editor, T> mutation) throws IOException {
        Properties candidate = new Properties();
        candidate.putAll(values);
        T result = mutation.apply(new Editor(candidate));
        persist(candidate);
        values.clear();
        values.putAll(candidate);
        return result;
    }

    private void persist() throws IOException {
        persist(values);
    }

    private void persist(Properties candidate) throws IOException {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        candidate.store(bytes, "MineScape durable state; managed by Bridge/Core");
        AtomicFiles.write(path, bytes.toByteArray());
    }

    public static final class Editor {
        private final Properties properties;

        private Editor(Properties properties) {
            this.properties = properties;
        }

        public String get(String key) {
            return properties.getProperty(key);
        }

        public boolean contains(String key) {
            return properties.containsKey(key);
        }

        public void put(String key, String value) {
            properties.setProperty(key, value);
        }

        public void remove(String key) {
            properties.remove(key);
        }
    }
}
