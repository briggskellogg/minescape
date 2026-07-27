from __future__ import annotations

import gzip
import io
import struct
from typing import Any

from .util import MineJammerError


class _Reader:
    def __init__(self, data: bytes):
        self.stream = io.BytesIO(gzip.decompress(data) if data[:2] == b"\x1f\x8b" else data)

    def take(self, size: int) -> bytes:
        value = self.stream.read(size)
        if len(value) != size:
            raise MineJammerError("truncated NBT payload")
        return value

    def unpack(self, fmt: str) -> Any:
        return struct.unpack(">" + fmt, self.take(struct.calcsize(">" + fmt)))[0]

    def string(self) -> str:
        length = self.unpack("H")
        try:
            return self.take(length).decode("utf-8")
        except UnicodeDecodeError as error:
            raise MineJammerError(f"invalid UTF-8 in NBT string: {error}") from error

    def payload(self, tag: int) -> Any:
        if tag == 1:
            return self.unpack("b")
        if tag == 2:
            return self.unpack("h")
        if tag == 3:
            return self.unpack("i")
        if tag == 4:
            return self.unpack("q")
        if tag == 5:
            return self.unpack("f")
        if tag == 6:
            return self.unpack("d")
        if tag == 7:
            length = self.unpack("i")
            if length < 0:
                raise MineJammerError("negative NBT byte-array length")
            return list(self.take(length))
        if tag == 8:
            return self.string()
        if tag == 9:
            element = self.unpack("B")
            length = self.unpack("i")
            if length < 0:
                raise MineJammerError("negative NBT list length")
            return [self.payload(element) for _ in range(length)]
        if tag == 10:
            result = {}
            while True:
                child_tag = self.unpack("B")
                if child_tag == 0:
                    return result
                name = self.string()
                result[name] = self.payload(child_tag)
        if tag == 11:
            length = self.unpack("i")
            if length < 0:
                raise MineJammerError("negative NBT int-array length")
            return [self.unpack("i") for _ in range(length)]
        if tag == 12:
            length = self.unpack("i")
            if length < 0:
                raise MineJammerError("negative NBT long-array length")
            return [self.unpack("q") for _ in range(length)]
        raise MineJammerError(f"unsupported NBT tag id: {tag}")


def loads(data: bytes) -> Any:
    reader = _Reader(data)
    tag = reader.unpack("B")
    if tag == 0:
        raise MineJammerError("NBT root cannot be TAG_End")
    reader.string()  # Root name is intentionally irrelevant to structure data.
    return reader.payload(tag)


def contains_text(value: Any, needle: str) -> bool:
    if isinstance(value, str):
        return needle in value
    if isinstance(value, dict):
        return any(contains_text(child, needle) for child in value.values())
    if isinstance(value, list):
        return any(contains_text(child, needle) for child in value)
    return False


def compounds(value: Any):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from compounds(child)
    elif isinstance(value, list):
        for child in value:
            yield from compounds(child)
