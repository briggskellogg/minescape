from __future__ import annotations

import hashlib
import re
from datetime import datetime, timedelta, timezone
from pathlib import Path, PurePosixPath
from typing import Any, Callable

from .artifacts import verify_lock
from .util import MineJammerError, dump_json, is_within, load_json, sha512_file


SHA512_PATTERN = re.compile(r"^[0-9a-f]{128}$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
MATCHA_MECHANICAL_SOURCE_SHA256 = {
    "data/main/function/setup/scoreboard.mcfunction":
        "b32f8b624aac5cc05579011f59bea6ae23d236441cd512f585240b2c1fc30ecd",
    "data/main/function/mechanic/hpdown.mcfunction":
        "e77b0ab25ba89a0053edf02f8cdb25307ff1a31b6c6dfbfe8ccbe2794a75f1c1",
    "data/main/function/mechanic/process_heart_container.mcfunction":
        "5c7b0cb2649f7be0bed7636146fae61b5593842ece5ad9beea15042035cf113e",
    "data/main/function/mechanic/clear_heart_container.mcfunction":
        "da91f3b1695f36464ce84bcfb2127f144ec45c87464ea76a488a941db14d9c7d",
    "data/main/function/mechanic/set_max_hp.mcfunction":
        "5e8d8fffbedb8be581b035ed4cf0b96baed81e4b8f23a3c9c9717aa3e3f58081",
}
MATCHA_MECHANICAL_ACTIONS = {
    "data/main/function/setup/scoreboard.mcfunction": "remove_matcha_heart_floor",
    "data/main/function/mechanic/hpdown.mcfunction": "reset_death_score_only",
    "data/main/function/mechanic/process_heart_container.mcfunction": "replace_with_inert_comment",
    "data/main/function/mechanic/clear_heart_container.mcfunction": "replace_with_inert_comment",
    "data/main/function/mechanic/set_max_hp.mcfunction": "replace_with_inert_comment",
}

# These definitions are code-owned on purpose.  A release policy may choose the
# evidence filename, but it cannot turn an unknown schema or a weaker evaluator
# into a release gate merely by declaring it in JSON.
GATE_SPECS: dict[str, tuple[str, str]] = {
    "acquisition": ("acquisition", "minescape.acquisition-report.v1"),
    "worldgen-collisions": ("compatibility", "minescape.compatibility-build.v1"),
    "village-adapter": ("village", "minescape.village-adapter-build.v1"),
    "loot-and-food": ("manual", "minescape.loot-food-evidence.v1"),
    "fishing": ("fishing", "minescape.fishing-adapter-build.v1"),
    "seed-and-finite-resources": ("seed-audit", "minescape.seed-evidence.v1"),
    "controller-complete-matcha": ("manual", "minescape.controller-evidence.v1"),
    "mortal-hearts": ("manual", "minescape.mortal-hearts-evidence.v1"),
    "worldgen-smoke": ("manual", "minescape.worldgen-smoke-evidence.v1"),
    "fog-privacy": ("manual", "minescape.fog-privacy-evidence.v1"),
    "backup-restore": ("manual", "minescape.backup-restore-evidence.v1"),
    "launcher-and-status": ("manual", "minescape.operations-evidence.v1"),
}

# Manual evidence is deliberately an operator attestation: its metadata proves who tested which
# locked build and when, but it cannot prove that unfinished code exists.  These code-owned IDs
# form a separate promotion interlock.  Removing, renaming, or omitting one from policy fails
# closed; each flag may become true only in the same reviewed change that completes its adapter.
REQUIRED_ADAPTER_COMPLETION = frozenset({
    "mortal-hearts-and-retirement",
    "native-whitelist-and-roles",
    "structure-loot-and-food",
    "civic-wards",
    "exploration-and-fog-privacy",
    "family-assist",
    "steward-leases",
    "quiesced-backup-and-restore",
    "signed-atomic-parcel-promotion",
    "managed-clients-and-resource-pack",
    "clean-shutdown-and-reboot-recovery",
})


def _parse_utc_timestamp(value: Any, field: str) -> datetime:
    if not isinstance(value, str) or not value.endswith("Z"):
        raise MineJammerError(f"{field} must be an RFC 3339 UTC timestamp ending in Z")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        raise MineJammerError(f"{field} is not a valid RFC 3339 timestamp") from exc
    if parsed.tzinfo is None or parsed.utcoffset() != timedelta(0):
        raise MineJammerError(f"{field} must be UTC")
    return parsed


def _is_sha512(value: Any) -> bool:
    return isinstance(value, str) and SHA512_PATTERN.fullmatch(value) is not None


def _hash_file(path: Path, algorithm: str) -> str:
    digest = hashlib.new(algorithm)
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _validated_minecraft_server_lock(lock_path: str | Path, cache: str | Path,
                                      expected_version: str) -> dict[str, Any]:
    lock_file = Path(lock_path).resolve(strict=True)
    document = load_json(lock_file)
    if not isinstance(document, dict) or document.get("schema") != "minescape.minecraft-server-lock.v1":
        raise MineJammerError("unsupported Minecraft server lock schema")
    if document.get("version") != expected_version or document.get("type") != "release":
        raise MineJammerError("Minecraft server lock disagrees with the release artifact lock")
    if (not isinstance(document.get("size"), int) or isinstance(document.get("size"), bool) or
            document["size"] <= 0 or not isinstance(document.get("sha1"), str) or
            re.fullmatch(r"[0-9a-f]{40}", document["sha1"]) is None or
            not _is_sha512(document.get("sha512")) or
            not isinstance(document.get("bundled_server_sha256"), str) or
            SHA256_PATTERN.fullmatch(document["bundled_server_sha256"]) is None or
            not _is_sha512(document.get("bundled_server_sha512"))):
        raise MineJammerError("Minecraft server lock has invalid official or inner-server hashes")

    cache_root = Path(cache).resolve(strict=True)
    repository_root = lock_file.parent.parent.resolve(strict=True)

    def resolve_locked(field: str) -> Path:
        value = document.get(field)
        if not isinstance(value, str):
            raise MineJammerError(f"Minecraft server lock is missing {field}")
        pure = PurePosixPath(value)
        if pure.is_absolute() or ".." in pure.parts or pure.as_posix() != value:
            raise MineJammerError(f"Minecraft server lock has an unsafe {field}")
        candidate = (repository_root / Path(*pure.parts)).resolve(strict=False)
        if not is_within(candidate, cache_root) or not candidate.is_file():
            raise MineJammerError(f"Minecraft server lock cache file is absent or escapes cache: {field}")
        return candidate

    outer = resolve_locked("cache_relative")
    inner = resolve_locked("bundled_server_relative")
    if (outer.stat().st_size != document["size"] or _hash_file(outer, "sha1") != document["sha1"] or
            sha512_file(outer) != document["sha512"]):
        raise MineJammerError("official Minecraft server archive differs from its lock")
    if (_hash_file(inner, "sha256") != document["bundled_server_sha256"] or
            sha512_file(inner) != document["bundled_server_sha512"]):
        raise MineJammerError("bundled vanilla server archive differs from its lock")
    return {
        "lock_sha512": sha512_file(lock_file),
        "version": document["version"],
        "bundled_server_sha512": document["bundled_server_sha512"],
    }


def _lock_hashes(lock: dict[str, Any]) -> dict[str, str]:
    hashes: dict[str, str] = {}
    for item in lock.get("artifacts", []):
        key, digest = item.get("key"), item.get("sha512")
        if not isinstance(key, str) or not key or key in hashes or not _is_sha512(digest):
            raise MineJammerError("artifact lock contains an invalid or duplicate key/hash")
        hashes[key] = digest
    return hashes


def _require_locked_inputs(inputs: Any, lock_sha512: str) -> dict[str, str]:
    if not isinstance(inputs, dict):
        raise MineJammerError("manual evidence requires an input_hashes object")
    required = {"artifact_lock_sha512", "server_bundle_sha512"}
    missing = required - set(inputs)
    if missing:
        raise MineJammerError(f"manual evidence is missing locked inputs: {sorted(missing)}")
    for name, digest in inputs.items():
        if not isinstance(name, str) or not name or not _is_sha512(digest):
            raise MineJammerError(f"manual evidence input hash is not SHA-512: {name!r}")
    if inputs["artifact_lock_sha512"] != lock_sha512:
        raise MineJammerError("manual evidence was recorded against a different artifact lock")
    return inputs


def _validate_manual_metadata(evidence: dict[str, Any], gate_id: str, expected_schema: str,
                              world_epoch: str, lock_sha512: str, max_age_hours: int,
                              now: datetime) -> dict[str, Any]:
    if evidence.get("schema") != expected_schema or evidence.get("gate") != gate_id:
        raise MineJammerError(f"{gate_id} evidence has an unrecognized gate/schema pairing")
    if evidence.get("passed") is not True:
        raise MineJammerError(f"{gate_id} evidence does not record a passing result")
    if evidence.get("world_epoch") != world_epoch:
        raise MineJammerError(f"{gate_id} evidence belongs to a different world epoch")
    tester = evidence.get("tester")
    if not isinstance(tester, dict) or not isinstance(tester.get("name"), str) or not tester["name"].strip():
        raise MineJammerError(f"{gate_id} manual evidence requires a named tester")
    if not isinstance(evidence.get("machine"), str) or not evidence["machine"].strip():
        raise MineJammerError(f"{gate_id} manual evidence requires the tested machine")
    observations = evidence.get("observations")
    if (not isinstance(observations, list) or not observations or
            any(not isinstance(item, str) or not item.strip() for item in observations)):
        raise MineJammerError(f"{gate_id} manual evidence requires non-empty observations")
    started = _parse_utc_timestamp(evidence.get("started_at"), "started_at")
    completed = _parse_utc_timestamp(evidence.get("completed_at"), "completed_at")
    if started > completed:
        raise MineJammerError(f"{gate_id} evidence completed before it started")
    if completed > now + timedelta(minutes=5):
        raise MineJammerError(f"{gate_id} evidence completion time is in the future")
    if completed < now - timedelta(hours=max_age_hours):
        raise MineJammerError(f"{gate_id} manual evidence is stale")
    inputs = _require_locked_inputs(evidence.get("input_hashes"), lock_sha512)
    return {
        "tester": tester["name"].strip(),
        "machine": evidence["machine"].strip(),
        "started_at": evidence["started_at"],
        "completed_at": evidence["completed_at"],
        "world_epoch": world_epoch,
        "input_hash_count": len(inputs),
        "observation_count": len(observations),
    }


def audit_seed(evidence_path: str | Path, policy_path: str | Path,
               output: str | Path | None = None) -> dict[str, Any]:
    evidence = load_json(evidence_path)
    policy = load_json(policy_path)
    if (not isinstance(evidence, dict) or evidence.get("schema") != "minescape.seed-evidence.v1" or
            not isinstance(policy, dict) or policy.get("schema") != "minescape.seed-policy.v1"):
        raise MineJammerError("unsupported seed policy/evidence schema")
    if not isinstance(policy.get("boundaries"), dict) or not isinstance(policy.get("required_structures"), list):
        raise MineJammerError("seed policy is malformed")
    checks = []

    def check(name: str, passed: bool, detail: Any) -> None:
        checks.append({"gate": name, "passed": bool(passed), "detail": detail})

    check("preferred_seed", str(evidence.get("seed")) == str(policy.get("seed")), evidence.get("seed"))
    check("boundary", evidence.get("boundaries") == policy["boundaries"], evidence.get("boundaries"))
    counts = evidence.get("structure_counts")
    if not isinstance(counts, dict):
        counts = {}
    for structure in policy["required_structures"]:
        value = counts.get(structure, 0)
        try:
            passed = not isinstance(value, bool) and int(value) >= 1
        except (TypeError, ValueError):
            passed = False
        check(f"structure:{structure}", passed, value)
    starter = evidence.get("starter_village")
    if not isinstance(starter, dict):
        starter = {}
    check("starter_village", starter.get("natural") is True, starter)
    check("public_arrival", starter.get("custom_homes") is False and starter.get("public_arrival") is True, starter)
    far_rim = evidence.get("far_rim")
    finite_resources = evidence.get("finite_resources")
    check("far_rim", isinstance(far_rim, dict) and far_rim.get("accepted") is True, far_rim)
    check("finite_resources", isinstance(finite_resources, dict) and finite_resources.get("complete") is True,
          finite_resources)
    result = {"schema": "minescape.seed-audit.v1", "passed": all(item["passed"] for item in checks), "checks": checks}
    if output:
        dump_json(output, result)
    return result


def _locked_input_matches(report: dict[str, Any], field: str, artifact: str,
                          lock_hashes: dict[str, str]) -> None:
    if artifact not in lock_hashes:
        raise MineJammerError(f"artifact lock does not contain required input {artifact}")
    if not isinstance(report.get("inputs"), dict) or report["inputs"].get(field) != lock_hashes[artifact]:
        raise MineJammerError(f"automated evidence input {field} is not locked to {artifact}")


def _validate_acquisition(report: dict[str, Any], lock_hashes: dict[str, str]) -> dict[str, Any]:
    if report.get("complete") is not True or report.get("metadata_only") is not False:
        raise MineJammerError("acquisition evidence must prove a complete, non-metadata-only acquisition")
    entries = report.get("entries")
    if not isinstance(entries, list):
        raise MineJammerError("acquisition evidence entries are missing")
    by_key = {item.get("key"): item for item in entries if isinstance(item, dict) and isinstance(item.get("key"), str)}
    if len(by_key) != len(entries) or set(by_key) != set(lock_hashes):
        raise MineJammerError("acquisition evidence does not cover the exact artifact lock")
    for key, item in by_key.items():
        if not all(item.get(field) is True for field in ("metadata_resolved", "downloaded", "sha512_verified")):
            raise MineJammerError(f"acquisition evidence is incomplete for {key}")
        if item.get("archive_safe") is False:
            raise MineJammerError(f"acquisition evidence found an unsafe archive for {key}")
    return {"locked_artifacts": len(lock_hashes), "metadata_only": False}


def _validate_compatibility(report: dict[str, Any], lock_hashes: dict[str, str]) -> dict[str, Any]:
    for field, artifact in (("matcha_sha512", "matcha"), ("terralith_sha512", "terralith"),
                            ("nullscape_sha512", "nullscape")):
        _locked_input_matches(report, field, artifact, lock_hashes)
    if not _is_sha512(report.get("output_sha512")) or not isinstance(report.get("collision_counts"), dict):
        raise MineJammerError("compatibility build evidence is incomplete")
    if not isinstance(report.get("resources"), list):
        raise MineJammerError("compatibility build evidence has no resource provenance")
    overrides = report.get("mechanical_overrides")
    if not isinstance(overrides, list) or len(overrides) != len(MATCHA_MECHANICAL_SOURCE_SHA256):
        raise MineJammerError("compatibility build evidence is missing the five Matcha heart overrides")
    by_resource = {
        item.get("resource"): item for item in overrides
        if isinstance(item, dict) and isinstance(item.get("resource"), str)
    }
    if len(by_resource) != len(overrides) or set(by_resource) != set(MATCHA_MECHANICAL_SOURCE_SHA256):
        raise MineJammerError("compatibility build evidence has unknown or duplicate mechanical overrides")
    for resource, source_sha256 in MATCHA_MECHANICAL_SOURCE_SHA256.items():
        item = by_resource[resource]
        if (item.get("action") != MATCHA_MECHANICAL_ACTIONS[resource] or
                item.get("authority") != "FabricHeartLifecycle" or
                item.get("source_sha256") != source_sha256 or
                not isinstance(item.get("output_sha256"), str) or
                SHA256_PATTERN.fullmatch(item["output_sha256"]) is None):
            raise MineJammerError(f"compatibility mechanical override is not exact: {resource}")
    return {
        "output_sha512": report["output_sha512"], "collision_counts": report["collision_counts"],
        "mechanical_overrides": sorted(by_resource),
    }


def _validate_village(report: dict[str, Any], lock_hashes: dict[str, str],
                      vanilla_sha512: str) -> dict[str, Any]:
    _locked_input_matches(report, "matcha_sha512", "matcha", lock_hashes)
    if not isinstance(report.get("inputs"), dict) or report["inputs"].get("vanilla_sha512") != vanilla_sha512:
        raise MineJammerError("village evidence vanilla_sha512 is not locked to the official inner server archive")
    restored = report.get("restored")
    if not isinstance(restored, list) or len(restored) != 6:
        raise MineJammerError("village evidence must contain all six restored vanilla resources")
    if report.get("terralith_namespace_written") is not False or not _is_sha512(report.get("output_sha512")):
        raise MineJammerError("village adapter evidence violates the preservation contract")
    return {"output_sha512": report["output_sha512"], "restored_resources": len(restored)}


def _validate_fishing(report: dict[str, Any], lock_hashes: dict[str, str]) -> dict[str, Any]:
    _locked_input_matches(report, "matcha_sha512", "matcha", lock_hashes)
    _locked_input_matches(report, "terralith_sha512", "terralith", lock_hashes)
    if report.get("hot_wet_corrections") != 1 or not isinstance(report.get("assignment_count"), int) or report["assignment_count"] <= 0:
        raise MineJammerError("fishing adapter evidence is incomplete")
    if not _is_sha512(report.get("output_sha512")):
        raise MineJammerError("fishing adapter evidence has no locked output")
    return {"output_sha512": report["output_sha512"], "assignment_count": report["assignment_count"]}


AUTOMATED_VALIDATORS: dict[str, Callable[[dict[str, Any], dict[str, str]], dict[str, Any]]] = {
    "acquisition": _validate_acquisition,
    "compatibility": _validate_compatibility,
    "fishing": _validate_fishing,
}


def _validated_gate_configuration(config: dict[str, Any]) -> tuple[str, int, list[dict[str, Any]]]:
    if config.get("schema") != "minescape.release-gates.v2":
        raise MineJammerError("unsupported release gate policy")
    world_epoch = config.get("world_epoch")
    if not isinstance(world_epoch, str) or not world_epoch.strip() or world_epoch.lower() in {"unset", "replace-me"}:
        raise MineJammerError("release policy requires a concrete world_epoch")
    max_age = config.get("manual_evidence_max_age_hours")
    if not isinstance(max_age, int) or isinstance(max_age, bool) or not 1 <= max_age <= 24 * 31:
        raise MineJammerError("manual_evidence_max_age_hours must be between 1 and 744")
    gates = config.get("gates")
    if not isinstance(gates, list) or len(gates) != len(GATE_SPECS):
        raise MineJammerError("release policy must contain every canonical gate exactly once")
    seen: set[str] = set()
    evidence_names: set[str] = set()
    for gate in gates:
        if not isinstance(gate, dict) or not isinstance(gate.get("id"), str):
            raise MineJammerError("release gate entry is malformed")
        gate_id = gate["id"]
        if gate_id in seen or gate_id not in GATE_SPECS:
            raise MineJammerError(f"unknown or duplicate release gate: {gate_id}")
        seen.add(gate_id)
        evaluator, schema = GATE_SPECS[gate_id]
        if gate.get("evaluator") != evaluator or gate.get("schema") != schema:
            raise MineJammerError(f"release gate {gate_id} attempts to weaken its evaluator/schema")
        evidence = gate.get("evidence")
        if (not isinstance(evidence, str) or not evidence.endswith(".json") or
                Path(evidence).name != evidence or evidence in evidence_names):
            raise MineJammerError(f"release gate {gate_id} has an unsafe or duplicate evidence filename")
        evidence_names.add(evidence)
    if seen != set(GATE_SPECS):
        raise MineJammerError("release policy omitted canonical release gates")
    return world_epoch, max_age, gates


def _adapter_completion_check(config: dict[str, Any]) -> dict[str, Any]:
    """Evaluate the committed code-completion interlock, never manual evidence."""
    policy = config.get("adapter_completion")
    if not isinstance(policy, dict) or policy.get("schema") != "minescape.adapter-completion-policy.v1":
        raise MineJammerError("release policy is missing the code-owned adapter-completion interlock")
    if not isinstance(policy.get("promotion_permitted"), bool):
        raise MineJammerError("adapter-completion promotion_permitted must be a boolean")
    components = policy.get("components")
    if not isinstance(components, list) or len(components) != len(REQUIRED_ADAPTER_COMPLETION):
        raise MineJammerError("adapter-completion policy must contain every code-owned component exactly once")

    statuses: dict[str, bool] = {}
    for component in components:
        if (not isinstance(component, dict) or set(component) != {"id", "complete"} or
                not isinstance(component.get("id"), str) or not isinstance(component.get("complete"), bool) or
                component["id"] in statuses):
            raise MineJammerError("adapter-completion policy contains a malformed or duplicate component")
        statuses[component["id"]] = component["complete"]
    if set(statuses) != REQUIRED_ADAPTER_COMPLETION:
        raise MineJammerError("adapter-completion policy differs from the code-owned component set")

    incomplete = sorted(name for name, complete in statuses.items() if not complete)
    promotion_permitted = policy["promotion_permitted"]
    return {
        "schema": policy["schema"],
        "passed": promotion_permitted and not incomplete,
        "promotion_permitted": promotion_permitted,
        "incomplete": incomplete,
        "detail": ("all code adapters are explicitly complete and promotion is enabled"
                   if promotion_permitted and not incomplete
                   else "code adapters or the explicit promotion switch remain incomplete"),
    }


def release_preflight(config_path: str | Path, evidence_dir: str | Path, lock_path: str | Path,
                      minecraft_server_lock_path: str | Path, cache: str | Path,
                      output: str | Path | None = None) -> dict[str, Any]:
    config_file = Path(config_path).resolve(strict=True)
    config = load_json(config_file)
    if not isinstance(config, dict):
        raise MineJammerError("release gate policy must be an object")
    world_epoch, max_age, gates = _validated_gate_configuration(config)
    root = Path(evidence_dir).resolve(strict=True)
    if not root.is_dir():
        raise MineJammerError("release evidence directory does not exist")
    lock_file = Path(lock_path).resolve(strict=True)
    lock = load_json(lock_file)
    if not isinstance(lock, dict) or lock.get("schema") != "minescape.artifacts.lock.v1":
        raise MineJammerError("unsupported artifact lock schema")
    lock_hashes = _lock_hashes(lock)
    lock_sha512 = sha512_file(lock_file)
    minecraft = _validated_minecraft_server_lock(
        minecraft_server_lock_path, cache, str(lock.get("minecraft", "")))
    checks = []
    lock_result = verify_lock(lock_file, cache)
    checks.append({"gate": "artifact_lock", "passed": lock_result["ok"], "detail": lock_result})
    checks.append({"gate": "minecraft_server_lock", "passed": True, "detail": minecraft})
    adapter_completion = _adapter_completion_check(config)
    checks.append({
        "gate": "adapter-completion",
        "passed": adapter_completion["passed"],
        "detail": adapter_completion,
    })
    now = datetime.now(timezone.utc)
    for gate in gates:
        gate_id = gate["id"]
        path = (root / gate["evidence"]).resolve(strict=False)
        if not is_within(path, root) or not path.is_file():
            checks.append({"gate": gate_id, "passed": False, "detail": "missing or unsafe evidence path"})
            continue
        try:
            evidence = load_json(path)
            if not isinstance(evidence, dict) or evidence.get("schema") != gate["schema"]:
                raise MineJammerError(f"expected evidence schema {gate['schema']}")
            evaluator = gate["evaluator"]
            if evaluator == "manual":
                detail = _validate_manual_metadata(evidence, gate_id, gate["schema"], world_epoch,
                                                   lock_sha512, max_age, now)
            elif evaluator == "seed-audit":
                metadata = _validate_manual_metadata(evidence, gate_id, gate["schema"], world_epoch,
                                                     lock_sha512, max_age, now)
                policy_value = config.get("seed_policy")
                if not isinstance(policy_value, str):
                    raise MineJammerError("release policy does not name the seed policy")
                seed_policy = (config_file.parent / policy_value).resolve(strict=True)
                if not is_within(seed_policy, config_file.parent):
                    raise MineJammerError("seed policy path escapes the release policy directory")
                seed_result = audit_seed(path, seed_policy)
                if not seed_result["passed"]:
                    raise MineJammerError("specialized seed audit failed")
                detail = {"manual": metadata, "seed_audit": seed_result}
            else:
                if evaluator == "village":
                    detail = _validate_village(evidence, lock_hashes, minecraft["bundled_server_sha512"])
                else:
                    validator = AUTOMATED_VALIDATORS.get(evaluator)
                    if validator is None:
                        raise MineJammerError(f"no code-owned evaluator for {gate_id}")
                    detail = validator(evidence, lock_hashes)
            detail = {"file": path.name, "sha512": sha512_file(path), "evaluation": detail}
            checks.append({"gate": gate_id, "passed": True, "detail": detail})
        except (MineJammerError, KeyError, TypeError, ValueError, OSError) as exc:
            checks.append({"gate": gate_id, "passed": False, "detail": str(exc)})
    result = {
        "schema": "minescape.release-preflight.v2",
        "passed": all(item["passed"] for item in checks),
        "world_epoch": world_epoch,
        "artifact_lock_sha512": lock_sha512,
        "minecraft_server_lock_sha512": minecraft["lock_sha512"],
        "vanilla_server_sha512": minecraft["bundled_server_sha512"],
        "evaluated_at": now.isoformat().replace("+00:00", "Z"),
        "checks": checks,
    }
    if output:
        dump_json(output, result)
    return result
