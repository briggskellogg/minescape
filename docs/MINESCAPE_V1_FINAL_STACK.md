# MineScape V1.0 Final Stack Decision

Status: canonical construction target; implementation is active and no world is promoted until V0 compatibility/release testing passes
Prepared: 2026-07-26
Preferred seed: `6246468738900744`

## Verdict

This is a complete first permanent world. **V1.0 is the children's first playable release.** V0.x is the disposable MineJammer laboratory used to prove V1.0; it is not an earlier family world.

MineScape does not need a future capital, dungeon pack, questline, custom NPC, custom mob, RPG class, Esquie, or paid build to become whole. Those may someday be optional discoveries, but they are not missing parts.

“Final” here means the design decision is frozen. It does not mean an untested download is trusted. V0 must resolve every dependency, archive the permitted artifacts, record filenames/URLs/licenses/creators and SHA-512 hashes, generate the private compatibility layers, and pass the acceptance suite. The resulting signed `manifest.lock.json` is the literal install manifest for the gold master.

Status meanings:

- **Release target:** required in a successful V1 gold master.
- **Gated target:** wanted, with a named fallback if exact testing fails.
- **Default:** installed for the intended profile but safely removable; never world law.
- **Optional/off:** available only when deliberately enabled.
- **MineJammer only:** construction or diagnostics; absent from ordinary family play.

## Definitive install table

| Order | Scope | Component and link | Creator/steward | Exact target | Status | Purpose and rule |
|---:|---|---|---|---|---|---|
| 1 | Base | Minecraft: Java Edition | Mojang Studios | 26.2 | Release target | The actual game and canonical vanilla structures, mobs, blocks, redstone, farms, bosses, and progression. |
| 2 | Host runtime | Private Java runtime | OpenJDK distribution chosen during build | Java 25 LTS, exact build archived | Release target | Runs the dedicated server and local tools; child Launchers may use their managed runtime. |
| 3 | Server/client loader | [Fabric](https://fabricmc.net/use/server/) | FabricMC | Loader 0.19.3; server launcher 1.1.1 | Release target | Small mod platform; no Paper world-behavior fork. |
| 4 | Server and modded clients | [Fabric API](https://modrinth.com/mod/fabric-api/version/lVXlbH4w) | FabricMC | 0.155.2+26.2, ID `lVXlbH4w` | Release target | Shared API required by the selected Fabric graph. |
| 5 | World + required resource pack | [Matcha Flavoured](https://modrinth.com/datapack/matcha-flavoured/version/RVX0a6It) | klei_wright and credited collaborators | 1.02, ID `RVX0a6It` | Release target; release-blocking QA | The sole added gameplay/economy voice: progression, food-as-healing, fishing, trades, refugees, Warding Stones, recipes, found items, sounds/models/textures, and post-dragon surface change. Its official 1.02 data freezes; later Matcha work may enter only through a tested resource-only derivative. |
| 6 | Overworld worldgen | [Terralith](https://modrinth.com/datapack/terralith/version/CzijfXJQ) | Stardust Labs / Starmute | 2.6.4 datapack, ID `CzijfXJQ` | Gated target | Complete Stardust Overworld: terrain, caves, every official structure family, native placement/templates/mobs/map behavior, and every original loot roll. No family filter or `data/terralith/**` override. |
| 7 | Nether worldgen | [Amplified Nether](https://modrinth.com/datapack/amplified-nether/version/xIayvf8F) | Stardust Labs / Starmute | 1.2.15 datapack, ID `xIayvf8F` | Release target; terrain QA | Doubled-height, dramatic Nether geography using Minecraft's own biomes, mobs, structures, items, and progression. |
| 8 | End worldgen | [Nullscape](https://modrinth.com/datapack/nullscape/version/prWWpjSv) | Stardust Labs / Starmute | 1.2.20 datapack, ID `prWWpjSv` | Gated target | Vast, alien, deliberately barren End terrain. Nullscape owns collided End geography; release stops if Matcha progression cannot coexist without erasing it. |
| 9 | World | MineScape Compatibility | First-party MineScape | Generated from the exact four world-pack inputs | Release target if the combined stack passes | Stardust-base biome merge, enumerated progression-critical Matcha deltas, Terralith-to-Matcha fishing climates, and the frozen hot-wet predicate correction; load order is never accepted as compatibility. |
| 10 | World | MineScape Village + Loot Adapters | First-party MineScape | Generated from vanilla 26.2, Matcha 1.02, Terralith 2.6.4 | Release target | Restores living vanilla villages, suppresses Matcha beta geometry/haunting, preserves its four curios at the original 19:4:1 building-pool ratio, registers civic wards, keeps every Terralith original roll, and adds at most one source-appropriate Matcha bonus per eligible Terralith start plus exact food-component normalization. |
| 11 | World | MineScape Foundations | First-party MineScape | Versioned private datapack | Release target | Stable game rules, migrations, settlement/restoration tags, release markers, edge warnings, and world invariants. |
| 12 | Host state | MineScape Settlement Registry | First-party generated state | Signed runtime manifest | Release target | Records settlement identity, bounds, census, building/ward anchors, storage policy, source hashes, and restoration state; this is state, not a downloaded mod. |
| 13 | Server + all playing clients | MineScape Bridge/Core + MineScape Client | First-party MineScape | Versioned paired Fabric modules | Release target | MineDeck control channel, server-authoritative capacity/blocked Mortal Hearts, black-heart HUD, full Crystal renewal, deterministic loot composition, exploration, ward batching, settlement classification, 1× event enforcement, inheritance, audit, and narrow authenticated administration. |
| 14 | Server/default client | [Lithium](https://modrinth.com/mod/lithium/version/UPNexAfy) | CaffeineMC | 0.25.2, ID `UPNexAfy` | Default | Invisible tick optimization; replaceable and not part of world identity. |
| 15 | Server/default client | [FerriteCore](https://modrinth.com/mod/ferrite-core/version/d5ddUdiB) | malte0811 | 9.0.0, ID `d5ddUdiB` | Default | Memory optimization; replaceable and not part of world identity. |
| 16 | Server | [World Border](https://modrinth.com/mod/world-border/version/1e9MjNpZ) + [Collective](https://modrinth.com/mod/collective/version/M75JwjyS) | Serilum | 26.2.0-4.9 + 8.39 | Release target | Conventional finite limits: Overworld ±16,384, Nether ±2,048, End ±8,192; looping/toroidal travel is explicitly off. |
| 17 | Host/server | [BlueMap](https://modrinth.com/plugin/bluemap/version/VTvifNPN) | BlueColored / BlueMap project | 5.22 Fabric, ID `VTvifNPN` | Release target after privacy QA | Private renderer behind MineDeck. It is never the authority for exploration and its raw endpoint cannot bypass fog of war. |
| 18 | Server | [Ledger](https://modrinth.com/mod/ledger/version/KpVLPOJk) | Potatoboy9999, DrexHD, and QuiltServerTools contributors | 1.3.23, ID `KpVLPOJk` | Release target after rollback QA | Block/entity audit and targeted recovery; its database is backed up with the world. |
| 19 | Server + all playing clients | [Enhanced Falling Trees](https://modrinth.com/mod/enhanced-falling-trees/version/Y0zs6l6h) + [Cloth Config](https://modrinth.com/mod/cloth-config/version/Nv3xnWXd) | addavriance; shedaniel | 0.7.1 beta + 26.2.155 | Gated target | The requested falling-tree behavior. It ships only if vanilla/Terralith trees, builds, drops, multiplayer, and performance pass; no substitute content mod is silently added. |
| 20 | Default clients | [Sodium](https://modrinth.com/mod/sodium/version/2Yom1N68) | CaffeineMC | 0.9.1, ID `2Yom1N68` | Default | Renderer performance; safely disabled/replaced per device. |
| 21 | Default clients | [ImmediatelyFast](https://modrinth.com/mod/immediatelyfast/version/uJHxuQxy) | RaphiMC | 1.16.2, ID `uJHxuQxy` | Default | Client rendering/UI optimization. |
| 22 | Default clients | [Entity Culling](https://modrinth.com/mod/entityculling/version/iiF6U3Ne) | tr7zw | 1.10.5, ID `iiF6U3Ne` | Default | Avoids rendering hidden entities; visual QA required. |
| 23 | Default clients | [Mod Menu](https://modrinth.com/mod/modmenu/version/njXb639R) + [Text Placeholder API](https://modrinth.com/mod/placeholder-api/version/NDqH16LT) | Terraformers / Prospector; Patbox | 20.0.1 + 3.1.0-beta.1+26.2, IDs `njXb639R` + `NDqH16LT` | Gated default | Makes client settings accessible; the beta dependency is admitted only with the whole row's client QA. |
| 24 | Controller clients | [Controlify](https://modrinth.com/mod/controlify/version/ww7bBOmX) + [YACL](https://modrinth.com/mod/yacl/version/cnfPzuFU) | isXander | 3.1.2 + 3.9.6 | Gated target on controller devices | Controller-first play; every required family screen must pass real controller QA. |
| 25 | Default clients | [Continuity](https://modrinth.com/mod/continuity/version/mgUN5Xz2) | Pepper_Bell / PepperCode1 | 3.0.1, ID `mgUN5Xz2` | Gated default | Restrained connected textures; client-only and removable. |
| 26 | Default clients | [Falling Leaves](https://modrinth.com/mod/fallingleaves/version/JYRSvzQW) | Fourmisain | 2.0.7, ID `JYRSvzQW`; shared Cloth Config | Gated default | Quiet forest atmosphere; no gameplay/world effect. |
| 27 | Capable clients | [Iris](https://modrinth.com/mod/iris/version/oaD6KQls) + [Complementary Reimagined](https://modrinth.com/shader/complementary-reimagined/version/yCCduG44) | coderbot, IMS, and IrisShaders contributors; EminGT | 1.11.2 + r5.8.1 | Optional/off by default | Family Clear shader preset per machine; never required and never world-critical. |
| 28 | Default clients | [Durability Tooltip](https://modrinth.com/mod/durability-tooltip/version/cvbPblt6) + [SuperMartijn642's Config Lib](https://modrinth.com/mod/supermartijn642s-config-lib/version/tg619S8t) | SuperMartijn642 | 1.1.6 + 1.1.8 | Gated default | Small, durable information improvement after Matcha/controller tooltip QA. |
| 29 | Default clients | [Better Statistics Screen](https://modrinth.com/mod/better-stats/version/Qn69b7mm) + [TCDCommons](https://modrinth.com/mod/tcdcommons/version/6YYTlCvO) | TheCSDev | 5.5.4 + 5.5.4 | Gated default | Long-world history screen after controller navigation QA. |
| 30 | Parent Steward client | [OrthoCamera](https://modrinth.com/mod/orthocamera/version/nURmQGL6) | DimasKama | 0.1.11+26.2, ID `nURmQGL6` | Gated parent-only | Orthographic planning/viewing. It grants no authority, does not count toward family fog in Steward mode, and requires Sodium culling QA. |
| 31 | Default clients | [Pick Up Notifier](https://modrinth.com/mod/pick-up-notifier/version/DWaZDkc8) + [Puzzles Lib](https://modrinth.com/mod/puzzles-lib/version/HfGQTxSR) + [Forge Config API Port](https://modrinth.com/mod/forge-config-api-port/version/rSd3GiG8) | Fuzs | 26.2.0 + 26.2.1 + 26.2.1 | Optional audition | Controller-friendly pickup clarity only if its dependency/HUD cost is worth it. |
| 32 | Clients | [Sound Physics Remastered](https://modrinth.com/mod/sound-physics-remastered/version/d8iioMMp) | henkelmax | 1.5.1 beta | Optional/off | Audition only; Matcha's deliberate soundscape gets priority. |
| 33 | Clients | [LambDynamicLights](https://modrinth.com/mod/lambdynamiclights/version/jBLH7Qy8) | LambdAurora | 4.12.2 | Optional/off | Off because portable light changes the meaning of darkness. |
| 34 | Parent keyboard/mouse client | [Mouse Tweaks](https://modrinth.com/mod/mouse-tweaks/version/jOiTBIaB) | YaLTeR | 2.31 | Optional parent-only | Useful for the parent; omitted from controller machines. |
| 35 | Server + participating clients | [Simple Voice Chat](https://modrinth.com/plugin/simple-voice-chat/version/3SOh5iiX) | henkelmax | 2.6.21 | Optional/off | Add only if separate rooms/remote family play need it; requires UDP 24454. |
| 36 | MineJammer server | [WorldEdit](https://modrinth.com/plugin/worldedit/version/j2w3GmPv) | EngineHub | 7.4.4 Fabric | MineJammer only | Construction and inspection; never ordinary production play. |
| 37 | MineJammer server | [Chunky](https://modrinth.com/plugin/chunky/version/4Eotm6ov) | pop4959 | 1.5.3 Fabric | MineJammer only | Seed tests and measured pregeneration; never blindly fills the border. |
| 38 | MineJammer client | [Camera Utils](https://modrinth.com/mod/camera-utils/version/ftzKCT0z) | henkelmax | 1.1.2 beta | MineJammer only | Inspection camera; excluded from child discovery. |
| 39 | MineJammer client/server | [JEI](https://modrinth.com/mod/jei/version/2jnbg93a) | mezz | 30.14.0.93 beta | MineJammer only | Recipe/content diagnostics; not a children's discovery browser. |
| 40 | MineJammer client | [AppleSkin](https://modrinth.com/mod/appleskin/version/uo5bAN1Y) | squeek502 | 3.0.10 | MineJammer only | Diagnostics only because vanilla hunger projections can misdescribe Matcha healing. |
| 41 | MineJammer benchmark | [Fast Noise](https://modrinth.com/mod/zfastnoise/version/GJk7sVtP) | ZenXArch | 1.0.39 | MineJammer only | Worldgen benchmark; production requires byte/parity proof and measured benefit, so it is not presently approved. |

MineDeck is Windows host software, not a Minecraft mod. Tailscale is host/Mac networking, not part of the world. Child clients do not install Terralith, Amplified Nether, Nullscape, World Border, BlueMap, Ledger, the server Java runtime, or server-side first-party packs. The exact Matcha resource pack must be loaded once—either supplied by the managed profile or enforced by the server endpoint, never duplicated ambiguously.

## MineDeck, administration, first arrival, and controller contract

MineDeck is an out-of-game Windows supervisor with a persistent tray indicator and local dashboard. It starts/stops MineScape, verifies a real Minecraft ping, manages MineJammer, backups, fog of war, pending players, characters/hearts, settlements/wards, Matcha progress, loot provenance, remote access, and the tightly audited Steward workflow. It creates no quest book, currency, home token, warp, land claim, custom NPC, or child-facing moral score. There is no arbitrary server-console page.

The parent is an ordinary Survival player during Family Play. Administrative access is a separate authenticated MineDeck identity. A conspicuous, short-lived Steward Creative lease is the only production Creative path; it snapshots and restores inventory/state, excludes future scouting from family fog, grants no permission to child UUIDs, and uses the parent-only OrthoCamera profile.

Unknown authenticated players become Pending Players. MineDeck displays their current profile and UUID for approval; no names are needed during construction. Approval writes a UUID whitelist role only. Every first join arrives at the same safe public natural path by the starter village bell with an ordinary empty inventory: no custom home, claimed building, private ward, kit, reward, teleport token, or personal spawn. Players choose/build homes in play, and ordinary beds control later respawns.

Matcha uses Minecraft's standard interfaces and has 64 displayed advancement nodes across **From Earth**, **Through Hell**, and **To Dreams**. Controlify handles normal containers/trades/recipes and its configured virtual mouse handles the advancement graph. V1 cannot ship until each child's actual controller can launch/join, navigate all three trees, pan/focus/read every node, search and craft recipes, trade, smith, use Matcha items/foods/wards, sleep, die, and respawn with keyboard/mouse disconnected. MineDeck's Progress page is a read-only parent aid; it cannot complete anything.

## Deliberately not installed

| Candidate | Decision | Why |
|---|---|---|
| [BetterEnd](https://modrinth.com/mod/betterend) and [BetterNether](https://modrinth.com/mod/betternether) | Excluded from V1 | No official 26.2 build; current projects target 26.1.x. Their shared [World Weaver](https://modrinth.com/mod/worldweaver) dependency says it is early development and may contain irreversible world-destroying bugs. More importantly, BetterX adds its own mobs, foods, materials, tools, armor, rituals, dungeons, cities, and progression. It is a second game author, not quiet terrain. |
| BCLib and World Weaver | Excluded | BetterX dependency chain; no role without BetterX and not acceptable for a permanent gold master in its current state. |
| Matcha beta villages/ruins | Suppressed | The geometry is visually wrong for this world. Matcha's economy, archaeology concepts, refugees, trades, wards, progression, and reward system remain. |
| Extra structure/dungeon/ruin packs and paid cities | Excluded | Vanilla already supplies dungeons, trial chambers, ancient cities, strongholds, monuments, mansions, fortresses, bastions, and End cities. Future great cities must remain optional authored wonders. |
| Esquie, RPG classes, quest engines, custom NPC/mob/item frameworks, ItemsAdder, MythicMobs, ModelEngine, Pokémon | Excluded | They replace children's self-authored Minecraft stories with another game's progression or a maintenance-heavy content platform. |
| [Geophilic](https://modrinth.com/datapack/geophilic) and [Tectonic](https://modrinth.com/datapack/tectonic) | Excluded | Redundant worldgen authors beside Terralith. |
| [Distant Horizons](https://modrinth.com/mod/distanthorizons) | Excluded from V1 | Beta/LOD complexity and long-distance rendering work against fog-of-war discovery. |
| [Indium](https://modrinth.com/mod/indium) | Excluded | Obsolete for the selected modern Sodium path. |
| Noisium/NoisiumForked | Excluded | Retiring worldgen optimization is not worth permanent risk. |
| Fresh Animations, EMF, ETF, and unaudited texture packs | Excluded/deferred | Matcha has extensive entity/item/GUI/environment art and 44 known entity-texture collisions with Fresh Animations. Exact archive diff comes before aesthetics. |
| Numerical Karma and per-player rare-loot multipliers | Excluded | MineDeck records private factual stewardship events only. Natural Matcha rewards stay at 1×; no child becomes the optimal spawner/dragon player. |
| LuckPerms | Held, not installed | The family whitelist and narrow Bridge capabilities do not presently require a general permission framework. |

## What populates the finished world

| Layer | V1 content and long-term loop |
|---|---|
| Overworld | Terralith's geography/caves and complete official structure suite; Minecraft's structures; living climate-specific vanilla villages; Terralith forts; Matcha trades, recipes, fishing, foods, tools, refugees, wards, archaeology, found items, and progression. Only Matcha beta-village geometry is absent. |
| Underground | Vanilla ores and structures, Terralith cave forms, Deep Dark/ancient cities, mineshafts, dungeons, strongholds, trial chambers, Matcha materials, dangerous cave creatures, spawner-breaking rewards, and recoverable hearts. |
| Nether | Amplified Nether's 256-block mountains/caverns with vanilla fortresses, bastions, piglins, blazes, wart, bartering, netherite, Wither progression, and building resources. No BetterNether mobs/items/cities. |
| End | Nullscape's 384-block alien terrain with the vanilla dragon, gateways, chorus, shulkers, End cities, ships, and elytra. No BetterEnd forests, mobs, gear, rituals, or parallel progression. |
| Settlements | A warded natural starter village, ordinary living villages, Terralith forts, refugees, player-built settlement registration, roads, rails, trade, restoration sites, and family construction. Wards keep Matcha's authored undead/healing semantics; they do not erase raids. |
| Resources | Minecraft mining/farming/fishing/redstone/trading remains the base. Restored vanilla villages use Matcha's 14 rich village tables. Terralith retains every original roll, while at most one eligible container per structure start receives one frozen Matcha table-reference bonus; foods receive consistent Matcha components. Per-event odds stay 1× and Divine Favour/Fragments are never injected. |
| Death and generations | Capacity starts at 10, with no black slots. One valid Survival death visibly blocks one unlocked heart. A genuine Crystal Heart clears every black slot and adds one capacity up to 30; all slots black retires the character, not the person or world. Reviewed finite-world heirlooms can move once without duplicating entitlements. |
| Post-dragon | Matcha's Divine Favour changes the Overworld surface into a safer road/rail/build/settlement age. Deep caves, raids, trial chambers, Withers, repeat dragons, Nether resources, and End-city exploration keep adventure alive. |

## Why this can be a forever world

The stack has one base game, one gameplay/economy author, and one terrain studio. Matcha changes why the family gathers, cooks, trades, survives, and defeats the dragon. Stardust changes the scale and shape of travel without adding a competing equipment tree. Minecraft still supplies the verbs: build, mine, farm, explore, automate, fight, trade, restore, collect, and invent.

That balance makes both sides of the world worthwhile. Staying home matters because the village, homes the children choose or build themselves, farms, redstone, museums, roads, and refugee growth can deepen for years. Going 10,000 blocks away matters because Terralith's complete structure/geography work, capped Matcha-enriched discoveries, amplified Nether routes, Nullscape, vanilla structures, living villages, and forts reward real exploration without turning every horizon into a theme-park quest.

The preservation promise is architectural, not magical:

1. Nothing world-critical auto-updates. Matcha 1.02 gameplay and every Stardust archive freeze permanently; only an official later Matcha visual delta may enter a tested, reversible resource-only derivative.
2. Exact artifacts, licenses, hashes, configs, first-party source, tests, and a private runtime are preserved.
3. Every upgrade is rehearsed on a restored MineJammer clone and can be rejected.
4. Backups include the world, dimensions, players, wards, Ledger, MineDeck state, exploration history, manifests, and databases.
5. The world has fixed conventional boundaries and a coherent V1 natural Far Rim.
6. Seed acceptance counts finite fortress, bastion, wart, stronghold, gateway, End-city/elytra, ancient-city, trial-chamber, and spawner supply before the children enter.
7. Later additions may enter only never-explored parcels. They are gifts, never repairs.

No one can honestly guarantee that Mojang authentication, Windows, hardware, or every third-party download service will be unchanged in ten years. We can guarantee that MineScape will not depend on those projects continuing to evolve: once V1 passes, the known-good world and its exact software environment are frozen and recoverable. That is the strongest practical form of a forever world.

**Release invariant:** If MineScape never receives a future parcel, capital, dungeon, mob, item, or quest, the family can still progress from first spawn through every dimension and sustain meaningful building, exploration, collection, combat, automation, and settlement play indefinitely.
