from __future__ import annotations

import base64
import hashlib
import hmac
import re
from datetime import datetime, timedelta, timezone
from pathlib import Path, PurePosixPath
from typing import Any

from .util import MineJammerError, canonical_json, copy_tree_transactional, dump_json, is_within, load_json, sha512_file, tree_hashes


REGION_PATTERN = re.compile(r"^r\.(-?\d+)\.(-?\d+)\.mca$")
REGION_KEY_PATTERN = re.compile(r"^-?\d+,-?\d+$")
SHA512_PATTERN = re.compile(r"^[0-9a-f]{128}$")
PLAN_ID_PATTERN = re.compile(r"^[0-9a-f]{64}$")
TOKEN_ID_PATTERN = re.compile(r"^[A-Za-z0-9_-]{16,128}$")
MAX_OFFLINE_TOKEN_LIFETIME = timedelta(minutes=5)


def _tree_digest(hashes: dict[str, str]) -> str:
    return hashlib.sha512(canonical_json(hashes)).hexdigest()


def clone_world(source: str | Path, destination: str | Path, apply: bool = False,
                receipt_path: str | Path | None = None) -> dict[str, Any]:
    source_path = Path(source).resolve(strict=True)
    destination_path = Path(destination).resolve(strict=False)
    if not source_path.is_dir():
        raise MineJammerError(f"source world is not a directory: {source_path}")
    if destination_path.exists():
        raise MineJammerError(f"clone destination already exists: {destination_path}")
    if source_path == destination_path or is_within(destination_path, source_path) or is_within(source_path, destination_path):
        raise MineJammerError("production and MineJammer worlds must be disjoint")
    hashes = tree_hashes(source_path)
    result = {
        "schema": "minescape.world-clone.v1", "dry_run": not apply,
        "source": str(source_path), "destination": str(destination_path),
        "file_count": len(hashes), "bytes": sum((source_path / name).stat().st_size for name in hashes),
        "source_tree_digest": _tree_digest(hashes),
    }
    if apply:
        copy_tree_transactional(source_path, destination_path)
        destination_hashes = tree_hashes(destination_path)
        if destination_hashes != hashes:
            raise MineJammerError("post-rename clone verification failed")
        result["destination_tree_digest"] = _tree_digest(destination_hashes)
    if receipt_path:
        dump_json(receipt_path, result)
    return result


def _canonical_region_path(relative: Any) -> str:
    if not isinstance(relative, str) or not relative or "\\" in relative or relative.startswith("/"):
        raise MineJammerError(f"parcel path is not canonical: {relative!r}")
    pure = PurePosixPath(relative)
    if pure.is_absolute() or "." in pure.parts or ".." in pure.parts or any(":" in part for part in pure.parts):
        raise MineJammerError(f"parcel path is unsafe: {relative!r}")
    canonical = pure.as_posix()
    if canonical != relative:
        raise MineJammerError(f"parcel path is not normalized: {relative!r}")
    return canonical


def _region_identity(relative: str) -> tuple[str, str, int, int] | None:
    parts = PurePosixPath(relative).parts
    dimension = "overworld"
    if parts[:1] == ("DIM-1",):
        dimension, parts = "nether", parts[1:]
    elif parts[:1] == ("DIM1",):
        dimension, parts = "end", parts[1:]
    if len(parts) != 2 or parts[0] not in {"region", "entities", "poi"}:
        return None
    match = REGION_PATTERN.fullmatch(parts[1])
    if not match:
        return None
    return dimension, parts[0], int(match.group(1)), int(match.group(2))


def _validate_bounds(bounds: Any) -> None:
    if not isinstance(bounds, dict) or not bounds:
        raise MineJammerError("parcel policy requires non-empty dimension bounds")
    if not set(bounds) <= {"overworld", "nether", "end"}:
        raise MineJammerError("parcel bounds contain an unknown dimension")
    for dimension, values in bounds.items():
        required = {"min_x", "max_x", "min_z", "max_z"}
        if not isinstance(values, dict) or set(values) != required:
            raise MineJammerError(f"parcel bounds for {dimension} require exactly {sorted(required)}")
        if any(not isinstance(values[key], int) or isinstance(values[key], bool) for key in required):
            raise MineJammerError(f"parcel bounds must use integer coordinates: {dimension}")
        if any(values[key] % 512 for key in required):
            raise MineJammerError(f"parcel bounds must align to 512-block region edges: {dimension}")
        if values["min_x"] >= values["max_x"] or values["min_z"] >= values["max_z"]:
            raise MineJammerError(f"invalid parcel bounds: {dimension}")


def _inside_region(bounds: dict[str, int], rx: int, rz: int) -> bool:
    min_x, max_x = rx * 512, (rx + 1) * 512
    min_z, max_z = rz * 512, (rz + 1) * 512
    return min_x >= bounds["min_x"] and max_x <= bounds["max_x"] and min_z >= bounds["min_z"] and max_z <= bounds["max_z"]


def _load_exploration(path: Path) -> dict[str, set[str]]:
    exploration = load_json(path)
    if not isinstance(exploration, dict) or exploration.get("schema") != "minescape.explored-regions.v1":
        raise MineJammerError("unsupported exploration manifest")
    regions = exploration.get("regions")
    if not isinstance(regions, dict) or not set(regions) <= {"overworld", "nether", "end"}:
        raise MineJammerError("exploration manifest has invalid dimensions")
    result: dict[str, set[str]] = {}
    for dimension, values in regions.items():
        if (not isinstance(values, list) or any(not isinstance(value, str) or
                                               REGION_KEY_PATTERN.fullmatch(value) is None for value in values)):
            raise MineJammerError(f"exploration manifest has invalid region keys for {dimension}")
        if len(set(values)) != len(values):
            raise MineJammerError(f"exploration manifest has duplicate regions for {dimension}")
        result[dimension] = set(values)
    return result


def _validate_backup_receipt(receipt_path: Path, production: Path, production_digest: str) -> dict[str, Any]:
    receipt = load_json(receipt_path)
    if (not isinstance(receipt, dict) or receipt.get("schema") != "minescape.world-clone.v1" or
            receipt.get("dry_run") is not False):
        raise MineJammerError("backup receipt must be an applied MineJammer world-clone receipt")
    try:
        source = Path(receipt["source"]).resolve(strict=True)
        backup = Path(receipt["destination"]).resolve(strict=True)
    except (KeyError, OSError, TypeError) as exc:
        raise MineJammerError("backup receipt names missing source/destination trees") from exc
    if source != production or not backup.is_dir() or source == backup or is_within(source, backup) or is_within(backup, source):
        raise MineJammerError("backup receipt does not name disjoint production and backup trees")
    if receipt.get("source_tree_digest") != production_digest or receipt.get("destination_tree_digest") != production_digest:
        raise MineJammerError("backup receipt does not match the current production tree")
    if _tree_digest(tree_hashes(backup)) != production_digest:
        raise MineJammerError("the complete backup diverged from its receipt")
    return receipt


def _derive_changes(before: dict[str, str], after: dict[str, str], staging: Path,
                    bounds: dict[str, Any], explored: dict[str, set[str]]) -> list[dict[str, Any]]:
    changes: list[dict[str, Any]] = []
    for relative_value in sorted(set(before) | set(after)):
        if before.get(relative_value) == after.get(relative_value):
            continue
        relative = _canonical_region_path(relative_value)
        if relative not in after:
            raise MineJammerError(f"parcel deletion is forbidden: {relative}")
        identity = _region_identity(relative)
        if identity is None:
            raise MineJammerError(f"global or non-region state changed: {relative}")
        dimension, store, rx, rz = identity
        if dimension not in bounds or not _inside_region(bounds[dimension], rx, rz):
            raise MineJammerError(f"changed region is outside parcel bounds: {relative}")
        region_key = f"{rx},{rz}"
        if region_key in explored.get(dimension, set()):
            raise MineJammerError(f"changed region has been explored: {dimension} {region_key}")
        source = (staging / relative).resolve(strict=True)
        if not is_within(source, staging) or not source.is_file():
            raise MineJammerError(f"staged parcel path is not a regular file: {relative}")
        changes.append({
            "path": relative, "dimension": dimension, "store": store, "region": region_key,
            "before_sha512": before.get(relative), "after_sha512": after[relative],
            "after_bytes": source.stat().st_size,
        })
    if not changes:
        raise MineJammerError("parcel contains no changes")
    return changes


def _plan_id(plan: dict[str, Any]) -> str:
    unsigned = dict(plan)
    unsigned.pop("plan_id", None)
    return hashlib.sha256(canonical_json(unsigned)).hexdigest()


def parcel_plan(production: str | Path, staging: str | Path, policy_path: str | Path,
                exploration_path: str | Path, backup_receipt_path: str | Path, output: str | Path) -> dict[str, Any]:
    production_path = Path(production).resolve(strict=True)
    staging_path = Path(staging).resolve(strict=True)
    if (not production_path.is_dir() or not staging_path.is_dir() or production_path == staging_path or
            is_within(production_path, staging_path) or is_within(staging_path, production_path)):
        raise MineJammerError("production and staging world trees must be disjoint directories")
    policy_file = Path(policy_path).resolve(strict=True)
    policy = load_json(policy_file)
    if not isinstance(policy, dict) or policy.get("schema") != "minescape.parcel-policy.v1":
        raise MineJammerError("unsupported parcel policy")
    bounds = policy.get("bounds")
    _validate_bounds(bounds)
    exploration_file = Path(exploration_path).resolve(strict=True)
    explored = _load_exploration(exploration_file)
    before = tree_hashes(production_path)
    after = tree_hashes(staging_path)
    production_digest = _tree_digest(before)
    backup_file = Path(backup_receipt_path).resolve(strict=True)
    _validate_backup_receipt(backup_file, production_path, production_digest)
    changes = _derive_changes(before, after, staging_path, bounds, explored)
    plan = {
        "schema": "minescape.parcel-plan.v2",
        "production": str(production_path),
        "staging": str(staging_path),
        "production_tree_digest": production_digest,
        "policy": str(policy_file),
        "policy_sha512": sha512_file(policy_file),
        "backup_receipt": str(backup_file),
        "backup_receipt_sha512": sha512_file(backup_file),
        "bounds": bounds,
        "exploration_manifest": str(exploration_file),
        "exploration_manifest_sha512": sha512_file(exploration_file),
        "changes": changes,
        "guard": "planning only; --apply remains unavailable until Bridge token signatures and one-time consumption are implemented",
    }
    plan["plan_id"] = _plan_id(plan)
    dump_json(output, plan)
    return plan


def _parse_token_time(value: Any, field: str) -> datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        raise MineJammerError(f"offline token {field} must be an RFC 3339 UTC timestamp")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        raise MineJammerError(f"offline token {field} is invalid") from exc
    if parsed.utcoffset() != timedelta(0):
        raise MineJammerError(f"offline token {field} must be UTC")
    return parsed


def _validate_token_contract(token: Any, production: Path, plan: dict[str, Any], now: datetime) -> dict[str, Any]:
    if not isinstance(token, dict) or token.get("schema") != "minescape.bridge-offline-token.v1":
        raise MineJammerError("a Bridge offline-token v1 contract is required")
    if token.get("issuer") != "minescape-bridge" or token.get("purpose") != "parcel-commit":
        raise MineJammerError("offline token was not issued by Bridge for parcel commit")
    if token.get("server_offline") is not True or token.get("one_time") is not True:
        raise MineJammerError("offline token must attest offline state and one-time use")
    if Path(token.get("world", "")).resolve(strict=False) != production:
        raise MineJammerError("offline token names a different world")
    if token.get("plan_id") != plan["plan_id"] or token.get("production_tree_digest") != plan["production_tree_digest"]:
        raise MineJammerError("offline token is not bound to this exact parcel plan and world tree")
    token_id = token.get("token_id")
    if not isinstance(token_id, str) or TOKEN_ID_PATTERN.fullmatch(token_id) is None:
        raise MineJammerError("offline token has no valid one-time token_id")
    issued = _parse_token_time(token.get("issued_at"), "issued_at")
    expires = _parse_token_time(token.get("expires_at"), "expires_at")
    if expires <= issued or expires - issued > MAX_OFFLINE_TOKEN_LIFETIME:
        raise MineJammerError("offline token lifetime must be positive and no more than five minutes")
    if issued > now + timedelta(seconds=30) or expires <= now:
        raise MineJammerError("offline token is not currently valid")
    signature = token.get("signature")
    if (not isinstance(signature, dict) or signature.get("algorithm") != "Ed25519" or
            not isinstance(signature.get("key_id"), str) or not signature["key_id"].strip() or
            not isinstance(signature.get("value"), str)):
        raise MineJammerError("offline token lacks the required Bridge Ed25519 signature contract")
    try:
        decoded = base64.b64decode(signature["value"], validate=True)
    except (ValueError, TypeError) as exc:
        raise MineJammerError("offline token signature is not valid base64") from exc
    if len(decoded) != 64:
        raise MineJammerError("offline token Ed25519 signature must be 64 bytes")
    return {
        "token_id": token_id,
        "issuer": token["issuer"],
        "issued_at": token["issued_at"],
        "expires_at": token["expires_at"],
        "signature_present": True,
        "signature_verified": False,
        "one_time_consumed": False,
    }


def _revalidate_plan(plan: Any) -> tuple[Path, Path, list[dict[str, Any]]]:
    if not isinstance(plan, dict) or plan.get("schema") != "minescape.parcel-plan.v2":
        raise MineJammerError("unsupported parcel plan; regenerate it with this MineJammer version")
    supplied_plan_id = plan.get("plan_id")
    recomputed_plan_id = _plan_id(plan)
    if (not isinstance(supplied_plan_id, str) or PLAN_ID_PATTERN.fullmatch(supplied_plan_id) is None or
            not hmac.compare_digest(supplied_plan_id, recomputed_plan_id)):
        raise MineJammerError("parcel plan integrity check failed")
    production = Path(plan.get("production", "")).resolve(strict=True)
    staging = Path(plan.get("staging", "")).resolve(strict=True)
    if (not production.is_dir() or not staging.is_dir() or production == staging or
            is_within(production, staging) or is_within(staging, production)):
        raise MineJammerError("parcel plan world trees are missing or not disjoint")
    bounds = plan.get("bounds")
    _validate_bounds(bounds)

    policy_file = Path(plan.get("policy", "")).resolve(strict=True)
    if sha512_file(policy_file) != plan.get("policy_sha512"):
        raise MineJammerError("parcel policy changed after planning")
    policy = load_json(policy_file)
    if (not isinstance(policy, dict) or policy.get("schema") != "minescape.parcel-policy.v1" or
            policy.get("bounds") != bounds):
        raise MineJammerError("parcel plan bounds no longer match its policy")

    exploration_file = Path(plan.get("exploration_manifest", "")).resolve(strict=True)
    if sha512_file(exploration_file) != plan.get("exploration_manifest_sha512"):
        raise MineJammerError("exploration manifest changed after planning; regenerate the plan")
    explored = _load_exploration(exploration_file)

    current = tree_hashes(production)
    production_digest = _tree_digest(current)
    if production_digest != plan.get("production_tree_digest"):
        raise MineJammerError("production diverged after parcel planning")
    backup_file = Path(plan.get("backup_receipt", "")).resolve(strict=True)
    if sha512_file(backup_file) != plan.get("backup_receipt_sha512"):
        raise MineJammerError("backup receipt changed after planning")
    _validate_backup_receipt(backup_file, production, production_digest)

    staged = tree_hashes(staging)
    derived_changes = _derive_changes(current, staged, staging, bounds, explored)
    if plan.get("changes") != derived_changes:
        raise MineJammerError("parcel plan changes do not exactly match the re-derived path/bounds/region set")
    for change in derived_changes:
        identity = _region_identity(_canonical_region_path(change["path"]))
        assert identity is not None  # guaranteed by _derive_changes
        dimension, store, rx, rz = identity
        if (change["dimension"], change["store"], change["region"]) != (dimension, store, f"{rx},{rz}"):
            raise MineJammerError(f"parcel path/bound/region identity mismatch: {change['path']}")
    return production, staging, derived_changes


def commit_parcel(plan_path: str | Path, offline_token_path: str | Path, journal_root: str | Path,
                  apply: bool = False) -> dict[str, Any]:
    plan = load_json(plan_path)
    production, _staging, changes = _revalidate_plan(plan)
    token = load_json(offline_token_path)
    authorization = _validate_token_contract(token, production, plan, datetime.now(timezone.utc))
    result = {
        "schema": "minescape.parcel-commit-readiness.v2",
        "plan_id": plan["plan_id"],
        "dry_run": True,
        "changes": len(changes),
        "journal_root": str(Path(journal_root).resolve(strict=False)),
        "authorization": authorization,
        "apply_available": False,
        "release_gate": "Bridge public-key verification and atomic one-time token consumption are not implemented",
    }
    if apply:
        raise MineJammerError(
            "parcel --apply is unavailable: Bridge signature verification and atomic one-time token consumption "
            "must be implemented and tested before production writes are enabled"
        )
    return result


def recover_parcel(journal: str | Path, apply: bool = False) -> dict[str, Any]:
    journal_path = Path(journal).resolve(strict=True)
    plan = load_json(journal_path / "plan.json")
    state = load_json(journal_path / "state.json")
    if state.get("state") in {"committed", "rolled_back"}:
        return {"schema": "minescape.parcel-recovery.v1", "needed": False, "state": state["state"]}
    result = {
        "schema": "minescape.parcel-recovery.v1", "needed": True, "dry_run": True,
        "restore_count": len(plan["changes"]), "apply_available": False,
        "release_gate": "authenticated transaction journals are not implemented",
    }
    if apply:
        raise MineJammerError(
            "parcel recovery --apply is unavailable until MineJammer can authenticate journals created by the "
            "future authorized commit implementation"
        )
    return result
