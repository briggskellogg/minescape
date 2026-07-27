package com.minescape.bridge;

import java.util.LinkedHashMap;
import java.util.Map;

/** Deliberately small JSON codec for the Bridge's flat command bodies and typed responses. */
final class Json {
    private Json() {
    }

    static String encode(Object value) {
        StringBuilder result = new StringBuilder();
        append(result, value);
        return result.toString();
    }

    private static void append(StringBuilder out, Object value) {
        if (value == null) {
            out.append("null");
        } else if (value instanceof String string) {
            appendString(out, string);
        } else if (value instanceof Number || value instanceof Boolean) {
            out.append(value);
        } else if (value instanceof Map<?, ?> map) {
            out.append('{');
            boolean first = true;
            for (Map.Entry<?, ?> entry : map.entrySet()) {
                if (!first) {
                    out.append(',');
                }
                first = false;
                appendString(out, String.valueOf(entry.getKey()));
                out.append(':');
                append(out, entry.getValue());
            }
            out.append('}');
        } else if (value instanceof Iterable<?> iterable) {
            out.append('[');
            boolean first = true;
            for (Object element : iterable) {
                if (!first) {
                    out.append(',');
                }
                first = false;
                append(out, element);
            }
            out.append(']');
        } else {
            throw new IllegalArgumentException("unsupported JSON value: " + value.getClass().getName());
        }
    }

    private static void appendString(StringBuilder out, String value) {
        out.append('"');
        for (int i = 0; i < value.length(); i++) {
            char current = value.charAt(i);
            switch (current) {
                case '"' -> out.append("\\\"");
                case '\\' -> out.append("\\\\");
                case '\b' -> out.append("\\b");
                case '\f' -> out.append("\\f");
                case '\n' -> out.append("\\n");
                case '\r' -> out.append("\\r");
                case '\t' -> out.append("\\t");
                default -> {
                    if (current < 0x20) {
                        out.append(String.format("\\u%04x", (int) current));
                    } else {
                        out.append(current);
                    }
                }
            }
        }
        out.append('"');
    }

    static Map<String, Object> parseFlatObject(String source) {
        Cursor cursor = new Cursor(source);
        Map<String, Object> result = cursor.object();
        cursor.whitespace();
        if (!cursor.end()) {
            throw new IllegalArgumentException("unexpected JSON after object");
        }
        return result;
    }

    private static final class Cursor {
        private final String source;
        private int index;

        Cursor(String source) {
            this.source = source;
        }

        Map<String, Object> object() {
            whitespace();
            expect('{');
            Map<String, Object> result = new LinkedHashMap<>();
            whitespace();
            if (take('}')) {
                return result;
            }
            while (true) {
                whitespace();
                String key = string();
                whitespace();
                expect(':');
                whitespace();
                Object value = scalar();
                if (result.containsKey(key)) {
                    throw new IllegalArgumentException("duplicate JSON property: " + key);
                }
                result.put(key, value);
                whitespace();
                if (take('}')) {
                    return result;
                }
                expect(',');
            }
        }

        Object scalar() {
            if (peek('"')) {
                return string();
            }
            if (source.startsWith("true", index)) {
                index += 4;
                return true;
            }
            if (source.startsWith("false", index)) {
                index += 5;
                return false;
            }
            if (source.startsWith("null", index)) {
                index += 4;
                return null;
            }
            int start = index;
            if (!end() && source.charAt(index) == '-') {
                index++;
            }
            while (!end() && Character.isDigit(source.charAt(index))) {
                index++;
            }
            if (start != index) {
                return Long.parseLong(source.substring(start, index));
            }
            throw new IllegalArgumentException("only scalar values are accepted in command bodies");
        }

        String string() {
            expect('"');
            StringBuilder result = new StringBuilder();
            while (!end()) {
                char current = source.charAt(index++);
                if (current == '"') {
                    return result.toString();
                }
                if (current != '\\') {
                    if (current < 0x20) {
                        throw new IllegalArgumentException("control character in JSON string");
                    }
                    result.append(current);
                    continue;
                }
                if (end()) {
                    throw new IllegalArgumentException("unterminated JSON escape");
                }
                char escaped = source.charAt(index++);
                switch (escaped) {
                    case '"', '\\', '/' -> result.append(escaped);
                    case 'b' -> result.append('\b');
                    case 'f' -> result.append('\f');
                    case 'n' -> result.append('\n');
                    case 'r' -> result.append('\r');
                    case 't' -> result.append('\t');
                    case 'u' -> {
                        if (index + 4 > source.length()) {
                            throw new IllegalArgumentException("short JSON unicode escape");
                        }
                        result.append((char) Integer.parseInt(source.substring(index, index + 4), 16));
                        index += 4;
                    }
                    default -> throw new IllegalArgumentException("invalid JSON escape");
                }
            }
            throw new IllegalArgumentException("unterminated JSON string");
        }

        void whitespace() {
            while (!end() && Character.isWhitespace(source.charAt(index))) {
                index++;
            }
        }

        boolean take(char expected) {
            if (peek(expected)) {
                index++;
                return true;
            }
            return false;
        }

        boolean peek(char expected) {
            return !end() && source.charAt(index) == expected;
        }

        void expect(char expected) {
            if (!take(expected)) {
                throw new IllegalArgumentException("expected '" + expected + "' at offset " + index);
            }
        }

        boolean end() {
            return index >= source.length();
        }
    }
}
