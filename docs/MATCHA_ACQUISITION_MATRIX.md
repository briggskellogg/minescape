# Matcha acquisition and Terralith enrichment contract

This audit is pinned to Matcha Flavoured 1.02 and Terralith 2.6.4.

| Archive | SHA-512 |
|---|---|
| Matcha 1.02 | `12dc5a600d473f06a44a4370c02b404a0260adf696a591a3fbb0f135bca613ffba4f9990f625746375fa9181bbb300eeffd9cc420e6a50f91a48c21314c81505` |
| Terralith 2.6.4 | `f5283548323149ecd3f218b8efbe03c1c582df3f40b6bb3b095ba9e4cac0c87108b98adabd6ebd3a8037ec239773a253f75b8bee7db52e6f5663bf9dc63c2ea3` |

## What restoring living vanilla villages unlocks

Matcha's beta-village templates never call the 14 rich village chest tables. Restored vanilla villages do, so they are the correct geometry for Matcha's living-village economy.

| Matcha override | Important results |
|---|---|
| Armorer | Healing bread, copper/iron/emerald, Matcha iron equipment |
| Butcher | Meat, bone, coal, emerald |
| Cartographer | Maps, paper, compass, healing bread, bundle chance |
| Climate houses | Crops/materials/saplings plus Matcha-component foods and bundle chances |
| Fisher | Ordinary supplies plus exactly three complete biome-sensitive Matcha fishing rolls |
| Mason | Clay/stone/quartz/dyes/emerald |
| Tannery | Healing bread, leather/saddle, Matcha sturdy-leather equipment |
| Temple | Minerals/food plus guaranteed Tanakh, Avesta, or Quran |
| Toolsmith | Diamonds/metals, healing bread, Matcha iron equipment |
| Weaponsmith | Diamonds/metals/obsidian/horse armor, healing food, Matcha iron equipment |

Matcha's 1,023 recipes (including 122 food recipes), 235 villager trades, and fishing system remain global. They do not depend on beta-village geometry. Rare cooked foods are principally made through those recipes; fisher containers/fishing provide raw fish and the recipe path provides the prepared version.

## What beta villages actually contain

The 16 templates contain 48 suspicious-gravel positions and two fixed-inventory set pieces. Ordinary blocks/resources remain obtainable elsewhere. Clay Fetishes remain obtainable through Matcha desert-well and rare trail-ruins archaeology.

| Beta-only stack identity | Preservation route |
|---|---|
| Cyan Rose | Namespaced restored-vanilla-village relic cache |
| Rose | Same |
| Classic Porkchop | Same |
| Dry Hands record | Same |

Each qualifying restored vanilla village receives at most one deterministic cache. Its single result reproduces the source building-pool weights: 19 calls Matcha's unchanged `minecraft:archaeology/village_plains`; 4 emits the exact Cyan Rose stack; 1 emits Rose ×3, Classic Porkchop ×3 as three max-stack-one stacks, and Dry Hands ×1. It never targets a Terralith start or guarantees the starter village.

The fixed beta template also held diamonds, iron ore, coal, building blocks, and a damaged wooden pickaxe. Those identities remain naturally available and are not duplicated by the relic bridge.

## Matcha found-only resources that already survive

| Resource | Frozen remaining source |
|---|---|
| Amber | Rare trail-ruins archaeology |
| Avesta | Desert pyramid and living-village temple |
| Crystal Heart | Stronghold corridor, simple dungeon, fishing treasure, ancient city |
| Divine Comedy / Paradise Lost | Buried and shipwreck treasure |
| Divine Fragment variants | Piglin brute, evoker, elder guardian, stronghold corridor, ancient city |
| Enoch | Simple dungeon |
| Gnocchi Recipe | Abandoned mineshaft |
| Opal | Elder guardian |
| Quran / Tanakh | Living-village temple |
| Ruby | Bastions |
| Solomon | Stronghold library |
| Topaz | Ancient city |
| Cheerful/Mournful Clay Fetish | Desert well and rare trail ruins |

## Terralith's natural Matcha composition

These are Stardust-authored shared references. They remain untouched and resolve through Matcha because Matcha owns the corresponding `minecraft:*` table at runtime.

| Terralith site | Shared reference | Template uses |
|---|---|---:|
| Valley Lodge | Matcha taiga-village chest | 8 barrels |
| Desert Outpost | Matcha desert-well archaeology | 9 brushables |
| Desert fortified cartographer | Matcha desert-well archaeology | 13 brushables |
| Terralith rubble | Matcha trail-ruins common | 80 brushables |
| Large rubble | Matcha trail-ruins rare | 6 brushables |
| Desert fortified cartographer | Matcha simple-dungeon chest | 2 chests |

## User-selected Terralith enrichment

Every Terralith archive/template/table and every original result remains intact. Bridge then selects at most one eligible container for the complete structure start and performs one source-appropriate reference roll:

| Terralith family | Additional frozen Matcha source |
|---|---|
| Fortified villages | Climate/profession village table |
| Surface lodge/hut/outpost/mage site | Provisions, fishing, or village table |
| Underground work site | Abandoned-mineshaft table |
| Spire / frosted dungeon | Simple-dungeon table |
| Rubble | None; its existing archaeology already uses Matcha |

This is world-wide and deterministic, not per-player. It resolves once, persists normally, and is logged with source structure/container/table. Divine Favour and Divine Fragments are never injected. A Crystal Heart can appear only when an unmodified danger-tier Matcha table natively contains one, at that table's original odds.

Terralith tables emit some componentless food. The adapter attaches the exact frozen Matcha components to a matching base-food stack without changing item ID, count, slot, probability, enchantments, or the upstream roll.

## Fishing compatibility

Matcha's 11 fishing climate tags list only vanilla biomes. The generated compatibility layer classifies every Terralith biome into exactly one Matcha fishing climate or an explicit no-fish exception. It also corrects the audited 1.02 predicate typo that routed hot-wet freshwater fish through the hot-dry tag; that V1 output then freezes.

Three Matcha fish stubs—axolotl, blind cave fish, and blind minnow—are already unreachable upstream, and the blind fish lack complete assets/language. MineScape documents but does not invent them into the economy.

## Release invariants

- Both upstream archive hashes match this document.
- Generated packs contain no `data/terralith/**` and never override a Terralith table.
- Original Terralith results serialize identically before the separate bonus.
- At most one bonus occurs per eligible structure start, across open/restart/restore.
- All bonus/relic outcomes carry structure/container/table provenance.
- Every Matcha recipe, reachable fishing leaf, trade, advancement acquisition, and found-only resource has a demonstrated path.
- No player-specific loot multiplier exists.
