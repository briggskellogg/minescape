from __future__ import annotations

import hashlib
import json
import os
import shutil
import tempfile
import zipfile
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


class MineJammerError(RuntimeError):
    """A deliberate, user-readable safety failure."""


def load_json(path: str | os.PathLike[str]) -> Any:
    with Path(path).open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def canonical_json(value: Any) -> bytes:
    return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def atomic_write(path: str | os.PathLike[str], data: bytes) -> None:
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(prefix=f".{target.name}.", suffix=".tmp", dir=target.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, target)
    finally:
        temporary.unlink(missing_ok=True)


def dump_json(path: str | os.PathLike[str], value: Any) -> None:
    atomic_write(path, canonical_json(value))


def sha512_file(path: str | os.PathLike[str]) -> str:
    digest = hashlib.sha512()
    with Path(path).open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def normalized_member(name: str) -> str:
    candidate = name.replace("\\", "/")
    pure = PurePosixPath(candidate)
    if not candidate or candidate.startswith("/") or pure.is_absolute() or ".." in pure.parts:
        raise MineJammerError(f"unsafe archive member: {name!r}")
    if pure.parts and ":" in pure.parts[0]:
        raise MineJammerError(f"drive-qualified archive member: {name!r}")
    return pure.as_posix().lstrip("./")


def is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve(strict=False).relative_to(root.resolve(strict=False))
        return True
    except ValueError:
        return False


def iter_files(root: Path) -> Iterable[Path]:
    for base, directories, files in os.walk(root, followlinks=False):
        base_path = Path(base)
        for name in list(directories):
            if (base_path / name).is_symlink():
                raise MineJammerError(f"symlinked directory is not allowed: {base_path / name}")
        for name in files:
            path = base_path / name
            if path.is_symlink():
                raise MineJammerError(f"symlinked file is not allowed: {path}")
            yield path


def tree_hashes(root: str | os.PathLike[str]) -> dict[str, str]:
    root_path = Path(root).resolve()
    if not root_path.is_dir():
        raise MineJammerError(f"directory does not exist: {root_path}")
    result: dict[str, str] = {}
    for path in sorted(iter_files(root_path), key=lambda item: item.relative_to(root_path).as_posix()):
        relative = path.relative_to(root_path).as_posix()
        result[relative] = sha512_file(path)
    return result


def deterministic_zip(source: Path, output: Path, extra: dict[str, bytes] | None = None) -> None:
    source = source.resolve()
    entries: dict[str, bytes] = {}
    for path in iter_files(source):
        entries[path.relative_to(source).as_posix()] = path.read_bytes()
    entries.update(extra or {})
    deterministic_zip_entries(entries, output)


def deterministic_zip_entries(entries: dict[str, bytes], output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    fd, temp_name = tempfile.mkstemp(prefix=f".{output.name}.", suffix=".tmp", dir=output.parent)
    os.close(fd)
    temporary = Path(temp_name)
    try:
        with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for name in sorted(entries):
                info = zipfile.ZipInfo(normalized_member(name), date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                archive.writestr(info, entries[name])
        os.replace(temporary, output)
    finally:
        temporary.unlink(missing_ok=True)


def copy_tree_transactional(source: Path, destination: Path) -> None:
    source = source.resolve(strict=True)
    destination = destination.resolve(strict=False)
    if destination.exists():
        raise MineJammerError(f"destination already exists: {destination}")
    if source == destination or is_within(destination, source) or is_within(source, destination):
        raise MineJammerError("source and destination must be disjoint directory trees")
    destination.parent.mkdir(parents=True, exist_ok=True)
    stage = destination.parent / f".{destination.name}.staging-{os.getpid()}"
    if stage.exists():
        raise MineJammerError(f"stale clone staging directory exists: {stage}")
    try:
        shutil.copytree(source, stage, symlinks=False)
        if tree_hashes(source) != tree_hashes(stage):
            raise MineJammerError("clone verification failed")
        os.replace(stage, destination)
    finally:
        if stage.exists():
            shutil.rmtree(stage)
