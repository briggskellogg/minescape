from __future__ import annotations

import json
import os
import tempfile
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

from .archives import audit_zip
from .util import MineJammerError, dump_json, load_json, sha512_file


API_ROOT = "https://api.modrinth.com/v2"
USER_AGENT = "MineJammer/0.1 (private family-server preservation tooling)"


def validate_source_manifest(document: dict[str, Any]) -> None:
    if document.get("schema") != "minescape.artifacts.source.v1":
        raise MineJammerError("unsupported source manifest schema")
    if document.get("minecraft") != "26.2":
        raise MineJammerError("canonical source manifest must target Minecraft 26.2")
    if document.get("java") != "Eclipse Temurin 25.0.3+9":
        raise MineJammerError("canonical source manifest must pin Eclipse Temurin 25.0.3+9")
    if document.get("toolchain") != {"dotnet_sdk": "10.0.302", "python": "3.13.14", "gradle": "9.5.1"}:
        raise MineJammerError("canonical toolchain pins changed")
    keys: set[str] = set()
    ids: set[str] = set()
    for artifact in document.get("artifacts", []):
        required = {"key", "name", "provider", "version", "version_id", "project", "creator", "status", "profiles"}
        missing = sorted(required - artifact.keys())
        if missing:
            raise MineJammerError(f"artifact is missing fields {missing}: {artifact.get('key', '<unknown>')}")
        if artifact["provider"] != "modrinth":
            raise MineJammerError(f"unsupported artifact provider for {artifact['key']}")
        if artifact["key"] in keys or artifact["version_id"] in ids:
            raise MineJammerError(f"duplicate key/version id: {artifact['key']}")
        keys.add(artifact["key"])
        ids.add(artifact["version_id"])


def _get_json(url: str) -> dict[str, Any]:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT, "Accept": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)
    except (urllib.error.URLError, json.JSONDecodeError) as error:
        raise MineJammerError(f"metadata request failed for {url}: {error}") from error


def _download(url: str, destination: Path, expected_sha512: str) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and sha512_file(destination) == expected_sha512:
        return
    fd, temp_name = tempfile.mkstemp(prefix=f".{destination.name}.", suffix=".download", dir=destination.parent)
    temporary = Path(temp_name)
    try:
        request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        with os.fdopen(fd, "wb") as output, urllib.request.urlopen(request, timeout=120) as response:
            while block := response.read(1024 * 1024):
                output.write(block)
            output.flush()
            os.fsync(output.fileno())
        actual = sha512_file(temporary)
        if actual != expected_sha512:
            raise MineJammerError(f"SHA-512 mismatch for {destination.name}: expected {expected_sha512}, got {actual}")
        os.replace(temporary, destination)
    finally:
        temporary.unlink(missing_ok=True)


def acquire(manifest_path: str | Path, cache: str | Path, lock_path: str | Path, report_path: str | Path,
            metadata_only: bool = False) -> dict[str, Any]:
    source = load_json(manifest_path)
    validate_source_manifest(source)
    cache_root = Path(cache).resolve()
    locked: list[dict[str, Any]] = []
    report_entries: list[dict[str, Any]] = []
    for item in source["artifacts"]:
        version_url = f"{API_ROOT}/version/{item['version_id']}"
        version = _get_json(version_url)
        if version.get("id") != item["version_id"]:
            raise MineJammerError(f"pinned version drift for {item['key']}")
        project = _get_json(f"{API_ROOT}/project/{version['project_id']}")
        if item["project"] not in {project.get("slug"), project.get("id")}:
            raise MineJammerError(f"project identity mismatch for {item['key']}: {project.get('slug')}")
        files = version.get("files", [])
        primary = next((entry for entry in files if entry.get("primary")), files[0] if len(files) == 1 else None)
        if primary is None:
            raise MineJammerError(f"no unambiguous primary file for {item['key']}")
        sha512 = primary.get("hashes", {}).get("sha512")
        if not sha512 or len(sha512) != 128:
            raise MineJammerError(f"Modrinth supplied no SHA-512 for {item['key']}")
        if item.get("known_sha512") and item["known_sha512"] != sha512:
            raise MineJammerError(f"published SHA-512 changed for pinned artifact {item['key']}")
        destination = cache_root / item["key"] / primary["filename"]
        if not metadata_only:
            _download(primary["url"], destination, sha512)
        local_ok = destination.exists() and sha512_file(destination) == sha512
        zip_result = audit_zip(destination) if local_ok and destination.suffix.lower() in {".zip", ".jar"} else None
        if zip_result and not zip_result["ok"]:
            raise MineJammerError(f"unsafe archive for {item['key']}: {zip_result['issues']}")
        locked_item = {
            **item,
            "api_version_number": version.get("version_number"),
            "project_id": version["project_id"],
            "filename": primary["filename"],
            "size": primary["size"],
            "sha512": sha512,
            "download_url": primary["url"],
            "license": project.get("license"),
            "provenance": {
                "version_api": version_url,
                "project_api": f"{API_ROOT}/project/{version['project_id']}",
                "published": version.get("date_published"),
                "loaders": version.get("loaders", []),
                "game_versions": version.get("game_versions", []),
            },
        }
        locked.append(locked_item)
        report_entries.append({
            "key": item["key"], "metadata_resolved": True, "downloaded": destination.exists(),
            "sha512_verified": local_ok, "archive_safe": None if zip_result is None else zip_result["ok"],
            "cache_relative": destination.relative_to(cache_root).as_posix(),
        })
    lock = {
        "schema": "minescape.artifacts.lock.v1",
        "minecraft": source["minecraft"],
        "java": source["java"],
        "toolchain": source["toolchain"],
        "fabric_loader": source["fabric_loader"],
        "fabric_launcher": source["fabric_launcher"],
        "artifacts": locked,
    }
    report = {
        "schema": "minescape.acquisition-report.v1",
        "metadata_only": metadata_only,
        "complete": all(entry["metadata_resolved"] and (metadata_only or entry["sha512_verified"]) for entry in report_entries),
        "entries": report_entries,
    }
    dump_json(lock_path, lock)
    dump_json(report_path, report)
    return report


def verify_lock(lock_path: str | Path, cache: str | Path) -> dict[str, Any]:
    lock = load_json(lock_path)
    if lock.get("schema") != "minescape.artifacts.lock.v1":
        raise MineJammerError("unsupported artifact lock schema")
    root = Path(cache).resolve()
    results = []
    for item in lock.get("artifacts", []):
        path = root / item["key"] / item["filename"]
        actual = sha512_file(path) if path.is_file() else None
        results.append({"key": item["key"], "exists": path.is_file(), "ok": actual == item["sha512"], "actual_sha512": actual})
    return {"schema": "minescape.lock-verification.v1", "ok": all(item["ok"] for item in results), "entries": results}
