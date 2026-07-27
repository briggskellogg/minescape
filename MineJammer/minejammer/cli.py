from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any, Callable

from .archives import audit_zip, collision_report
from .artifacts import acquire, validate_source_manifest, verify_lock
from .builders import (
    build_compatibility,
    build_fishing_adapter,
    build_resource_only,
    build_village_adapter,
    compatibility_candidates,
    generate_curio_spec,
    generate_food_normalizer,
    generate_loot_adapter,
)
from .preflight import audit_seed, release_preflight
from .util import MineJammerError, dump_json, load_json
from .worlds import clone_world, commit_parcel, parcel_plan, recover_parcel


def _emit(value: Any) -> None:
    # ASCII escaping keeps the CLI reliable in Windows services using legacy
    # console code pages; report files remain canonical UTF-8.
    print(json.dumps(value, sort_keys=True, indent=2, ensure_ascii=True))


def _write_optional(path: str | None, value: Any) -> None:
    if path:
        dump_json(path, value)


def _common_report(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--report", help="write canonical JSON report")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="minejammer", description="MineScape compatibility and release laboratory")
    sub = parser.add_subparsers(dest="command", required=True)

    item = sub.add_parser("manifest-validate", help="validate the pinned source manifest")
    item.add_argument("manifest")

    item = sub.add_parser("acquire", help="resolve Modrinth metadata and optionally download verified artifacts")
    item.add_argument("manifest")
    item.add_argument("--cache", required=True)
    item.add_argument("--lock", required=True)
    item.add_argument("--report", required=True)
    item.add_argument("--metadata-only", action="store_true")

    item = sub.add_parser("verify-lock", help="verify every cached artifact against a lock")
    item.add_argument("lock")
    item.add_argument("--cache", required=True)
    _common_report(item)

    item = sub.add_parser("zip-audit", help="audit a zip/jar without extracting it")
    item.add_argument("archive")
    _common_report(item)

    item = sub.add_parser("collisions", help="report byte-level archive path collisions")
    item.add_argument("left")
    item.add_argument("right")
    item.add_argument("--left-name", default="left")
    item.add_argument("--right-name", default="right")
    _common_report(item)

    item = sub.add_parser("compat-candidates", help="enumerate Matcha/Stardust biome collisions and JSON-pointer deltas")
    item.add_argument("--matcha", required=True)
    item.add_argument("--terralith", required=True)
    item.add_argument("--nullscape", required=True)
    item.add_argument("--report", required=True)

    item = sub.add_parser("build-compat", help="build reviewed Stardust-base compatibility pack")
    item.add_argument("--matcha", required=True)
    item.add_argument("--terralith", required=True)
    item.add_argument("--nullscape", required=True)
    item.add_argument("--policy", required=True)
    item.add_argument("--output", required=True)
    item.add_argument("--report", required=True)

    item = sub.add_parser("build-village-adapter", help="restore pinned vanilla villages and disable Matcha beta haunting")
    item.add_argument("--vanilla", required=True, help="extracted vanilla data directory or jar/zip")
    item.add_argument("--matcha", required=True)
    item.add_argument("--output", required=True)
    item.add_argument("--report", required=True)

    item = sub.add_parser("build-fishing-adapter", help="classify Terralith biomes and fix Matcha's hot-wet predicate")
    item.add_argument("--matcha", required=True)
    item.add_argument("--terralith", required=True)
    item.add_argument("--policy", required=True)
    item.add_argument("--output", required=True)
    item.add_argument("--report", required=True)

    item = sub.add_parser("build-loot-spec", help="lock exhaustive per-structure Matcha enrichment mapping")
    item.add_argument("--matcha", required=True)
    item.add_argument("--terralith", required=True)
    item.add_argument("--policy", required=True)
    item.add_argument("--output", required=True)

    item = sub.add_parser("build-food-spec", help="derive exact Matcha components for componentless Terralith food")
    item.add_argument("--matcha", required=True)
    item.add_argument("--terralith", required=True)
    item.add_argument("--output", required=True)

    item = sub.add_parser("build-curio-spec", help="write the deterministic namespaced 19:4:1 village cache specification")
    item.add_argument("--matcha", required=True)
    item.add_argument("--output", required=True)

    item = sub.add_parser("build-resource-only", help="build an assets-only Matcha derivative with strict reference guard")
    item.add_argument("--frozen", required=True)
    item.add_argument("--candidate", required=True)
    item.add_argument("--policy", dest="resource_policy")
    item.add_argument("--allow-missing", dest="resource_policy", help=argparse.SUPPRESS)
    item.add_argument("--output", required=True)
    item.add_argument("--report", required=True)

    item = sub.add_parser("clone-world", help="plan or transactionally clone MineScape into MineJammer")
    item.add_argument("--source", required=True)
    item.add_argument("--destination", required=True)
    item.add_argument("--receipt")
    item.add_argument("--apply", action="store_true", help="perform the clone; omission is a dry run")

    item = sub.add_parser("parcel-plan", help="reject unsafe or explored MineJammer parcel changes")
    item.add_argument("--production", required=True)
    item.add_argument("--staging", required=True)
    item.add_argument("--policy", required=True)
    item.add_argument("--exploration", required=True)
    item.add_argument("--backup-receipt", required=True)
    item.add_argument("--output", required=True)

    item = sub.add_parser("parcel-commit", help="revalidate parcel readiness; production apply is release-gated")
    item.add_argument("--plan", required=True)
    item.add_argument("--offline-token", required=True)
    item.add_argument("--journal-root", required=True)
    item.add_argument(
        "--apply", action="store_true",
        help="fail closed until Bridge signature verification and one-time token consumption ship",
    )

    item = sub.add_parser("parcel-recover", help="inspect a journal; recovery writes are release-gated")
    item.add_argument("--journal", required=True)
    item.add_argument("--apply", action="store_true", help="fail closed until authenticated journals ship")

    item = sub.add_parser("seed-audit", help="evaluate machine-readable seed evidence")
    item.add_argument("--evidence", required=True)
    item.add_argument("--policy", required=True)
    _common_report(item)

    item = sub.add_parser("preflight", help="aggregate artifact and release evidence gates")
    item.add_argument("--policy", required=True)
    item.add_argument("--evidence-dir", required=True)
    item.add_argument("--lock", required=True)
    item.add_argument("--minecraft-server-lock", required=True)
    item.add_argument("--cache", required=True)
    _common_report(item)
    return parser


def run(args: argparse.Namespace) -> Any:
    command = args.command
    if command == "manifest-validate":
        document = load_json(args.manifest)
        validate_source_manifest(document)
        return {"schema": document["schema"], "valid": True, "artifacts": len(document["artifacts"])}
    if command == "acquire":
        return acquire(args.manifest, args.cache, args.lock, args.report, args.metadata_only)
    if command == "verify-lock":
        result = verify_lock(args.lock, args.cache)
        _write_optional(args.report, result)
        return result
    if command == "zip-audit":
        result = audit_zip(args.archive)
        _write_optional(args.report, result)
        return result
    if command == "collisions":
        result = collision_report(args.left, args.right, args.left_name, args.right_name)
        _write_optional(args.report, result)
        return result
    if command == "compat-candidates":
        result = compatibility_candidates(args.matcha, args.terralith, args.nullscape)
        dump_json(args.report, result)
        return result
    if command == "build-compat":
        return build_compatibility(args.matcha, args.terralith, args.nullscape, args.policy, args.output, args.report)
    if command == "build-village-adapter":
        return build_village_adapter(args.vanilla, args.matcha, args.output, args.report)
    if command == "build-fishing-adapter":
        return build_fishing_adapter(args.matcha, args.terralith, args.policy, args.output, args.report)
    if command == "build-loot-spec":
        return generate_loot_adapter(args.matcha, args.terralith, args.policy, args.output)
    if command == "build-food-spec":
        return generate_food_normalizer(args.matcha, args.terralith, args.output)
    if command == "build-curio-spec":
        return generate_curio_spec(args.matcha, args.output)
    if command == "build-resource-only":
        return build_resource_only(args.frozen, args.candidate, args.output, args.report, args.resource_policy)
    if command == "clone-world":
        return clone_world(args.source, args.destination, args.apply, args.receipt)
    if command == "parcel-plan":
        return parcel_plan(args.production, args.staging, args.policy, args.exploration, args.backup_receipt, args.output)
    if command == "parcel-commit":
        return commit_parcel(args.plan, args.offline_token, args.journal_root, args.apply)
    if command == "parcel-recover":
        return recover_parcel(args.journal, args.apply)
    if command == "seed-audit":
        return audit_seed(args.evidence, args.policy, args.report)
    if command == "preflight":
        return release_preflight(
            args.policy, args.evidence_dir, args.lock, args.minecraft_server_lock, args.cache, args.report)
    raise AssertionError(command)


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        result = run(args)
        _emit(result)
        if result.get("ok") is False or result.get("passed") is False or result.get("complete") is False:
            return 1
        return 0
    except (MineJammerError, OSError, ValueError) as error:
        print(f"MineJammer refused: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
