from __future__ import annotations

import json
import stat
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .util import MineJammerError, normalized_member, sha256_bytes, sha512_file


@dataclass(frozen=True)
class Resource:
    path: str
    size: int
    sha256: str


class ArchiveView:
    """Read-only normalized view over a zip file or extracted directory."""

    def __init__(self, source: str | Path):
        self.source = Path(source).resolve(strict=True)
        self._zip: zipfile.ZipFile | None = None
        self._members: dict[str, zipfile.ZipInfo | Path] = {}
        if self.source.is_dir():
            for path in self.source.rglob("*"):
                if path.is_symlink():
                    raise MineJammerError(f"symlink is not allowed in archive view: {path}")
                if path.is_file():
                    name = normalized_member(path.relative_to(self.source).as_posix())
                    self._insert(name, path)
        else:
            self._zip = zipfile.ZipFile(self.source)
            for info in self._zip.infolist():
                name = normalized_member(info.filename)
                if info.is_dir():
                    continue
                mode = info.external_attr >> 16
                if stat.S_ISLNK(mode):
                    raise MineJammerError(f"symlink member is not allowed: {name}")
                self._insert(name, info)

    def _insert(self, name: str, value: zipfile.ZipInfo | Path) -> None:
        folded = name.casefold()
        for existing in self._members:
            if existing.casefold() == folded:
                raise MineJammerError(f"duplicate or case-colliding member: {existing!r} / {name!r}")
        self._members[name] = value

    def close(self) -> None:
        if self._zip:
            self._zip.close()

    def __enter__(self) -> "ArchiveView":
        return self

    def __exit__(self, *_args: object) -> None:
        self.close()

    @property
    def names(self) -> tuple[str, ...]:
        return tuple(sorted(self._members))

    def has(self, name: str) -> bool:
        return normalized_member(name) in self._members

    def read(self, name: str) -> bytes:
        normalized = normalized_member(name)
        member = self._members.get(normalized)
        if member is None:
            raise MineJammerError(f"resource is absent from {self.source.name}: {normalized}")
        if isinstance(member, Path):
            return member.read_bytes()
        assert self._zip is not None
        return self._zip.read(member)

    def json(self, name: str) -> Any:
        try:
            return json.loads(self.read(name).decode("utf-8-sig"))
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise MineJammerError(f"invalid JSON resource {name} in {self.source.name}: {error}") from error


def audit_zip(path: str | Path, max_uncompressed: int = 2 * 1024 * 1024 * 1024) -> dict[str, Any]:
    source = Path(path).resolve(strict=True)
    issues: list[str] = []
    members: list[dict[str, Any]] = []
    seen: set[str] = set()
    total = 0
    with zipfile.ZipFile(source) as archive:
        for info in archive.infolist():
            try:
                name = normalized_member(info.filename)
            except MineJammerError as error:
                issues.append(str(error))
                continue
            if info.is_dir():
                continue
            folded = name.casefold()
            if folded in seen:
                issues.append(f"duplicate/case-colliding member: {name}")
            seen.add(folded)
            mode = info.external_attr >> 16
            if stat.S_ISLNK(mode):
                issues.append(f"symlink member: {name}")
            total += info.file_size
            ratio = info.file_size / max(info.compress_size, 1)
            if ratio > 500 and info.file_size > 10 * 1024 * 1024:
                issues.append(f"suspicious compression ratio {ratio:.1f}: {name}")
            members.append({"path": name, "size": info.file_size, "compressed": info.compress_size})
    if total > max_uncompressed:
        issues.append(f"uncompressed size {total} exceeds safety limit {max_uncompressed}")
    return {
        "schema": "minescape.zip-audit.v1",
        "source": source.name,
        "sha512": sha512_file(source),
        "member_count": len(members),
        "uncompressed_bytes": total,
        "issues": sorted(issues),
        "ok": not issues,
        "members": members,
    }


def collision_report(left: str | Path, right: str | Path, left_name: str, right_name: str) -> dict[str, Any]:
    with ArchiveView(left) as left_view, ArchiveView(right) as right_view:
        shared = sorted(set(left_view.names) & set(right_view.names))
        collisions = []
        for name in shared:
            left_bytes = left_view.read(name)
            right_bytes = right_view.read(name)
            collisions.append({
                "path": name,
                "identical": left_bytes == right_bytes,
                "left_sha256": sha256_bytes(left_bytes),
                "right_sha256": sha256_bytes(right_bytes),
                "world_critical": name.startswith("data/") and any(
                    token in name for token in ("worldgen/biome/", "worldgen/structure", "loot_table/", "tags/worldgen/")
                ),
            })
    return {
        "schema": "minescape.archive-collision.v1",
        "left": {"name": left_name, "sha512": sha512_file(left)},
        "right": {"name": right_name, "sha512": sha512_file(right)},
        "collision_count": len(collisions),
        "different_count": sum(not item["identical"] for item in collisions),
        "world_critical_count": sum(item["world_critical"] and not item["identical"] for item in collisions),
        "collisions": collisions,
    }
