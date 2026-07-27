from __future__ import annotations

import copy
import hashlib
import json
import re
from pathlib import Path, PurePosixPath
from typing import Any, Iterable

from .archives import ArchiveView
from .nbt import compounds as nbt_compounds, contains_text as nbt_contains_text, loads as load_nbt
from .util import MineJammerError, canonical_json, deterministic_zip_entries, dump_json, load_json, sha256_bytes, sha512_file


COMPATIBILITY_INPUT_NAMES = ("matcha", "terralith", "nullscape")
MATCHA_MECHANICAL_OVERRIDE_ACTIONS = {
    "data/main/function/setup/scoreboard.mcfunction": "remove_matcha_heart_floor",
    "data/main/function/mechanic/hpdown.mcfunction": "reset_death_score_only",
    "data/main/function/mechanic/process_heart_container.mcfunction": "replace_with_inert_comment",
    "data/main/function/mechanic/clear_heart_container.mcfunction": "replace_with_inert_comment",
    "data/main/function/mechanic/set_max_hp.mcfunction": "replace_with_inert_comment",
}
MATCHA_HEART_FLOOR_LINE = (
    b"execute at @a[scores={Hearts=..20}] run scoreboard players set @p Hearts 20\r\n"
)


def _mechanical_override_replacement(resource: str, source: bytes) -> bytes:
    action = MATCHA_MECHANICAL_OVERRIDE_ACTIONS[resource]
    if action == "remove_matcha_heart_floor":
        if source.count(MATCHA_HEART_FLOOR_LINE) != 1:
            raise MineJammerError("reviewed Matcha setup no longer has exactly one Hearts<=20 floor reset")
        return source.replace(
            MATCHA_HEART_FLOOR_LINE,
            b"# MineScape Compatibility: FabricHeartLifecycle owns initial and restored heart state.\r\n",
        )
    if action == "reset_death_score_only":
        return (
            b"# MineScape Compatibility: FabricHeartLifecycle is the sole death-heart authority.\n"
            b"scoreboard players set @a[scores={deaths=1..}] deaths 0\n"
        )
    authority = "Crystal Heart" if "heart_container" in resource else "maximum-health"
    return (
        f"# MineScape Compatibility: inert; FabricHeartLifecycle is the sole {authority} authority.\n"
    ).encode("utf-8")


def _reviewed_compatibility_inputs(config: dict[str, Any], archives: dict[str, str | Path]) -> dict[str, str]:
    reviewed = config.get("reviewed_input_sha512")
    if not isinstance(reviewed, dict) or set(reviewed) != set(COMPATIBILITY_INPUT_NAMES):
        raise MineJammerError("compatibility policy must review the exact Matcha, Terralith, and Nullscape SHA-512s")
    actual = {name: sha512_file(archives[name]) for name in COMPATIBILITY_INPUT_NAMES}
    for name in COMPATIBILITY_INPUT_NAMES:
        expected = reviewed.get(name)
        if not isinstance(expected, str) or re.fullmatch(r"[0-9a-f]{128}", expected) is None:
            raise MineJammerError(f"compatibility policy has an invalid reviewed {name} SHA-512")
        if expected != actual[name]:
            raise MineJammerError(f"compatibility policy was reviewed against a different {name} archive")
    return actual


def _reviewed_mechanical_overrides(config: dict[str, Any], matcha_view: ArchiveView) -> tuple[dict[str, bytes], list[dict[str, Any]]]:
    configured = config.get("mechanical_overrides")
    if not isinstance(configured, list):
        raise MineJammerError("compatibility policy must contain the reviewed Matcha mechanical overrides")
    by_resource: dict[str, dict[str, Any]] = {}
    for item in configured:
        if not isinstance(item, dict) or not isinstance(item.get("resource"), str):
            raise MineJammerError("compatibility mechanical override is malformed")
        resource = item["resource"]
        if resource in by_resource:
            raise MineJammerError(f"compatibility mechanical override is duplicated: {resource}")
        by_resource[resource] = item
    if set(by_resource) != set(MATCHA_MECHANICAL_OVERRIDE_ACTIONS):
        raise MineJammerError("compatibility policy must review exactly the five Matcha heart-mechanic overrides")

    entries: dict[str, bytes] = {}
    provenance: list[dict[str, Any]] = []
    for resource in sorted(MATCHA_MECHANICAL_OVERRIDE_ACTIONS):
        item = by_resource[resource]
        action = MATCHA_MECHANICAL_OVERRIDE_ACTIONS[resource]
        if item.get("action") != action:
            raise MineJammerError(f"unsupported compatibility mechanical override action for {resource}")
        expected = item.get("source_sha256")
        if not isinstance(expected, str) or re.fullmatch(r"[0-9a-f]{64}", expected) is None:
            raise MineJammerError(f"compatibility policy has an invalid source SHA-256 for {resource}")
        source = matcha_view.read(resource)
        actual = sha256_bytes(source)
        if actual != expected:
            raise MineJammerError(f"reviewed Matcha mechanical source changed: {resource}")
        replacement = _mechanical_override_replacement(resource, source)
        entries[resource] = replacement
        provenance.append({
            "resource": resource,
            "action": action,
            "source_sha256": actual,
            "output_sha256": sha256_bytes(replacement),
            "authority": "FabricHeartLifecycle",
        })
    return entries, provenance


def _pack_meta(source: ArchiveView, description: str) -> bytes:
    meta = source.json("pack.mcmeta")
    if not isinstance(meta, dict) or not isinstance(meta.get("pack"), dict):
        raise MineJammerError(f"invalid pack.mcmeta in {source.source.name}")
    meta = copy.deepcopy(meta)
    meta["pack"]["description"] = description
    return canonical_json(meta)


def _pointer_tokens(pointer: str) -> list[str]:
    if pointer == "":
        return []
    if not pointer.startswith("/"):
        raise MineJammerError(f"JSON pointer must start with '/': {pointer}")
    return [part.replace("~1", "/").replace("~0", "~") for part in pointer[1:].split("/")]


def pointer_get(document: Any, pointer: str) -> Any:
    current = document
    for token in _pointer_tokens(pointer):
        if isinstance(current, list):
            try:
                current = current[int(token)]
            except (ValueError, IndexError) as error:
                raise MineJammerError(f"missing list pointer {pointer}") from error
        elif isinstance(current, dict) and token in current:
            current = current[token]
        else:
            raise MineJammerError(f"missing object pointer {pointer}")
    return copy.deepcopy(current)


def pointer_set(document: Any, pointer: str, value: Any) -> Any:
    tokens = _pointer_tokens(pointer)
    if not tokens:
        return copy.deepcopy(value)
    current = document
    for token in tokens[:-1]:
        if isinstance(current, list):
            current = current[int(token)]
        elif isinstance(current, dict) and token in current:
            current = current[token]
        else:
            raise MineJammerError(f"destination parent is absent for {pointer}")
    final = tokens[-1]
    if isinstance(current, list):
        index = int(final)
        if index < 0 or index >= len(current):
            raise MineJammerError(f"destination index is absent for {pointer}")
        current[index] = copy.deepcopy(value)
    elif isinstance(current, dict):
        current[final] = copy.deepcopy(value)
    else:
        raise MineJammerError(f"destination is not a container for {pointer}")
    return document


def _json_differences(left: Any, right: Any, pointer: str = "") -> list[dict[str, Any]]:
    if type(left) is not type(right):
        return [{"pointer": pointer, "left": left, "right": right}]
    if isinstance(left, dict):
        result: list[dict[str, Any]] = []
        for key in sorted(set(left) | set(right)):
            escaped = key.replace("~", "~0").replace("/", "~1")
            child = f"{pointer}/{escaped}"
            if key not in left:
                result.append({"pointer": child, "left": "<absent>", "right": right[key]})
            elif key not in right:
                result.append({"pointer": child, "left": left[key], "right": "<absent>"})
            else:
                result.extend(_json_differences(left[key], right[key], child))
        return result
    if isinstance(left, list):
        if left == right:
            return []
        return [{"pointer": pointer, "left": left, "right": right}]
    return [] if left == right else [{"pointer": pointer, "left": left, "right": right}]


def compatibility_candidates(matcha: str | Path, terralith: str | Path, nullscape: str | Path) -> dict[str, Any]:
    with ArchiveView(matcha) as matcha_view, ArchiveView(terralith) as terra_view, ArchiveView(nullscape) as null_view:
        resources = []
        for path in matcha_view.names:
            if not path.startswith("data/minecraft/worldgen/biome/") or not path.endswith(".json"):
                continue
            base_name = "nullscape" if null_view.has(path) else "terralith" if terra_view.has(path) else None
            if not base_name:
                continue
            base_view = null_view if base_name == "nullscape" else terra_view
            resources.append({
                "resource": path,
                "base_owner": base_name,
                "base_sha256": sha256_bytes(base_view.read(path)),
                "matcha_sha256": sha256_bytes(matcha_view.read(path)),
                "differences": _json_differences(base_view.json(path), matcha_view.json(path)),
            })
    return {
        "schema": "minescape.compatibility-candidates.v1",
        "inputs": {
            "matcha_sha512": sha512_file(matcha), "terralith_sha512": sha512_file(terralith),
            "nullscape_sha512": sha512_file(nullscape),
        },
        "collision_count": len(resources),
        "resources": resources,
    }


def build_compatibility(matcha: str | Path, terralith: str | Path, nullscape: str | Path,
                        config_path: str | Path, output: str | Path, report_path: str | Path) -> dict[str, Any]:
    config = load_json(config_path)
    if config.get("schema") != "minescape.compatibility-policy.v1":
        raise MineJammerError("unsupported compatibility policy")
    if not config.get("allowlist_reviewed"):
        raise MineJammerError("compatibility delta allowlist has not been explicitly reviewed")
    input_hashes = _reviewed_compatibility_inputs(config, {
        "matcha": matcha, "terralith": terralith, "nullscape": nullscape,
    })
    expected = config.get("expected_collisions", {})
    operations_by_resource: dict[str, list[dict[str, Any]]] = {}
    for operation in config.get("matcha_delta_allowlist", []):
        resource = operation.get("resource")
        if not resource or resource in operations_by_resource:
            raise MineJammerError("each allowlisted resource must appear exactly once")
        operations_by_resource[resource] = operation.get("copies", [])
    entries: dict[str, bytes] = {}
    provenance: list[dict[str, Any]] = []
    mechanical_provenance: list[dict[str, Any]] = []
    with ArchiveView(matcha) as matcha_view, ArchiveView(terralith) as terra_view, ArchiveView(nullscape) as null_view:
        base_counts = {"terralith": 0, "nullscape": 0}
        collided: set[str] = set()
        for resource in matcha_view.names:
            if not resource.startswith("data/minecraft/worldgen/biome/") or not resource.endswith(".json"):
                continue
            base_name = "nullscape" if null_view.has(resource) else "terralith" if terra_view.has(resource) else None
            if not base_name:
                continue
            collided.add(resource)
            base_counts[base_name] += 1
            base_view = null_view if base_name == "nullscape" else terra_view
            base_doc = base_view.json(resource)
            matcha_doc = matcha_view.json(resource)
            applied = []
            for operation in operations_by_resource.get(resource, []):
                source_pointer = operation["from"]
                target_pointer = operation.get("to", source_pointer)
                value = pointer_get(matcha_doc, source_pointer)
                pointer_set(base_doc, target_pointer, value)
                applied.append({"from": source_pointer, "to": target_pointer, "value_sha256": sha256_bytes(canonical_json(value))})
            entries[resource] = canonical_json(base_doc)
            provenance.append({
                "resource": resource, "base_owner": base_name,
                "base_sha256": sha256_bytes(base_view.read(resource)), "matcha_deltas": applied,
                "output_sha256": sha256_bytes(entries[resource]),
            })
        if base_counts != expected:
            raise MineJammerError(f"worldgen collision count changed: expected {expected}, got {base_counts}")
        unknown = sorted(set(operations_by_resource) - collided)
        if unknown:
            raise MineJammerError(f"allowlist references non-collided resources: {unknown}")
        mechanical_entries, mechanical_provenance = _reviewed_mechanical_overrides(config, matcha_view)
        entries.update(mechanical_entries)
        entries["pack.mcmeta"] = _pack_meta(terra_view, "MineScape exact-version Stardust-base compatibility overlay")
    report = {
        "schema": "minescape.compatibility-build.v1", "policy": str(Path(config_path).name),
        "inputs": {f"{name}_sha512": digest for name, digest in input_hashes.items()},
        "collision_counts": base_counts, "resources": provenance,
        "mechanical_overrides": mechanical_provenance,
    }
    entries["minescape-provenance.json"] = canonical_json(report)
    deterministic_zip_entries(entries, Path(output))
    report["output_sha512"] = sha512_file(output)
    dump_json(report_path, report)
    return report


VILLAGE_RESOURCES = (
    "data/minecraft/worldgen/structure/village_plains.json",
    "data/minecraft/worldgen/structure/village_desert.json",
    "data/minecraft/worldgen/structure/village_savanna.json",
    "data/minecraft/worldgen/structure/village_snowy.json",
    "data/minecraft/worldgen/structure/village_taiga.json",
    "data/minecraft/worldgen/structure_set/villages.json",
)


def build_village_adapter(vanilla: str | Path, matcha: str | Path, output: str | Path, report_path: str | Path) -> dict[str, Any]:
    entries: dict[str, bytes] = {}
    restored = []
    with ArchiveView(vanilla) as vanilla_view, ArchiveView(matcha) as matcha_view:
        for resource in VILLAGE_RESOURCES:
            payload = vanilla_view.read(resource)
            entries[resource] = payload
            restored.append({"resource": resource, "vanilla_sha256": sha256_bytes(payload), "matcha_overrode": matcha_view.has(resource)})
        entries["data/main/advancement/mechanics/enter_village_plains.json"] = canonical_json({
            "parent": "main:mechanics/root", "criteria": {"disabled": {"trigger": "minecraft:impossible"}},
            "requirements": [["disabled"]],
        })
        entries["data/main/predicate/in_village.json"] = canonical_json({"condition": "minecraft:random_chance", "chance": 0.0})
        entries["data/main/predicate/not_in_village.json"] = canonical_json({"condition": "minecraft:random_chance", "chance": 1.0})
        entries["data/main/function/environmental/village_eerie_sound.mcfunction"] = b"# Disabled by MineScape Village Adapter: living villages are not haunted.\n"
        entries["pack.mcmeta"] = _pack_meta(matcha_view, "MineScape vanilla village restoration and Matcha haunting suppression")
    report = {
        "schema": "minescape.village-adapter-build.v1",
        "inputs": {"vanilla_sha512": sha512_file(vanilla) if Path(vanilla).is_file() else None, "matcha_sha512": sha512_file(matcha)},
        "restored": restored,
        "suppressed": ["minecraft:village_beta geometry reachability", "main:mechanics/enter_village_plains", "main:environmental/village_eerie_sound"],
        "terralith_namespace_written": False,
    }
    entries["minescape-provenance.json"] = canonical_json(report)
    deterministic_zip_entries(entries, Path(output))
    report["output_sha512"] = sha512_file(output)
    dump_json(report_path, report)
    return report


def _classify_biome(resource: str, biome: dict[str, Any], policy: dict[str, Any]) -> str:
    identifier = PurePosixPath(resource).stem
    for pattern in policy.get("no_fish_patterns", []):
        if re.search(pattern, resource):
            return "no_fish"
    for climate, patterns in policy.get("name_overrides", {}).items():
        if any(re.search(pattern, identifier) for pattern in patterns):
            return climate
    temperature = float(biome.get("temperature", 0.5))
    downfall = float(biome.get("downfall", 0.5))
    thresholds = policy["freshwater_thresholds"]
    if temperature <= thresholds["cold_max"]:
        return "freshwater_cold"
    if temperature <= thresholds["cool_max"]:
        return "freshwater_cool"
    if temperature <= thresholds["temperate_max"]:
        return "freshwater_temperate"
    return "freshwater_hot_wet" if downfall >= thresholds["hot_wet_min_downfall"] else "freshwater_hot_dry"


def _correct_hot_wet(document: Any) -> int:
    corrections = 0
    if isinstance(document, dict):
        if document.get("value") == "minecraft:gameplay/fishing/freshwater_hot_wet":
            def replace(node: Any) -> None:
                nonlocal corrections
                if isinstance(node, dict):
                    if node.get("biomes") == "#minecraft:freshwater_hot_dry":
                        node["biomes"] = "#minecraft:freshwater_hot_wet"
                        corrections += 1
                    for child in node.values():
                        replace(child)
                elif isinstance(node, list):
                    for child in node:
                        replace(child)
            replace(document)
            return corrections
        for child in document.values():
            corrections += _correct_hot_wet(child)
    elif isinstance(document, list):
        for child in document:
            corrections += _correct_hot_wet(child)
    return corrections


def build_fishing_adapter(matcha: str | Path, terralith: str | Path, config_path: str | Path,
                          output: str | Path, report_path: str | Path) -> dict[str, Any]:
    policy = load_json(config_path)
    if policy.get("schema") != "minescape.fishing-climates.v1":
        raise MineJammerError("unsupported fishing climate policy")
    assignments: dict[str, list[str]] = {name: [] for name in policy["climates"]}
    assignments["no_fish"] = []
    with ArchiveView(matcha) as matcha_view, ArchiveView(terralith) as terra_view:
        resources = [name for name in terra_view.names if name.startswith("data/terralith/worldgen/biome/") and name.endswith(".json")]
        for resource in resources:
            identifier = "terralith:" + resource.split("data/terralith/worldgen/biome/", 1)[1][:-5]
            climate = _classify_biome(resource, terra_view.json(resource), policy)
            if climate not in assignments:
                raise MineJammerError(f"unknown climate {climate} for {identifier}")
            assignments[climate].append(identifier)
        if sum(len(values) for values in assignments.values()) != len(resources):
            raise MineJammerError("not every Terralith biome received exactly one fishing classification")
        entries: dict[str, bytes] = {}
        for climate in policy["climates"]:
            entries[f"data/minecraft/tags/worldgen/biome/{climate}.json"] = canonical_json({
                "replace": False, "values": sorted(assignments[climate]),
            })
        fishing_path = "data/minecraft/loot_table/gameplay/fishing.json"
        fishing = matcha_view.json(fishing_path)
        corrections = _correct_hot_wet(fishing)
        if corrections != 1:
            raise MineJammerError(f"expected exactly one hot-wet predicate correction, found {corrections}")
        entries[fishing_path] = canonical_json(fishing)
        entries["pack.mcmeta"] = _pack_meta(matcha_view, "MineScape Terralith fishing climates and frozen Matcha hot-wet correction")
    report = {
        "schema": "minescape.fishing-adapter-build.v1",
        "inputs": {"matcha_sha512": sha512_file(matcha), "terralith_sha512": sha512_file(terralith)},
        "hot_wet_corrections": corrections,
        "assignment_count": sum(len(values) for values in assignments.values()),
        "assignments": {name: sorted(values) for name, values in assignments.items()},
    }
    entries["minescape-provenance.json"] = canonical_json(report)
    deterministic_zip_entries(entries, Path(output))
    report["output_sha512"] = sha512_file(output)
    dump_json(report_path, report)
    return report


def _resource_id(path: str, kind: str) -> str:
    match = re.fullmatch(rf"data/([^/]+)/{re.escape(kind)}/(.+)\.json", path)
    if not match:
        raise MineJammerError(f"cannot derive resource id from {path}")
    return f"{match.group(1)}:{match.group(2)}"


def _loot_table_path(identifier: str) -> str:
    namespace, value = identifier.split(":", 1)
    return f"data/{namespace}/loot_table/{value}.json"


def generate_loot_adapter(matcha: str | Path, terralith: str | Path, config_path: str | Path,
                          output: str | Path) -> dict[str, Any]:
    policy = load_json(config_path)
    if policy.get("schema") != "minescape.loot-adapter-policy.v1":
        raise MineJammerError("unsupported loot adapter policy")
    mapping = policy["structures"]
    with ArchiveView(matcha) as matcha_view, ArchiveView(terralith) as terra_view:
        structure_paths = [name for name in terra_view.names if name.startswith("data/terralith/worldgen/structure/") and name.endswith(".json")]
        structures = sorted(_resource_id(path, "worldgen/structure") for path in structure_paths)
        if set(structures) != set(mapping):
            missing = sorted(set(structures) - set(mapping))
            stale = sorted(set(mapping) - set(structures))
            raise MineJammerError(f"loot mapping is not exhaustive; missing={missing}, stale={stale}")
        locked_mapping = {}
        for structure in structures:
            choice = mapping[structure]
            table = choice.get("matcha_bonus_table")
            if table and not matcha_view.has(_loot_table_path(table)):
                raise MineJammerError(f"Matcha bonus table does not exist for {structure}: {table}")
            locked_mapping[structure] = {**choice, "eligible": table is not None}
    result = {
        "schema": "minescape.loot-adapter.lock.v1",
        "version": policy["version"], "world_seed": str(policy["world_seed"]),
        "inputs": {"matcha_sha512": sha512_file(matcha), "terralith_sha512": sha512_file(terralith)},
        "selection": {
            "algorithm": "sha256-v1",
            "material": "world_seed\\0dimension\\0start_chunk_x\\0start_chunk_z\\0structure_id\\0adapter_version",
            "container_rule": "lowest unsigned digest among sorted eligible container block positions",
            "application": "one post-upstream reference roll, persisted before delivery; never reroll",
        },
        "forbidden_items": policy["forbidden_items"],
        "structures": locked_mapping,
    }
    dump_json(output, result)
    return result


def _walk_dicts(value: Any) -> Iterable[dict[str, Any]]:
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _walk_dicts(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk_dicts(child)


def generate_food_normalizer(matcha: str | Path, terralith: str | Path, output: str | Path) -> dict[str, Any]:
    candidates: dict[str, list[dict[str, bytes | Any]]] = {}
    identity_aliases = {"minecraft:cooked_porkchop": "cooked_pork"}
    terralith_occurrences: dict[str, set[str]] = {}
    with ArchiveView(matcha) as matcha_view, ArchiveView(terralith) as terra_view:
        for path in matcha_view.names:
            if not path.startswith("data/minecraft/loot_table/food/") or not path.endswith(".json"):
                continue
            if not matcha_view.read(path).strip():
                continue
            document = matcha_view.json(path)
            for node in _walk_dicts(document):
                if node.get("type") != "minecraft:item" or not isinstance(node.get("name"), str):
                    continue
                for function in node.get("functions", []):
                    if isinstance(function, dict) and function.get("function") == "minecraft:set_components" and isinstance(function.get("components"), dict):
                        item = node["name"]
                        component_bytes = canonical_json(function["components"])
                        candidate = {"bytes": component_bytes, "components": function["components"], "source": path}
                        if not any(existing["bytes"] == component_bytes for existing in candidates.setdefault(item, [])):
                            candidates[item].append(candidate)
        templates: dict[str, dict[str, bytes | Any]] = {}
        for item, choices in candidates.items():
            expected_stem = identity_aliases.get(item, item.split(":", 1)[-1])
            identity_choices = [choice for choice in choices if PurePosixPath(str(choice["source"])).stem == expected_stem]
            if len(identity_choices) == 1:
                templates[item] = identity_choices[0]
        for path in terra_view.names:
            if not path.startswith("data/terralith/loot_table/") or not path.endswith(".json"):
                continue
            if not terra_view.read(path).strip():
                continue
            for node in _walk_dicts(terra_view.json(path)):
                item = node.get("name") if node.get("type") == "minecraft:item" else None
                if item in templates:
                    has_components = any(
                        isinstance(function, dict) and function.get("function") == "minecraft:set_components"
                        for function in node.get("functions", [])
                    )
                    if not has_components:
                        terralith_occurrences.setdefault(item, set()).add(path)
    mappings = {
        item: {"components": templates[item]["components"], "matcha_source": templates[item]["source"], "terralith_tables": sorted(paths)}
        for item, paths in sorted(terralith_occurrences.items())
    }
    result = {
        "schema": "minescape.food-normalizer.lock.v1",
        "inputs": {"matcha_sha512": sha512_file(matcha), "terralith_sha512": sha512_file(terralith)},
        "rule": "attach exact components only; preserve item id, count, slot, probability, functions, and upstream roll",
        "mappings": mappings,
    }
    dump_json(output, result)
    return result


def _curio_stack_candidates(matcha_view: ArchiveView) -> dict[str, list[dict[str, Any]]]:
    markers = {
        "cyan_rose_classic": "Cyan Rose", "rose_classic": "Rose",
        "porkchop_classic": "Classic Porkchop", "dry_hands": "Dry Hands",
    }
    found: dict[str, list[dict[str, Any]]] = {name: [] for name in markers.values()}
    for resource in matcha_view.names:
        if not resource.startswith("data/minecraft/structure/village_beta/buildings/") or not resource.endswith(".nbt"):
            continue
        root = load_nbt(matcha_view.read(resource))
        for marker, label in markers.items():
            for compound in nbt_compounds(root):
                if not nbt_contains_text(compound, marker):
                    continue
                if "id" not in compound or not any(key in compound for key in ("count", "Count")):
                    continue
                stack = {key: value for key, value in compound.items() if key not in {"Slot", "slot"}}
                fingerprint = sha256_bytes(canonical_json(stack))
                existing = next((item for item in found[label] if item["fingerprint"] == fingerprint), None)
                if existing:
                    existing["occurrences"] += 1
                    if resource not in existing["source_templates"]:
                        existing["source_templates"].append(resource)
                else:
                    found[label].append({"stack": stack, "fingerprint": fingerprint, "occurrences": 1, "source_templates": [resource]})
    return found


def generate_curio_spec(matcha: str | Path, output: str | Path) -> dict[str, Any]:
    with ArchiveView(matcha) as matcha_view:
        candidates = _curio_stack_candidates(matcha_view)
    missing = [label for label, values in candidates.items() if not values]
    if missing:
        raise MineJammerError(f"could not extract exact beta-village curio stacks: {missing}")
    def select(label: str, count: int, occurrences: int = 1) -> dict[str, Any]:
        matches = [entry for entry in candidates[label] if int(entry["stack"].get("count", entry["stack"].get("Count", 0))) == count and entry["occurrences"] == occurrences]
        if len(matches) != 1:
            raise MineJammerError(f"expected one exact {label} stack count={count} occurrences={occurrences}; found {len(matches)}")
        return matches[0]
    cyan = select("Cyan Rose", 1)
    rose = select("Rose", 3)
    pork = select("Classic Porkchop", 1, 3)
    disc = select("Dry Hands", 1)
    entries = [
        {"weight": 19, "type": "loot_table", "value": "minecraft:archaeology/village_plains"},
        {"weight": 4, "type": "exact_matcha_stack", "identity": "Cyan Rose", "source": cyan},
        {"weight": 1, "type": "exact_matcha_bundle", "source": {
            "Rose x3": rose, "Classic Porkchop x3": pork, "Dry Hands x1": disc,
        }},
    ]
    result = {
        "schema": "minescape.curio-cache.v1", "resource_id": "minescape:matcha_village_relics",
        "ratio": "19:4:1", "total_weight": 24, "entries": entries,
        "matcha_sha512": sha512_file(matcha),
        "assignment": "sha256(world_seed\\0dimension\\0village_start_chunk) mod 24; one cache maximum per restored vanilla village start",
        "starter_guarantee": False, "replaces_upstream_loot": False,
    }
    dump_json(output, result)
    return result


def build_resource_only(frozen_matcha: str | Path, candidate_matcha: str | Path, output: str | Path,
                        report_path: str | Path, allow_missing_path: str | Path | None = None) -> dict[str, Any]:
    policy = load_json(allow_missing_path) if allow_missing_path else {
        "schema": "minescape.resource-delta-policy.v1",
        "approved_changes": [], "approved_additions": [], "approved_removals": [],
    }
    if policy.get("schema") != "minescape.resource-delta-policy.v1":
        raise MineJammerError("unsupported Matcha resource-delta policy")

    def approvals(section: str, required_hashes: tuple[str, ...]) -> dict[str, dict[str, Any]]:
        result: dict[str, dict[str, Any]] = {}
        for item in policy.get(section, []):
            if (not isinstance(item, dict) or not isinstance(item.get("path"), str)
                    or not (item["path"].startswith("assets/") or item["path"] == "pack.png")):
                raise MineJammerError(f"{section} entries require an assets/** path or pack.png")
            if not isinstance(item.get("reason"), str) or not item["reason"].strip():
                raise MineJammerError(f"{section} approval requires a written reason: {item['path']}")
            for field in required_hashes:
                value = item.get(field)
                if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{128}", value):
                    raise MineJammerError(f"{section} approval requires lowercase SHA-512 field {field}: {item['path']}")
            if item["path"] in result:
                raise MineJammerError(f"duplicate resource approval: {item['path']}")
            result[item["path"]] = item
        return result

    approved_changes = approvals("approved_changes", ("frozen_sha512", "candidate_sha512"))
    approved_additions = approvals("approved_additions", ("candidate_sha512",))
    approved_removals = approvals("approved_removals", ("frozen_sha512",))
    approved_paths = set(approved_changes) | set(approved_additions) | set(approved_removals)
    if len(approved_paths) != len(approved_changes) + len(approved_additions) + len(approved_removals):
        raise MineJammerError("a resource path may be approved in exactly one delta category")

    def digest(payload: bytes) -> str:
        return hashlib.sha512(payload).hexdigest()

    entries: dict[str, bytes] = {}
    with ArchiveView(frozen_matcha) as frozen, ArchiveView(candidate_matcha) as candidate:
        frozen_assets = {name for name in frozen.names if name.startswith("assets/") or name == "pack.png"}
        candidate_assets = {name for name in candidate.names if name.startswith("assets/") or name == "pack.png"}
        changed = {name for name in frozen_assets & candidate_assets if frozen.read(name) != candidate.read(name)}
        added = candidate_assets - frozen_assets
        removed = frozen_assets - candidate_assets
        actual_paths = changed | added | removed
        if actual_paths != approved_paths:
            unapproved = sorted(actual_paths - approved_paths)
            stale = sorted(approved_paths - actual_paths)
            raise MineJammerError(f"resource delta review mismatch; unapproved={unapproved[:10]}, stale={stale[:10]}")

        for name, approval in approved_changes.items():
            if digest(frozen.read(name)) != approval["frozen_sha512"] or digest(candidate.read(name)) != approval["candidate_sha512"]:
                raise MineJammerError(f"approved changed resource bytes do not match locked hashes: {name}")
        for name, approval in approved_additions.items():
            if digest(candidate.read(name)) != approval["candidate_sha512"]:
                raise MineJammerError(f"approved added resource bytes do not match locked hash: {name}")
        for name, approval in approved_removals.items():
            if digest(frozen.read(name)) != approval["frozen_sha512"]:
                raise MineJammerError(f"approved removed resource bytes do not match locked hash: {name}")

        # Frozen Matcha 1.02 is always the baseline. Only exact, individually
        # reviewed candidate deltas can replace, add, or remove visual members.
        for name in sorted(frozen_assets):
            entries[name] = frozen.read(name)
        for name in sorted(approved_changes | approved_additions):
            entries[name] = candidate.read(name)
        for name in approved_removals:
            entries.pop(name, None)
        for name in candidate.names:
            leaf = PurePosixPath(name).name.casefold()
            if leaf.startswith(("license", "licence", "copying", "credits", "authors", "changelog")):
                entries[name] = candidate.read(name)
        candidate_meta = candidate.json("pack.mcmeta")
        if not isinstance(candidate_meta, dict) or not isinstance(candidate_meta.get("pack"), dict):
            raise MineJammerError("candidate Matcha archive has invalid pack.mcmeta")
        safe_pack = copy.deepcopy(candidate_meta["pack"])
        safe_pack["description"] = "MineScape approved Matcha resource-only derivative"
        entries["pack.mcmeta"] = canonical_json({"pack": safe_pack})
        report = {
            "schema": "minescape.matcha-resource-derivative.v1",
            "frozen_gameplay_sha512": sha512_file(frozen_matcha), "candidate_archive_sha512": sha512_file(candidate_matcha),
            "data_members_emitted": 0, "frozen_asset_count": len(frozen_assets), "candidate_asset_count": len(candidate_assets),
            "approved_changed_assets": sorted(changed), "approved_added_assets": sorted(added),
            "approved_removed_assets": sorted(removed),
            "strict_reference_guard": "frozen 1.02 baseline plus only individually reviewed, exact-hash candidate deltas",
        }
    entries["minescape-provenance.json"] = canonical_json(report)
    deterministic_zip_entries(entries, Path(output))
    report["output_sha512"] = sha512_file(output)
    dump_json(report_path, report)
    return report
