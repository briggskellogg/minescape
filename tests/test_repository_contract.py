import json
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]


class RepositoryContractTests(unittest.TestCase):
    def test_three_products_and_canonical_docs_exist(self) -> None:
        for relative in (
            "MineScape",
            "MineJammer",
            "MineDeck",
            "docs/MINESCAPE_V1_MASTER_PLAN.md",
            "docs/MINESCAPE_V1_FINAL_STACK.md",
            "docs/MATCHA_ACQUISITION_MATRIX.md",
        ):
            self.assertTrue((ROOT / relative).exists(), relative)

    def test_canonical_non_rpg_and_first_arrival_rules_are_present(self) -> None:
        plan = (ROOT / "docs/MINESCAPE_V1_MASTER_PLAN.md").read_text(encoding="utf-8")
        for phrase in (
            "no advance names, custom homes, claims, kits, or personal spawn points",
            "Complete official Terralith 2.6.4 suite",
            "all slots blocked retires that character",
            "resource-only derivative",
            "at most one eligible container",
        ):
            self.assertIn(phrase, plan)

    def test_artifact_keys_and_version_ids_are_unique(self) -> None:
        source = ROOT / "MineJammer/manifests/artifacts.json"
        self.assertTrue(source.exists(), "canonical MineJammer artifact manifest is missing")
        manifest = json.loads(source.read_text(encoding="utf-8"))
        keys = [item["key"] for item in manifest["artifacts"]]
        version_ids = [
            item["version_id"]
            for item in manifest["artifacts"]
            if item.get("provider") == "modrinth"
        ]
        self.assertEqual(len(keys), len(set(keys)))
        self.assertEqual(len(version_ids), len(set(version_ids)))

    def test_runtime_and_identity_paths_are_gitignored(self) -> None:
        ignore = (ROOT / ".gitignore").read_text(encoding="utf-8")
        for rule in ("/var/", "/backups/", "**/playerdata/", "**/fog-data/", "**/secrets.json"):
            self.assertIn(rule, ignore)

    def test_legacy_prototype_is_clearly_inactive(self) -> None:
        archive = ROOT / "legacy/bedrock-v0/ARCHIVE.md"
        self.assertTrue(archive.exists())
        self.assertIn("not installed", archive.read_text(encoding="utf-8"))

    def test_compatibility_review_is_bound_to_exact_inputs_and_five_heart_writers(self) -> None:
        artifact_lock = json.loads((ROOT / "artifacts/artifacts.lock.json").read_text(encoding="utf-8"))
        locked_hashes = {item["key"]: item["sha512"] for item in artifact_lock["artifacts"]}
        policy = json.loads((ROOT / "MineJammer/config/compatibility.policy.json").read_text(encoding="utf-8"))
        self.assertEqual(
            {key: locked_hashes[key] for key in ("matcha", "terralith", "nullscape")},
            policy["reviewed_input_sha512"],
        )
        expected = {
            "data/main/function/setup/scoreboard.mcfunction": "remove_matcha_heart_floor",
            "data/main/function/mechanic/hpdown.mcfunction": "reset_death_score_only",
            "data/main/function/mechanic/process_heart_container.mcfunction": "replace_with_inert_comment",
            "data/main/function/mechanic/clear_heart_container.mcfunction": "replace_with_inert_comment",
            "data/main/function/mechanic/set_max_hp.mcfunction": "replace_with_inert_comment",
        }
        self.assertEqual(expected, {item["resource"]: item["action"] for item in policy["mechanical_overrides"]})
        self.assertTrue(all(len(item["source_sha256"]) == 64 for item in policy["mechanical_overrides"]))

    def test_runtime_modes_and_world_border_are_managed_contracts(self) -> None:
        profile = json.loads((ROOT / "ops/runtime-profiles.json").read_text(encoding="utf-8"))
        self.assertEqual("family", profile["instances"]["MineScape"]["mode"])
        self.assertEqual("heart_qa", profile["instances"]["MineJammer"]["mode"])
        assembly = (ROOT / "ops/assemble-runtime.ps1").read_text(encoding="utf-8")
        verifier = (ROOT / "ops/verify-runtime.ps1").read_text(encoding="utf-8")
        for phrase in (
            "config\\worldborder.json5", "world_border_config_sha512",
            "enableCustomNetherBorder", "shouldLoopToOppositeBorder",
        ):
            self.assertIn(phrase, assembly)
            self.assertIn(phrase, verifier)

    def test_fresh_clone_install_builds_generated_inputs_before_runtime_assembly(self) -> None:
        install = (ROOT / "docs/INSTALL_HOST.md").read_text(encoding="utf-8")
        ordered = [
            "ops\\acquire-artifacts.ps1", "ops\\acquire-minecraft-server.ps1",
            "ops\\build-generated-packs.ps1", "ops\\assemble-runtime.ps1 -StageMineJammerLauncher",
        ]
        positions = [install.index(item) for item in ordered]
        self.assertEqual(sorted(positions), positions)
        self.assertIn("var\\MineJammer\\eula.txt", install)
        self.assertIn("Leave `var\\MineScape\\eula.txt` at `eula=false`", install)


if __name__ == "__main__":
    unittest.main()
