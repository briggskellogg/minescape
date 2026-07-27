from __future__ import annotations

import base64
import gzip
import hashlib
import json
import struct
import tempfile
import unittest
import zipfile
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest import mock

from minejammer.archives import audit_zip, collision_report
from minejammer.artifacts import acquire, validate_source_manifest
from minejammer.builders import (
    build_compatibility, build_fishing_adapter, build_resource_only, build_village_adapter,
    generate_food_normalizer, generate_loot_adapter, pointer_get, pointer_set, _classify_biome,
)
from minejammer.nbt import loads as load_nbt
from minejammer.preflight import audit_seed, release_preflight
from minejammer.util import MineJammerError, canonical_json, dump_json
from minejammer.worlds import clone_world, commit_parcel, parcel_plan, recover_parcel


ROOT = Path(__file__).resolve().parents[1]


def make_zip(path: Path, entries: dict[str, object]) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        for name, value in entries.items():
            payload = value if isinstance(value, bytes) else canonical_json(value)
            archive.writestr(name, payload)


def pack_meta() -> dict:
    return {"pack": {"pack_format": 999, "description": "fixture"}}


class ManifestTests(unittest.TestCase):
    def test_canonical_manifest(self):
        document = json.loads((ROOT / "manifests/artifacts.json").read_text(encoding="utf-8"))
        validate_source_manifest(document)
        self.assertEqual(43, len(document["artifacts"]))
        self.assertEqual("Eclipse Temurin 25.0.3+9", document["java"])

    def test_acquisition_records_hash_license_and_provenance(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = {
                "schema": "minescape.artifacts.source.v1", "minecraft": "26.2",
                "java": "Eclipse Temurin 25.0.3+9",
                "toolchain": {"dotnet_sdk": "10.0.302", "python": "3.13.14", "gradle": "9.5.1"},
                "fabric_loader": "0.19.3", "fabric_launcher": "1.1.1",
                "artifacts": [{"key": "fixture", "name": "Fixture", "provider": "modrinth", "project": "fixture", "version": "1", "version_id": "AbCd1234", "creator": "Creator", "status": "test", "profiles": ["test"]}],
            }
            dump_json(root / "source.json", manifest)
            version = {"id": "AbCd1234", "version_number": "1+fabric", "project_id": "Project1", "files": [{"primary": True, "filename": "fixture.zip", "size": 1, "url": "https://cdn.modrinth.com/f", "hashes": {"sha512": "a" * 128}}], "loaders": ["fabric"], "game_versions": ["26.2"]}
            project = {"id": "Project1", "slug": "fixture", "license": {"id": "MIT", "name": "MIT"}}
            with mock.patch("minejammer.artifacts._get_json", side_effect=[version, project]):
                report = acquire(root / "source.json", root / "cache", root / "lock.json", root / "report.json", metadata_only=True)
            lock = json.loads((root / "lock.json").read_text())
            self.assertTrue(report["complete"])
            self.assertEqual("a" * 128, lock["artifacts"][0]["sha512"])
            self.assertEqual("MIT", lock["artifacts"][0]["license"]["id"])


class ArchiveAndBuilderTests(unittest.TestCase):
    def test_zip_audit_and_collision(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); left = root / "a.zip"; right = root / "b.zip"
            make_zip(left, {"data/a.json": {"x": 1}}); make_zip(right, {"data/a.json": {"x": 2}})
            self.assertTrue(audit_zip(left)["ok"])
            self.assertEqual(1, collision_report(left, right, "a", "b")["different_count"])
            bad = root / "bad.zip"
            with zipfile.ZipFile(bad, "w") as archive: archive.writestr("../escape", b"x")
            self.assertFalse(audit_zip(bad)["ok"])

    def test_json_pointer_and_compatibility_build(self):
        document = {"a": {"b": [1, 2]}}
        self.assertEqual(2, pointer_get(document, "/a/b/1")); pointer_set(document, "/a/b/0", 9)
        self.assertEqual(9, document["a"]["b"][0])
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); matcha = root / "m.zip"; terra = root / "t.zip"; null = root / "n.zip"
            path1 = "data/minecraft/worldgen/biome/plains.json"; path2 = "data/minecraft/worldgen/biome/the_end.json"
            setup = "data/main/function/setup/scoreboard.mcfunction"
            hpdown = "data/main/function/mechanic/hpdown.mcfunction"
            heart_container = "data/main/function/mechanic/process_heart_container.mcfunction"
            clear_heart_container = "data/main/function/mechanic/clear_heart_container.mcfunction"
            set_max_hp = "data/main/function/mechanic/set_max_hp.mcfunction"
            setup_source = (
                b"scoreboard objectives add Hearts dummy\r\n"
                b"scoreboard players add @a Hearts 0\r\n"
                b"execute at @a[scores={Hearts=..20}] run scoreboard players set @p Hearts 20\r\n"
                b"scoreboard objectives add divinity dummy\r\n"
            )
            hpdown_source = b"scoreboard players remove @s Hearts 2\n"
            heart_container_source = b"function main:mechanic/clear_heart_container\n"
            clear_heart_container_source = b"scoreboard players add @p Hearts 2\n"
            set_max_hp_source = b"attribute @p minecraft:max_health base set 20\n"
            make_zip(matcha, {
                "pack.mcmeta": pack_meta(), path1: {"effects": {"x": 1}, "features": ["matcha"]},
                path2: {"features": ["matcha"]}, setup: setup_source, hpdown: hpdown_source,
                heart_container: heart_container_source, clear_heart_container: clear_heart_container_source,
                set_max_hp: set_max_hp_source,
            })
            make_zip(terra, {"pack.mcmeta": pack_meta(), path1: {"effects": {"x": 2}, "features": ["terra"]}})
            make_zip(null, {"pack.mcmeta": pack_meta(), path2: {"features": ["null"]}})
            policy = {
                "schema": "minescape.compatibility-policy.v1", "allowlist_reviewed": True,
                "reviewed_input_sha512": {
                    "matcha": hashlib.sha512(matcha.read_bytes()).hexdigest(),
                    "terralith": hashlib.sha512(terra.read_bytes()).hexdigest(),
                    "nullscape": hashlib.sha512(null.read_bytes()).hexdigest(),
                },
                "expected_collisions": {"terralith": 1, "nullscape": 1},
                "matcha_delta_allowlist": [{"resource": path1, "copies": [{"from": "/effects/x"}]}],
                "mechanical_overrides": [
                    {"resource": setup, "source_sha256": hashlib.sha256(setup_source).hexdigest(),
                     "action": "remove_matcha_heart_floor"},
                    {"resource": hpdown, "source_sha256": hashlib.sha256(hpdown_source).hexdigest(),
                     "action": "reset_death_score_only"},
                    {"resource": heart_container,
                     "source_sha256": hashlib.sha256(heart_container_source).hexdigest(),
                     "action": "replace_with_inert_comment"},
                    {"resource": clear_heart_container,
                     "source_sha256": hashlib.sha256(clear_heart_container_source).hexdigest(),
                     "action": "replace_with_inert_comment"},
                    {"resource": set_max_hp,
                     "source_sha256": hashlib.sha256(set_max_hp_source).hexdigest(),
                     "action": "replace_with_inert_comment"},
                ],
            }
            dump_json(root / "policy.json", policy)
            result = build_compatibility(matcha, terra, null, root / "policy.json", root / "out.zip", root / "report.json")
            self.assertEqual({"terralith": 1, "nullscape": 1}, result["collision_counts"])
            self.assertEqual(
                {setup, hpdown, heart_container, clear_heart_container, set_max_hp},
                {item["resource"] for item in result["mechanical_overrides"]})
            with zipfile.ZipFile(root / "out.zip") as archive:
                merged = json.loads(archive.read(path1)); self.assertEqual(["terra"], merged["features"]); self.assertEqual(1, merged["effects"]["x"])
                setup_output = archive.read(setup)
                self.assertIn(b"scoreboard objectives add Hearts dummy", setup_output)
                self.assertIn(b"scoreboard objectives add divinity dummy", setup_output)
                self.assertNotIn(b"scores={Hearts=..20}", setup_output)
                self.assertIn(b"sole death-heart authority", archive.read(hpdown))
                self.assertIn(b"scoreboard players set @a[scores={deaths=1..}] deaths 0", archive.read(hpdown))
                self.assertIn(b"sole Crystal Heart authority", archive.read(heart_container))
                self.assertIn(b"sole Crystal Heart authority", archive.read(clear_heart_container))
                self.assertIn(b"sole maximum-health authority", archive.read(set_max_hp))

            bad_source_policy = json.loads(json.dumps(policy))
            bad_source_policy["mechanical_overrides"][0]["source_sha256"] = "0" * 64
            dump_json(root / "bad-source-policy.json", bad_source_policy)
            with self.assertRaisesRegex(MineJammerError, "reviewed Matcha mechanical source changed"):
                build_compatibility(matcha, terra, null, root / "bad-source-policy.json",
                                    root / "bad-source.zip", root / "bad-source.json")

            make_zip(null, {"pack.mcmeta": pack_meta(), path2: {"features": ["changed-after-review"]}})
            with self.assertRaisesRegex(MineJammerError, "reviewed against a different nullscape archive"):
                build_compatibility(matcha, terra, null, root / "policy.json",
                                    root / "changed-input.zip", root / "changed-input.json")

    def test_village_and_fishing_builders(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); vanilla = root / "v.zip"; matcha = root / "m.zip"; terra = root / "t.zip"
            village_paths = [f"data/minecraft/worldgen/structure/village_{kind}.json" for kind in ("plains", "desert", "savanna", "snowy", "taiga")]
            make_zip(vanilla, {**{path: {"vanilla": path} for path in village_paths}, "data/minecraft/worldgen/structure_set/villages.json": {"vanilla": True}})
            fishing = {"pools": [{"entries": [{"value": "minecraft:gameplay/fishing/freshwater_hot_wet", "conditions": [{"predicate": {"biomes": "#minecraft:freshwater_hot_dry"}}]}]}]}
            make_zip(matcha, {"pack.mcmeta": pack_meta(), **{path: {"matcha": True} for path in village_paths}, "data/minecraft/worldgen/structure_set/villages.json": {"matcha": True}, "data/minecraft/loot_table/gameplay/fishing.json": fishing})
            make_zip(terra, {"pack.mcmeta": pack_meta(), "data/terralith/worldgen/biome/wet.json": {"temperature": 1.1, "downfall": 0.8}, "data/terralith/worldgen/biome/cave/dry.json": {"temperature": 1.1, "downfall": 0.0}})
            build_village_adapter(vanilla, matcha, root / "villages.zip", root / "villages.json")
            with zipfile.ZipFile(root / "villages.zip") as archive: self.assertTrue(json.loads(archive.read(village_paths[0]))["vanilla"])
            policy = {"schema": "minescape.fishing-climates.v1", "climates": ["freshwater_cold", "freshwater_cool", "freshwater_temperate", "freshwater_hot_wet", "freshwater_hot_dry"], "freshwater_thresholds": {"cold_max": .2, "cool_max": .5, "temperate_max": .9, "hot_wet_min_downfall": .5}, "name_overrides": {}, "no_fish_patterns": ["/cave/"]}
            dump_json(root / "fish-policy.json", policy)
            result = build_fishing_adapter(matcha, terra, root / "fish-policy.json", root / "fish.zip", root / "fish.json")
            self.assertEqual(1, result["hot_wet_corrections"]); self.assertEqual(2, result["assignment_count"])

    def test_canonical_deep_warm_ocean_is_warm_saltwater(self):
        policy = json.loads((ROOT / "config/fishing-climates.json").read_text(encoding="utf-8"))
        actual = _classify_biome(
            "data/terralith/worldgen/biome/deep_warm_ocean.json",
            {"temperature": 0.5, "downfall": 0.5},
            policy,
        )
        self.assertEqual("saltwater_warm", actual)

    def test_loot_food_and_resource_only(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); matcha = root / "m.zip"; terra = root / "t.zip"
            bread = {"pools": [{"entries": [{"type": "minecraft:item", "name": "minecraft:bread", "functions": [{"function": "minecraft:set_components", "components": {"minecraft:food": {"nutrition": 0}}}]}]}]}
            make_zip(matcha, {"pack.mcmeta": pack_meta(), "assets/minecraft/a.txt": b"a", "data/x.json": {}, "data/minecraft/loot_table/chests/simple_dungeon.json": {}, "data/minecraft/loot_table/food/bread.json": bread})
            make_zip(terra, {"pack.mcmeta": pack_meta(), "data/terralith/worldgen/structure/spire.json": {}, "data/terralith/loot_table/spire.json": {"pools": [{"entries": [{"type": "minecraft:item", "name": "minecraft:bread"}]}]}})
            policy = {"schema": "minescape.loot-adapter-policy.v1", "version": "1", "world_seed": "1", "forbidden_items": [], "structures": {"terralith:spire": {"matcha_bonus_table": "minecraft:chests/simple_dungeon"}}}
            dump_json(root / "loot.json", policy)
            self.assertTrue(generate_loot_adapter(matcha, terra, root / "loot.json", root / "loot.lock")["structures"]["terralith:spire"]["eligible"])
            self.assertIn("minecraft:bread", generate_food_normalizer(matcha, terra, root / "food.lock")["mappings"])
            report = build_resource_only(matcha, matcha, root / "assets.zip", root / "assets.json")
            self.assertEqual(0, report["data_members_emitted"])
            with zipfile.ZipFile(root / "assets.zip") as archive: self.assertFalse(any(name.startswith("data/") for name in archive.namelist()))

    def test_resource_derivative_requires_exact_per_asset_approval(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); frozen = root / "frozen.zip"; candidate = root / "candidate.zip"
            make_zip(frozen, {"pack.mcmeta": pack_meta(), "assets/minecraft/a.txt": b"old"})
            make_zip(candidate, {
                "pack.mcmeta": pack_meta(),
                "assets/minecraft/a.txt": b"new",
                "assets/minecraft/b.txt": b"added",
                "data/forbidden.json": {},
            })
            with self.assertRaises(MineJammerError):
                build_resource_only(frozen, candidate, root / "rejected.zip", root / "rejected.json")

            sha512 = lambda value: hashlib.sha512(value).hexdigest()
            policy = {
                "schema": "minescape.resource-delta-policy.v1",
                "approved_changes": [{
                    "path": "assets/minecraft/a.txt", "frozen_sha512": sha512(b"old"),
                    "candidate_sha512": sha512(b"new"), "reason": "Reviewed visual replacement",
                }],
                "approved_additions": [{
                    "path": "assets/minecraft/b.txt", "candidate_sha512": sha512(b"added"),
                    "reason": "Reviewed new visual",
                }],
                "approved_removals": [],
            }
            dump_json(root / "policy.json", policy)
            report = build_resource_only(
                frozen, candidate, root / "approved.zip", root / "approved.json", root / "policy.json")
            self.assertEqual(["assets/minecraft/a.txt"], report["approved_changed_assets"])
            with zipfile.ZipFile(root / "approved.zip") as archive:
                self.assertEqual(b"new", archive.read("assets/minecraft/a.txt"))
                self.assertEqual(b"added", archive.read("assets/minecraft/b.txt"))
                self.assertFalse(any(name.startswith("data/") for name in archive.namelist()))

    def test_nbt_reader(self):
        # Root compound containing one named integer.
        payload = bytes([10]) + struct.pack(">H", 0) + bytes([3]) + struct.pack(">H", 1) + b"x" + struct.pack(">i", 7) + bytes([0])
        self.assertEqual({"x": 7}, load_nbt(gzip.compress(payload)))


class WorldAndReleaseTests(unittest.TestCase):
    def test_clone_and_parcel_guards(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); production = root / "production"; production.mkdir(); (production / "region").mkdir(); (production / "region/r.0.0.mca").write_bytes(b"old")
            staging = root / "staging"; dry = clone_world(production, staging); self.assertTrue(dry["dry_run"]); clone_world(production, staging, apply=True)
            backup = root / "pristine-backup"
            clone_world(production, backup, apply=True, receipt_path=root / "backup.json")
            (staging / "region/r.0.0.mca").write_bytes(b"new")
            policy = {"schema": "minescape.parcel-policy.v1", "bounds": {"overworld": {"min_x": 0, "max_x": 512, "min_z": 0, "max_z": 512}}}; dump_json(root / "policy.json", policy)
            dump_json(root / "explored.json", {"schema": "minescape.explored-regions.v1", "regions": {"overworld": []}})
            plan = parcel_plan(production, staging, root / "policy.json", root / "explored.json", root / "backup.json", root / "plan.json")
            self.assertEqual(1, len(plan["changes"]))
            self.assertEqual("minescape.parcel-plan.v2", plan["schema"])
            (staging / "level.dat").write_bytes(b"global")
            with self.assertRaises(MineJammerError): parcel_plan(production, staging, root / "policy.json", root / "explored.json", root / "backup.json", root / "bad-plan.json")

    def test_parcel_commit_revalidates_every_identity_and_apply_is_unavailable(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            production = root / "production"; (production / "region").mkdir(parents=True)
            (production / "region/r.0.0.mca").write_bytes(b"old")
            staging = root / "staging"; clone_world(production, staging, apply=True)
            backup = root / "backup"; clone_world(production, backup, apply=True, receipt_path=root / "backup.json")
            (staging / "region/r.0.0.mca").write_bytes(b"new")
            dump_json(root / "policy.json", {"schema": "minescape.parcel-policy.v1", "bounds": {"overworld": {"min_x": 0, "max_x": 512, "min_z": 0, "max_z": 512}}})
            dump_json(root / "explored.json", {"schema": "minescape.explored-regions.v1", "regions": {"overworld": []}})
            plan = parcel_plan(production, staging, root / "policy.json", root / "explored.json", root / "backup.json", root / "plan.json")

            def token_for(document: dict, *, expired: bool = False) -> dict:
                now = datetime.now(timezone.utc)
                issued = now - (timedelta(minutes=10) if expired else timedelta(seconds=10))
                expires = issued + timedelta(minutes=2)
                return {
                    "schema": "minescape.bridge-offline-token.v1", "issuer": "minescape-bridge",
                    "purpose": "parcel-commit", "token_id": "test-token-00000001", "one_time": True,
                    "server_offline": True, "world": str(production), "plan_id": document["plan_id"],
                    "production_tree_digest": document["production_tree_digest"],
                    "issued_at": issued.isoformat().replace("+00:00", "Z"),
                    "expires_at": expires.isoformat().replace("+00:00", "Z"),
                    "signature": {"algorithm": "Ed25519", "key_id": "test-key",
                                  "value": base64.b64encode(b"x" * 64).decode("ascii")},
                }

            dump_json(root / "token.json", token_for(plan))
            readiness = commit_parcel(root / "plan.json", root / "token.json", root / "journals")
            self.assertFalse(readiness["apply_available"])
            self.assertFalse(readiness["authorization"]["signature_verified"])
            with self.assertRaisesRegex(MineJammerError, "--apply is unavailable"):
                commit_parcel(root / "plan.json", root / "token.json", root / "journals", apply=True)
            self.assertEqual(b"old", (production / "region/r.0.0.mca").read_bytes())
            self.assertFalse((root / "journals").exists())

            tampered = json.loads(json.dumps(plan))
            tampered["changes"][0]["region"] = "99,99"
            unsigned = dict(tampered); unsigned.pop("plan_id")
            tampered["plan_id"] = hashlib.sha256(canonical_json(unsigned)).hexdigest()
            dump_json(root / "tampered-plan.json", tampered)
            dump_json(root / "tampered-token.json", token_for(tampered))
            with self.assertRaisesRegex(MineJammerError, "re-derived path/bounds/region"):
                commit_parcel(root / "tampered-plan.json", root / "tampered-token.json", root / "journals")

            dump_json(root / "expired-token.json", token_for(plan, expired=True))
            with self.assertRaisesRegex(MineJammerError, "not currently valid"):
                commit_parcel(root / "plan.json", root / "expired-token.json", root / "journals")

            forged_journal = root / "forged-journal"; forged_journal.mkdir()
            dump_json(forged_journal / "plan.json", {"changes": [], "production": str(production)})
            dump_json(forged_journal / "state.json", {"state": "prepared"})
            self.assertFalse(recover_parcel(forged_journal)["apply_available"])
            with self.assertRaisesRegex(MineJammerError, "recovery --apply is unavailable"):
                recover_parcel(forged_journal, apply=True)

    def test_seed_gate(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); policy = {"schema": "minescape.seed-policy.v1", "seed": "1", "boundaries": {"overworld": 1}, "required_structures": ["a"]}; evidence = {"schema": "minescape.seed-evidence.v1", "seed": "1", "boundaries": {"overworld": 1}, "structure_counts": {"a": 1}, "starter_village": {"natural": True, "custom_homes": False, "public_arrival": True}, "far_rim": {"accepted": True}, "finite_resources": {"complete": True}}
            dump_json(root / "p.json", policy); dump_json(root / "e.json", evidence)
            self.assertTrue(audit_seed(root / "e.json", root / "p.json")["passed"])

    def test_release_preflight_uses_code_owned_evaluators_and_rejects_trivial_pass(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); artifacts = root / "artifacts"; cache = artifacts / "cache"
            cache.mkdir(parents=True); evidence_dir = root / "evidence"; evidence_dir.mkdir()
            locked = []
            artifact_hashes = {}
            for key in ("matcha", "terralith", "nullscape"):
                payload = key.encode("ascii")
                digest = hashlib.sha512(payload).hexdigest(); artifact_hashes[key] = digest
                path = cache / key / f"{key}.zip"; path.parent.mkdir(); path.write_bytes(payload)
                locked.append({"key": key, "filename": path.name, "sha512": digest})
            lock = {"schema": "minescape.artifacts.lock.v1", "minecraft": "26.2", "artifacts": locked}
            artifact_lock_path = artifacts / "artifacts.lock.json"
            dump_json(artifact_lock_path, lock)
            lock_digest = hashlib.sha512(artifact_lock_path.read_bytes()).hexdigest()

            outer = cache / "minecraft_server/server-26.2.jar"
            inner = cache / "minecraft_server/server-26.2-inner.jar"
            outer.parent.mkdir(); outer.write_bytes(b"official server bundle")
            inner.write_bytes(b"exact inner vanilla server")
            minecraft_server_lock = {
                "schema": "minescape.minecraft-server-lock.v1", "version": "26.2", "type": "release",
                "size": outer.stat().st_size, "sha1": hashlib.sha1(outer.read_bytes()).hexdigest(),
                "sha512": hashlib.sha512(outer.read_bytes()).hexdigest(),
                "cache_relative": "artifacts/cache/minecraft_server/server-26.2.jar",
                "bundled_server_relative": "artifacts/cache/minecraft_server/server-26.2-inner.jar",
                "bundled_server_sha256": hashlib.sha256(inner.read_bytes()).hexdigest(),
                "bundled_server_sha512": hashlib.sha512(inner.read_bytes()).hexdigest(),
            }
            minecraft_server_lock_path = artifacts / "minecraft-server.lock.json"
            dump_json(minecraft_server_lock_path, minecraft_server_lock)

            config = json.loads((ROOT / "config/release-gates.json").read_text(encoding="utf-8"))
            config["world_epoch"] = "test-world-epoch"
            dump_json(root / "release.json", config)
            seed_policy = {"schema": "minescape.seed-policy.v1", "seed": "1", "boundaries": {"overworld": 512}, "required_structures": ["minecraft:village_plains"]}
            dump_json(root / "seed.policy.json", seed_policy)

            acquisition = {
                "schema": "minescape.acquisition-report.v1", "complete": True, "metadata_only": False,
                "entries": [{"key": key, "metadata_resolved": True, "downloaded": True,
                             "sha512_verified": True, "archive_safe": True} for key in artifact_hashes],
            }
            compatibility = {
                "schema": "minescape.compatibility-build.v1",
                "inputs": {"matcha_sha512": artifact_hashes["matcha"], "terralith_sha512": artifact_hashes["terralith"], "nullscape_sha512": artifact_hashes["nullscape"]},
                "output_sha512": "a" * 128, "collision_counts": {"terralith": 1, "nullscape": 1}, "resources": [],
                "mechanical_overrides": [
                    {"resource": "data/main/function/setup/scoreboard.mcfunction",
                     "action": "remove_matcha_heart_floor", "authority": "FabricHeartLifecycle",
                     "source_sha256": "b32f8b624aac5cc05579011f59bea6ae23d236441cd512f585240b2c1fc30ecd",
                     "output_sha256": "c" * 64},
                    {"resource": "data/main/function/mechanic/hpdown.mcfunction",
                     "action": "reset_death_score_only", "authority": "FabricHeartLifecycle",
                     "source_sha256": "e77b0ab25ba89a0053edf02f8cdb25307ff1a31b6c6dfbfe8ccbe2794a75f1c1",
                     "output_sha256": "d" * 64},
                    {"resource": "data/main/function/mechanic/process_heart_container.mcfunction",
                     "action": "replace_with_inert_comment", "authority": "FabricHeartLifecycle",
                     "source_sha256": "5c7b0cb2649f7be0bed7636146fae61b5593842ece5ad9beea15042035cf113e",
                     "output_sha256": "e" * 64},
                    {"resource": "data/main/function/mechanic/clear_heart_container.mcfunction",
                     "action": "replace_with_inert_comment", "authority": "FabricHeartLifecycle",
                     "source_sha256": "da91f3b1695f36464ce84bcfb2127f144ec45c87464ea76a488a941db14d9c7d",
                     "output_sha256": "f" * 64},
                    {"resource": "data/main/function/mechanic/set_max_hp.mcfunction",
                     "action": "replace_with_inert_comment", "authority": "FabricHeartLifecycle",
                     "source_sha256": "5e8d8fffbedb8be581b035ed4cf0b96baed81e4b8f23a3c9c9717aa3e3f58081",
                     "output_sha256": "1" * 64},
                ],
            }
            villages = {
                "schema": "minescape.village-adapter-build.v1",
                "inputs": {"matcha_sha512": artifact_hashes["matcha"],
                           "vanilla_sha512": minecraft_server_lock["bundled_server_sha512"]},
                "restored": [{"resource": str(index)} for index in range(6)], "terralith_namespace_written": False,
                "output_sha512": "b" * 128,
            }
            fishing = {
                "schema": "minescape.fishing-adapter-build.v1",
                "inputs": {"matcha_sha512": artifact_hashes["matcha"], "terralith_sha512": artifact_hashes["terralith"]},
                "hot_wet_corrections": 1, "assignment_count": 1, "output_sha512": "c" * 128,
            }
            dump_json(evidence_dir / "acquisition.json", acquisition)
            dump_json(evidence_dir / "compatibility.json", compatibility)
            dump_json(evidence_dir / "villages.json", villages)
            dump_json(evidence_dir / "fishing.json", fishing)

            completed = datetime.now(timezone.utc)
            started = completed - timedelta(minutes=5)
            common = {
                "passed": True, "world_epoch": "test-world-epoch",
                "started_at": started.isoformat().replace("+00:00", "Z"),
                "completed_at": completed.isoformat().replace("+00:00", "Z"),
                "tester": {"name": "Test Operator", "role": "release QA"}, "machine": "test-machine",
                "input_hashes": {"artifact_lock_sha512": lock_digest, "server_bundle_sha512": "d" * 128},
                "observations": ["Observed the expected behavior on the locked build."], "attachments": [],
            }
            for gate in config["gates"]:
                if gate["evaluator"] != "manual":
                    continue
                document = {"schema": gate["schema"], "gate": gate["id"], **common}
                dump_json(evidence_dir / gate["evidence"], document)
            seed = {
                "schema": "minescape.seed-evidence.v1", "gate": "seed-and-finite-resources", **common,
                "seed": "1", "boundaries": {"overworld": 512},
                "structure_counts": {"minecraft:village_plains": 1},
                "starter_village": {"natural": True, "custom_homes": False, "public_arrival": True},
                "far_rim": {"accepted": True}, "finite_resources": {"complete": True},
            }
            dump_json(evidence_dir / "seed.json", seed)
            # Even a complete directory of fresh, hash-bound generic attestations cannot claim
            # unfinished adapters exist.  The committed policy's separate code interlock is false.
            blocked = release_preflight(
                root / "release.json", evidence_dir, artifact_lock_path, minecraft_server_lock_path, cache)
            self.assertFalse(blocked["passed"])
            adapter_check = next(item for item in blocked["checks"] if item["gate"] == "adapter-completion")
            self.assertFalse(adapter_check["passed"])
            self.assertFalse(adapter_check["detail"]["promotion_permitted"])
            self.assertEqual(11, len(adapter_check["detail"]["incomplete"]))
            self.assertTrue(all(item["passed"] for item in blocked["checks"]
                                if item["gate"] != "adapter-completion"))

            # The rest of this test exercises the downstream evaluators with an explicitly opened
            # fixture interlock.  Production policy never makes this mutation.
            config["adapter_completion"]["promotion_permitted"] = True
            for component in config["adapter_completion"]["components"]:
                component["complete"] = True
            dump_json(root / "release.json", config)
            report = release_preflight(
                root / "release.json", evidence_dir, artifact_lock_path, minecraft_server_lock_path, cache)
            self.assertTrue(report["passed"])
            self.assertEqual(minecraft_server_lock["bundled_server_sha512"], report["vanilla_server_sha512"])
            seed_check = next(item for item in report["checks"] if item["gate"] == "seed-and-finite-resources")
            self.assertEqual("minescape.seed-audit.v1", seed_check["detail"]["evaluation"]["seed_audit"]["schema"])

            villages["inputs"]["vanilla_sha512"] = "0" * 128
            dump_json(evidence_dir / "villages.json", villages)
            wrong_vanilla = release_preflight(
                root / "release.json", evidence_dir, artifact_lock_path, minecraft_server_lock_path, cache)
            village_check = next(item for item in wrong_vanilla["checks"] if item["gate"] == "village-adapter")
            self.assertFalse(village_check["passed"])
            self.assertIn("official inner server archive", village_check["detail"])
            villages["inputs"]["vanilla_sha512"] = minecraft_server_lock["bundled_server_sha512"]
            dump_json(evidence_dir / "villages.json", villages)

            dump_json(evidence_dir / "controller.json", {"passed": True})
            rejected = release_preflight(
                root / "release.json", evidence_dir, artifact_lock_path, minecraft_server_lock_path, cache)
            self.assertFalse(rejected["passed"])
            controller = next(item for item in rejected["checks"] if item["gate"] == "controller-complete-matcha")
            self.assertFalse(controller["passed"])
            self.assertIn("expected evidence schema", controller["detail"])


if __name__ == "__main__":
    unittest.main()
