# MineScape V1.0 Master Build Plan

Status: canonical construction contract — implementation authorized; V1.0 release gates remain closed
Prepared: 2026-07-26
Preferred seed: `6246468738900744`

## 1. What the first version is

The naming is now fixed:

- **V0.x** means private construction, compatibility work, seed testing, and release-candidate testing. It happens in MineJammer. V0 worlds are disposable.
- **V1.0** means the first permanent MineScape world the children are allowed to enter. Their first login begins canonical history. V1.0 is never reset merely because the software later changes.

The three products have distinct jobs:

- **MineScape** is the permanent production world.
- **MineJammer** is the isolated laboratory and staging server.
- **MineDeck** is the nontechnical Windows control center: status, start/stop, maps, fog of war, backups, staging, settlement registration, and safe promotion of approved changes.

V1.0 will contain Minecraft's vanilla structures, the frozen Matcha gameplay/economy layer, and the complete official Stardust terrain trilogy. Terralith's official biomes, caves, structures, placement, templates, villagers, and base loot rolls remain enabled as one creator's work; a separately namespaced MineScape adapter adds a small, deterministic Matcha bonus to eligible Terralith discoveries without changing the Terralith archive or replacing its results. Matcha's beta-village geometry is suppressed so living Minecraft villages remain, but no Terralith family is filtered. Every separate structure pack remains absent. V1.0 contains no paid cities, external dungeon bundles, external ruin libraries, custom quest engines, RPG classes, custom NPC framework, custom mob ecosystem, ItemsAdder, MythicMobs, or ModelEngine.

## 2. Decisions already made

| Subject | V1.0 decision |
|---|---|
| Minecraft edition | Java Edition 26.2 |
| Server platform | Fabric, not Paper |
| Java | Private, pinned Java 25 LTS runtime |
| Seed | Use `6246468738900744` unless its exact locked-stack seed audit fails; changing it requires explicit approval |
| Core gameplay voice | Matcha Flavoured |
| Overworld | Terralith, conditional on the compatibility gate below |
| Nether | Amplified Nether |
| End | Nullscape, conditional on the compatibility gate below |
| Terralith structures | Complete official Terralith 2.6.4 suite, native placement, geometry, mobs, templates, and original loot rolls; one capped first-party Matcha enrichment per eligible structure start, never a replacement or per-player multiplier |
| World boundary | Conventional finite boundary; no looping or toroidal teleportation |
| Default boundary | Overworld `-16,384..16,384`, Nether `-2,048..2,048`, End `-8,192..8,192` on X and Z |
| Pregeneration | Benchmark a 2,048-block sample, then decide whether storage permits freezing the whole finite world |
| Night/difficulty | Easy onboarding with a parent-controlled switch to Normal; Matcha's no-phantom/longer-cycle rules and danger remain outside protected settlements |
| Starter village | The best ordinary living Minecraft village near the audited spawn; retain its natural architecture, set one public arrival point, add only safety repairs and civic wards |
| Warding culture | Active stone in every designated existing village building and at the town center; registered civic stones are immutable |
| Friendly civilizations | Generated living vanilla/Terralith villages are registered and consecrated automatically; player-built settlements can be registered later through MineDeck |
| Server access | Online authentication and whitelist; children use the home LAN, while the parent may use a private Tailscale route from the Mac; no router port forwarding |
| Player enrollment | MineDeck approves pending authenticated players and assigns access role by UUID; no advance names, custom homes, claims, kits, or personal spawn points |
| Remote parent access | Tailscale between the Windows host and parent's Mac; no public Minecraft or MineDeck port |
| Source control | Existing public `briggskellogg/minescape` GitHub repository for sanitized source, manifests, tests, and docs; never the family world, identities, addresses, credentials, exploration, or backups |
| Updates | Matcha gameplay data and all Stardust worldgen freeze at V1.0; only a compatible later Matcha visual resource archive may change the authored look; operational/client maintenance remains staged and explicit |
| Character hearts | Start with 10 usable slots; each valid Survival death visibly blacks/blocks one slot; a genuine Matcha Crystal Heart clears every blocked slot and adds one new usable slot up to 30; all slots blocked retires that character |
| Parent play | Survival Family Play by default; authenticated, time-limited Steward Creative through MineDeck; never blanket operator |
| Stewardship | Private factual journal and parent notes only; no numerical karma, automatic moral labels, rewards, or punishments in V1.0 |
| Matcha rare rewards | Natural economy locked at 1×; no per-player multiplier; narrow audited Reward Correction only |
| BetterX | BetterEnd and BetterNether excluded from V1.0; no supported 26.2 stack and they add a second progression/content system |

The 16,384-block Overworld radius makes 10,000 blocks genuinely distant while leaving thousands of blocks beyond it. It is exactly aligned to 32 Anvil regions in every direction, which materially simplifies border enforcement, staged region commits, and recovery. The Nether boundary follows the 1:8 coordinate relationship. The World Border mod must have `shouldLoopToOppositeBorder=false`.

### 2.1 What the children will actually see

#### The world edge

The [World Border](https://modrinth.com/mod/world-border) mod is an invisible coordinate tripwire, not scenery. It warns a player near the edge and safely teleports them inward if they cross it. With looping disabled it does not create 4D space, a visible force field, or an Interstellar distortion.

In V1.0 it is the permanent emergency backstop. MineScape Foundations provides clear escalating warnings and restrained particles/sound near the final approach. The V1 seed audit must also accept a quiet natural **Far Rim** buffer—preferably distant ocean, barren archipelago, or coherent extreme terrain—around the reachable boundary so the finished world never ends against an arbitrary ordinary meadow. A later authored spectacle may enrich only unseen outer parcels, but the V1 edge must already be coherent without it.

#### Settlements

The world has two naturally living settlement families:

1. **Minecraft villages:** the five pinned vanilla climate-specific designs at vanilla frequency, populated by ordinary villagers using Matcha's trade and chest economy.
2. **Terralith fortified villages:** the temperate fort and desert fort at Terralith's normal 46-chunk spacing/18-chunk separation with its built-in eight-chunk exclusion around ordinary villages. They are richer village variants—walls, chapels, smiths, farms, barns, and roads—not MineScape's future great cities.

Both are friendly sanctuaries. MineScape Core registers them as their structure pieces generate, installs one safe visible immutable Warding Stone in every designated house plus one at the bell or center, and evaluates those anchors through the batched sanctuary service. A raw decorative lodestone is never trusted as a functional Matcha ward. Matcha's abandoned `village_beta` structures and their eerie entry behavior do not generate.

Terralith's complete official non-village structure suite also generates unchanged: its Mage sites, Spire, huts, outposts, lodge, rubble, underground sites, and frosted dungeon remain part of Stardust's authored geography. They are discoveries, not automatically friendly settlements, and receive no civic wards unless the family later deliberately restores and registers one.

“Sanctuary” keeps Matcha's own semantics: civic wards heal villagers, players, pets, and golems; slow or attack nearby undead; and combine with light, walls, doors, and golems. MineScape does not invent blanket immunity to creepers, spiders, pillagers, patrols, or raid mobs. Covered interiors must be spawn-proofed, and raids remain a real Minecraft play loop.

The starting town is the best ordinary Minecraft village near safe spawn terrain: approximately 8–12 useful existing buildings, a bell or other natural common point, safe paths and lighting, a modest population, one iron golem, and open plots. No building is preassigned or rebuilt as a child's home. Every new approved player arrives at the same public village point with no MineScape kit and chooses or builds a home through ordinary play. Refugee forms remain a good-deed and settlement-growth mechanic rather than the world's sole source of villagers.

Ordinary village chests already resolve through Matcha's 14 overridden `minecraft:chests/village/*` tables, so restored vanilla villages expose the intended living-village economy without copied tables: component-bearing healing foods and equipment, climate fish, sturdy leather, bundles, religious texts, diamonds, maps, crops, and ordinary supplies. Matcha's 1,023 recipes—including 122 food recipes—remain global, as do its 235 villager trades and fishing system. Terralith villagers naturally receive those Matcha trades.

Terralith already composes with Matcha in several places without MineScape intervention: Valley Lodge barrels call Matcha's taiga-village table; desert sites and rubble call Matcha's desert-well and trail-ruins archaeology; and two fortified-desert cartographer chests call Matcha's simple-dungeon table. Those upstream references remain exactly as Stardust authored them. MineScape does not retarget them.

The user has deliberately chosen one further exception to pure Terralith output: the pinned **MineScape Loot Adapter** enriches eligible Terralith discoveries with frozen Matcha loot. Every Terralith archive, template, table reference, item count, slot, probability, enchantment, and original roll remains unchanged. After the original container resolves, Bridge deterministically selects at most one eligible container for the complete structure start and adds exactly one reference roll from an allowlisted, source-appropriate frozen Matcha table: a climate/profession village table for a fortified village; a provisions/fishing/village table for a surface lodge, hut, outpost, or mage site; `abandoned_mineshaft` for an underground work site; and `simple_dungeon` for the Spire or frosted dungeon. Rubble gets no second bonus because its brushables already resolve through Matcha archaeology. The chosen container and bonus resolve once, persist normally, and are recorded with structure/table provenance in MineDeck. No per-player rate, reroll, starter guarantee, or more-than-one-cache scaling exists. Divine Favour and Divine Fragments are never injected; a Crystal Heart may appear only when the referenced frozen Matcha table itself contains it, at that table's unmodified chance in a danger-tier site.

The same adapter normalizes visually identical componentless food emitted by a `terralith:*` table by attaching the exact frozen Matcha 1.02 components for that base food, without changing its identity, count, slot, or roll. This prevents one loaf or cooked fish from healing while an identical-looking Terralith one does not. It also classifies every Terralith biome into exactly one appropriate Matcha fishing-climate tag. The V1 generator corrects Matcha 1.02's audited `freshwater_hot_wet` predicate typo so tropical freshwater fish occur in wet rather than dry hot climates, then freezes that generated output. The three dormant upstream fish stubs lacking complete reachability/assets remain documented and are not invented into existence.

The archive-wide reachability audit found that Matcha's Cheerful and Mournful Clay Fetishes already remain obtainable through its frozen desert-well and trail-ruins archaeology overrides. Only four decorative set-piece curios exist solely inside the suppressed beta-village templates: Cyan Rose, Rose, Classic Porkchop, and the Dry Hands record. The Village Adapter preserves them through a deterministic, one-time `minescape:matcha_village_relics` cache attached only to restored vanilla village starts. Its single weighted result reproduces Matcha's own 24-weight beta building pool: weight 19 calls the unchanged `minecraft:archaeology/village_plains` table, weight 4 emits the exact Cyan Rose stack, and weight 1 emits Rose ×3, three max-stack-one Classic Porkchops, and Dry Hands ×1. It adds no new item ID, never replaces an existing roll, and never appears in the starter village by fiat. The finite-seed audit records every cache and proves that at least one complete curio set is obtainable.

Post-V1 authored capitals are a separate scale: enormous, exceptionally rare MineJammer-built discoveries committed only into never-explored parcels. Their rarity is not simulated by making ordinary Terralith forts scarce.

Terralith's native structure placement remains unchanged. Seed acceptance records the realized distribution and may reject the preferred seed if the complete official pack creates an unusable starting region, but MineScape never silently changes Stardust's frequencies. Matcha/Terralith village maps may lead players to ordinary forts; secrecy is reserved for future great cities.

#### Overworld and caves

Terralith supplies more than 95 biomes, reworked vanilla landscapes, mountain chains, valleys, volcanoes, canyons, arches, floating terrain, deep ocean trenches, skylands, vegetation, its cave-generation system, and its complete official structure suite. All use Minecraft blocks, items, and mobs; Terralith adds geography and authored arrangements rather than a second equipment, creature, or progression registry.

Underground Jungles, Fungal Caves, Infested Caves, Frostfire Caves, the Deep Dark, ancient cities, mineshafts, ordinary dungeons, strongholds, and trial chambers keep underground exploration rich. Matcha makes caves especially consequential: some hostile creatures favor caves/forests, food preparation matters because natural regeneration is removed, and breaking spawners is rewarded rather than preserving them as farms.

#### Nether

Amplified Nether raises the dimension to 256 blocks and creates huge mountains, valleys, caverns, and vertically stacked vanilla biomes. It adds no new items, mobs, biomes, bosses, structures, or progression system. Fortresses, bastions, ruined portals, piglins, blazes, and Nether resources remain Minecraft's; only their geography becomes grander.

#### End

Nullscape raises the End terrain space to 384 blocks and creates shattered islands, floating valleys, crystallized peaks, porous regions, and immense alien landforms while remaining barren. The dragon, gateways, chorus, shulkers, and End cities remain. Nullscape also contains sparse geographic rifts and dragon-skeleton formations; these are retained as atmosphere, not dungeons or an RPG layer.

#### The End victory

Stardust supplies no final reward. Terralith, Amplified Nether, and Nullscape are a geological trilogy from Stardust Labs, not a shared quest system.

The final world-changing reward comes from Matcha. When a player personally earns Matcha's one-time dragon-kill advancement, it summons one Nether Star—renamed **Divine Favour** by Matcha—at End coordinates `0 100 0`, announces “Evil has been banished from the surface,” and activates Matcha's safe-surface rule. Each stable family-person identity may qualify only once across character generations; clearing a retired character's advancements never reopens that entitlement. The item is not automatically assigned to the killer. Ordinary hostile mobs are then forbidden when exposed to the Overworld sky or at or above sea level, while caves below that level remain dangerous. The Nether and End remain adventurous. MineScape keeps Matcha's one physical reward per qualifying personal first kill and records every child present at the first victory as a participant, so the world-changing achievement is communal without silently duplicating the rare item.

That victory begins the world's second age rather than ending the game: the safer surface becomes a place for roads, rails, farms, redstone, museums, large family builds, refugee settlement, and connecting distant villages. Deep caves remain the Overworld danger-and-reward layer; raids and trial chambers remain repeatable combat; the Wither and dragon can be fought again; the Nether remains the route to blaze, wart, bastion, netherite, and construction resources; and Nullscape's outer islands, End cities, shulkers, and elytra remain long-range exploration. If no future parcel is ever committed, every progression and sustainable play loop must still work.

#### Difficulty and family assistance

Matcha intentionally makes the early game harder: stone tools are skipped, natural regeneration is removed, food becomes healing, and early zombies are dangerous. It also makes failure kinder through its item-retaining death system, removes phantoms, gives early inventory relief, and makes the post-dragon surface dramatically safer.

The warded village should prevent this from becoming discouraging. MineDeck will expose a parent-only **Family Assist** panel with reversible controls: Easy or Normal difficulty, start a session at dawn, rescue a stranded player to the village, and review a short Matcha progression hint. The initial recommendation is Easy during onboarding, then Normal when the children are comfortable. Peaceful is not the default because it would erase the contrast that makes the dragon reward meaningful.

### 2.2 Upstream-pure Terralith and the narrow Matcha village adapter

Terralith itself leaves `minecraft:village_plains`, `village_desert`, `village_savanna`, `village_snowy`, `village_taiga`, and the `minecraft:villages` structure set intact. It extends the five vanilla village biome tags so ordinary villages can appear naturally across appropriate Terralith terrain, then adds the separately named `terralith:fortified_village` and `terralith:fortified_desert_village` in `terralith:rare_village`.

Matcha changes that result: it replaces all five Minecraft village structure IDs with the same abandoned `village_beta` pool and changes the shared village placement to 80-chunk spacing/50-chunk separation. Consequently, the unmodified combined stack produces Matcha ruins plus separately generated populated Terralith forts—not living vanilla villages. Restoring the vanilla structures without changing Matcha's predicates would also make every living village trigger Matcha's abandoned-village music suppression and eerie sounds.

| Property | Matcha 1.02 | Terralith 2.6.4 |
|---|---|---|
| Ordinary village generation | All five climates point to one sparse `village_beta` system | Leaves the ordinary Minecraft village family available if Matcha's replacement is disabled |
| Special villages | None beyond its beta ruins | Adds fortified temperate and fortified desert villages |
| Population | No normal villagers, golems, or beds in ordinary templates | Fortified villages have beds, professions, workstations, bells, farms, and ordinary villagers; the desert castle includes a camel |
| Visual language | Small old oak/cobble/mossy-cobble ruins, cobwebs, hanging moss, coarse dirt, archaeology | Castles, walls, roads, lanterns, banners, churches/chapels, smiths, libraries, farms, barns, stables, graveyards, and profession buildings |
| Mood | Abandoned and eerie; entering suppresses music and plays unsettling environmental sounds | Living and prosperous; no Matcha eerie treatment by default |
| Placement | 80-chunk spacing, 50-chunk separation | Fortified-village set uses 46-chunk spacing, 18-chunk separation, and avoids an eight-chunk area around ordinary/Matcha villages |
| Economy fit | Native Matcha archaeology and village loot | Original Terralith tables/rolls remain; shared `minecraft:*` references resolve through Matcha, fort villagers receive Matcha trades, and one explicit capped Matcha enrichment may be added per eligible start |
| Warding covenant | Ruins need no civic wards until deliberately restored | Generated living villages contain no Warding Stones and therefore violate MineScape's civic rule until integrated |

Terralith's custom-structure switch is broader than villages. Its seven automatic structure families are:

1. **Rare villages:** fortified and fortified-desert villages.
2. **Mage sites:** four seasonal mage towers, a regular mage tower, and a mage complex with a small settlement.
3. **Rare dungeon:** the great icy Spire.
4. **Regular sites:** witch hut, desert outpost, Valley Lodge, glacial hut, and igloo.
5. **Rubble:** small forest, taiga, jungle, mesa, mountain, and desert archaeological ruins.
6. **Underground sites:** oak cabin, giant bee hive, mining outpost, old refinery, and sunken tower.
7. **Underground dungeon:** frosted dungeon.

The official No Structures add-on disables all seven Terralith families, including the two forts, and is not installed. MineScape does not recreate a forts-only switch. A pinned private **MineScape Village Adapter** instead:

1. Restores the five ordinary village structures and `minecraft:villages` placement from the exact pinned vanilla 26.2 server data while retaining Terralith's additive biome-tag extensions.
2. Suppresses Matcha's beta-village geometry and retargets/disables its `in_village`, `not_in_village`, and entry-advancement haunted behavior so living villages are never falsely eerie.
3. Leaves every Terralith structure, placement set, template, processor, loot table, mob, and map tag byte-for-byte authoritative; the generated packs contain no `data/terralith/**`, and a version diff fails review if MineScape would need to shadow one.
4. Lets Matcha's global profession-trade replacements apply naturally to every vanilla villager, including fort residents and asylum seekers.
5. Lets restored vanilla village containers resolve through Matcha's own overridden `minecraft:chests/village/*` tables while every `terralith:*` container performs its original upstream roll first.
6. Adds the separately namespaced, one-time Matcha relic archaeology cache described above only to a deterministic minority of restored vanilla villages; it neither wraps nor shadows any upstream loot table.
7. Automatically registers only generated living vanilla villages and Terralith fortified villages as friendly after a census check, places one safe visible ward anchor per designated existing building plus the center, and evaluates the anchors through the batched sanctuary service. Other Terralith structures remain unwarded discoveries.
8. Hands eligible Terralith structure starts to the separate Loot Adapter for the one-time, at-most-one-container bonus and exact food-component normalization described above; no source table is rewritten or rerolled.
9. Records structure family, bounds, building pieces, census, ward anchors, upstream hashes, original and bonus table provenance, relic-cache assignment, and discovery history in MineDeck without assigning homes.

The governing rule is now: **Minecraft supplies the base civilization, Terralith remains Stardust's complete geographic and architectural work with every original roll intact, Matcha supplies the family economy, refugees, danger, recovery, trades, recipes, fishing, and found items, and MineScape supplies the explicit compatibility, capped cross-author enrichment, family safety, and preservation policy.**

## 3. Critical release gates

### 3.1 Matcha and Stardust do not safely stack by load order

The current official archives were inspected directly. Matcha 1.02 and Terralith 2.6.4 replace 38 of the same complete vanilla biome definitions. Matcha 1.02 and Nullscape 1.2.20 replace the same five vanilla End biome definitions. Terralith's Fabric jar contains the same collision; changing from datapack to mod form does not solve it.

Whichever pack loads later silently replaces the other pack's features, spawns, fog, vegetation, and attributes in those biomes. Amplified Nether does not have this collision.

Therefore V1.0 must not be generated by merely dropping the packs together. The private, generated, exact-version **MineScape Compatibility** layer follows explicit field ownership:

1. Reads the pinned upstream definitions.
2. Uses the exact Terralith or Nullscape definition as the base for every collided biome; Stardust owns terrain, carvers, feature ordering, ambience, and dimensional character.
3. Applies only an enumerated allowlist of progression-critical Matcha additions whose omission would make frozen Matcha gameplay incomplete.
4. Records the source hash and ownership of every applied field and expected output hash; unknown fields, changed schemas, new collisions, or duplicate feature steps fail the build.
5. Loads at the highest data priority.
6. Is validated by automated structure/feature/spawn checks and manual MineJammer exploration.

This overlay is world-critical code. It is private, credited, generated, hashed, documented, and tied to the exact frozen inputs. If the narrow additions cannot be proven safe, Stardust's complete biome definition wins and the conflicting Matcha biome delta is omitted. V1.0 may ship only if the resulting Matcha progression, resources, and rewards remain complete; otherwise release stops for an explicit design decision rather than silently changing Terralith or Nullscape.

For the five End biome collisions, Nullscape's terrain/features receive priority while the compatibility layer preserves only Matcha behavior that can be retained without erasing Nullscape. Otherwise Nullscape's glowstone, chorus, and related End features disappear.

### 3.2 Matcha gameplay freezes; only compatible visuals may refresh

[Matcha Flavoured 1.02](https://modrinth.com/datapack/matcha-flavoured/version/RVX0a6It) was released as a same-day hotfix and its author explicitly warns that future item changes may require migration. Its `data/` side becomes permanent V1.0 world law after the release suite. No later Matcha functions, recipes, loot, advancements, trades, items, scoreboards, or worldgen enter this world.

Before V1.0, the exact Matcha build must pass recipe progression, loot, death, sleep, Warding Stone, dragon, advancement, and item-reload tests. The same official 1.02 archive initially supplies both server data and client resources.

A later official Matcha archive may be considered **only as the source for a client visual-resource update**. Its whole official archive is preserved and hashed, then MineJammer builds a separately versioned resource-only derivative from frozen 1.02 `assets/**` plus individually approved later visual deltas; the served artifact contains no `data/**`, is never copied into the world's `datapacks`, and carries the required Matcha credit/license metadata. The server/world continues running the unchanged official 1.02 archive as data. Because Matcha intentionally binds data and resources together, an asset-only update is never assumed compatible: MineJammer must resolve every frozen 1.02 item model, blockstate, equipment definition, font, translation, sound/instrument, GUI, advancement icon/background, and world item stack—including preserved relics—reject any visual that assumes newer gameplay data, run the complete controller/progression suite, and retain one-click rollback to the 1.02 resource archive. If it fails, visuals also remain 1.02. No other world-authoring pack is updated after V1.0.

### 3.3 Falling trees are wanted, but the current build is beta

[Enhanced Falling Trees 0.7.1](https://modrinth.com/mod/enhanced-falling-trees/version/Y0zs6l6h) is the current 26.2 candidate. It is a Fabric beta, requires Cloth Config, and must be installed on the server and clients. It remains in V1.0 only if MineJammer proves that it handles ordinary and Terralith trees without duplication, partial floating canopies, unacceptable lag, or griefing nearby builds. If it fails, V1.0 pauses for a better implementation rather than substituting a large gameplay mod.

### 3.4 The official Launcher cannot promise zero-click joining

Mojang officially supports Launcher Quick Play and Java's `--quickPlayMultiplayer "host:port"` game argument, but it does not document a desktop-callable Launcher deep link that selects a pinned Quick Play tile. The durable V1.0 behavior is:

1. The **Play MineScape** shortcut starts or attaches to MineDeck.
2. MineDeck starts MineScape only if needed.
3. It waits for a real Minecraft protocol response.
4. It opens/focuses the official Launcher with MineScape pinned in Quick Play.
5. The player clicks the MineScape Quick Play tile once.

MineDeck will never store Microsoft credentials or bypass authentication. True unattended entry would require brittle Windows UI automation or a different launcher and is not part of the default promise. See Mojang's [Quick Play guide](https://help.minecraft.net/hc/en-us/articles/18511975781645) and [Java Quick Play specification](https://feedback.minecraft.net/hc/en-us/articles/16499677456781-Minecraft-Java-Edition-1-20-Trails-Tales).

### 3.5 Many Warding Stones deliberately change the starting difficulty

Matcha 1.02 implements a Warding Stone as an invulnerable tagged armor stand that maintains a lodestone. Each active stone heals villager friends within 16 blocks, slows undead within 26 blocks, and attacks nearby undead. Its normal design allows the lodestone to be broken, after which the stone shatters.

MineScape will make only registered civic stones immutable through the MineScape Bridge. The Bridge must reject player breaking, piston movement, explosions, and environmental replacement at those coordinates while leaving the surrounding building editable. Ordinary stones crafted later retain Matcha's normal behavior.

Dozens of overlapping Matcha armor-stand loops would repeat essentially the same entity searches. MineScape Core therefore indexes civic stones and evaluates their union as one batched sanctuary service per settlement while retaining a real visible stone, local soul-fire effects, and the intended protection at every designated building and town center. The stones are functional anchors, not inert decorations. The complete starter town is still profiled with 1, 16, 32, and 64 loaded anchors and approximately 50 undead nearby. V1.0 requires stable 20 TPS and no more than roughly a 5% MSPT increase over the equivalent unwarded scene.

### 3.6 Mortal Hearts use visible scars and full Crystal renewal

Matcha 1.02 does not merely default to ten hearts: its setup forcibly resets every `Hearts` score at or below 20 health points back to 20, its death function subtracts only above 20, and its maximum-health mapping stops at that floor. It also uses nearest-player `@p` targeting in paths where multiplayer attribution must instead remain on the executing player.

MineScape Core owns a versioned **Mortal Hearts** state with two server-authoritative integers per character: `capacity` and `blocked`. A new character starts at capacity 10, blocked 0. One valid production Survival death increments `blocked` by one. Usable maximum health is `capacity - blocked`, but the required MineScape Client HUD continues to draw every unlocked slot: usable hearts retain Matcha's normal heart art and each lost slot is visibly black/blocked, so a lifetime loss never disappears from the interface.

Consuming one genuine Matcha Crystal Heart is a renewal, not a one-slot repair: it sets `blocked` to zero and increments `capacity` by one, up to Matcha's existing 30-heart ceiling. Thus a character at seven usable and three black hearts returns with eleven usable hearts. Bridge mirrors the usable value to Matcha only for compatibility, attributes every transition by UUID, and rejects duplicate item-use/replay events. When the final usable slot would become blocked, Bridge enters Retirement Pending before Minecraft is asked to represent impossible zero maximum health.

Only unique deaths from an active character in Family Play count. Steward, Creative, MineJammer, test, replayed, rollback-created, and clearly administrative deaths do not. PvP never consumes a permanent heart in V1.0; siblings cannot erase one another's characters. A short respawn grace prevents a single bad spawn from emitting several life events. Matcha's item-retaining death behavior remains.

All unlocked slots black means **game over for that character**, not data destruction. MineDeck enters Retirement Pending, takes a complete snapshot, pauses ordinary joins for that character, and presents the cause and evidence for parent review. A proven technical or administrative error receives an append-only pardon. Otherwise the character is archived in the Hall of Ancestors. The same authenticated account may begin generation N+1 at ten usable hearts and the public starter-village arrival, with a new personal inventory, Ender Chest, statistics, recipes, and advancements; family milestones, builds, stable-person dragon entitlement, and natural rare-reward history remain. The retired inventory is sealed initially rather than inherited automatically, and spectator mode is not offered because it would reveal unexplored terrain. After a documented waiting period and parent review, nonpersonal finite-world equipment—such as spare elytra, shulker boxes, netherite templates, and communal tools—may be transferred once into a family estate vault under an immutable inheritance event; character-bound trophies and entitlement items remain with the ancestor. Nothing is deleted or silently duplicated.

### 3.7 V1 records stewardship facts, not moral scores

Numerical karma and automatic moral classification are deferred. Minecraft can prove that a player damaged a villager, completed a trade, transferred an item, opened a container, settled a tagged refugee, or attempted to break a civic ward; it usually cannot prove intent, consent, generosity, accident, theft, or cruelty. Turning those guesses into points would add complexity without improving the world.

V1.0 therefore includes only a private append-only **Stewardship Journal**. Bridge records factual events with actor/target UUID, character generation, time, dimension, coordinates, settlement/container provenance, direct cause, consent state when explicit, confidence, and source version. The parent may attach a private note or label and may append a reversal; records are never silently edited. There is no balance, weighting, automatic positive/negative label, child-facing leaderboard, reward, punishment, heart interaction, loot interaction, access rule, or difficulty rule.

The journal can later derive a versioned karma policy without changing world files or losing history, after real family play reveals which observations are meaningful. “Ethical loot” is handled directly instead: friendly settlements have marked public supplies and blocked private containers, while abandoned and hostile-site containers are clearly adventure loot. Real lightning, damage, confiscation, public shame, and automated punishment remain vetoed; a future harmless theatrical effect would require a separate parent-confirmed design.

### 3.8 Matcha's rare-reward economy stays at 1×

MineDeck displays **Natural Matcha economy: 1× (locked)**. A persistent per-player multiplier is vetoed: Divine Fragments and other rewards are tradable, so the family would simply route every spawner through the boosted player; the setting would become both an optimization puzzle and a hidden life multiplier because Divine Fragments feed Crystal Hearts.

Divine Favour and Crystal Hearts are never multiplied. The dragon's one-time entitlement is tied to the stable family-person identity across character resets and world epochs, not merely the currently visible advancement. Other progression items are denied unless individually reviewed. If long-term MineJammer evidence later proves spawner progression too sparse, the only candidate automatic rule is a disclosed family-wide `Divine Fragment from player-broken spawner` rate of 1× or 2×, prospective and versioned; V1.0 remains 1×.

MineDeck does provide a narrow parent-only **Reward Correction**, not a rate. It requires reauthentication, an exact player and current character, one allowlisted item and quantity, a preset reason plus note/evidence, and an immutable grant ID. The V1 allowlist is Divine Fragment only, normally quantity one, for verified technical loss, administrative correction, or deliberate accessibility/catch-up assistance. Delivery occurs once to the selected online UUID, forces a save, and becomes `pending`, `delivered`, or `delivery unknown`; an ambiguous timeout never retries automatically. Retirement suspends pending grants, restores create a new world epoch with reconciliation events, and no correction silently inherits or replays.

### 3.9 Matcha must be completely operable by controller

Matcha is a datapack/resource pack and does not introduce a Java-coded custom screen. Its crafting, inventory, villagers, trades, containers, item use, death/respawn flow, recipe book, and progression tree use Minecraft's existing interfaces. Archive inspection found 64 displayed advancement nodes under the three vanilla-screen roots **From Earth**, **Through Hell**, and **To Dreams**. Their criteria use vanilla triggers rather than clickable chat commands, `/trigger`, or a keyboard-only interaction.

[Controlify 3.1.2](https://modrinth.com/mod/controlify/version/ww7bBOmX) is the sole controller layer. It provides controller navigation for Minecraft GUIs, container cursor snapping, button guides, an on-screen keyboard, and a virtual-mouse fallback for mouse-shaped screens. Its exact jar has dedicated handling for the ordinary container, recipe-book, merchant, enchantment, loom, and stonecutter families, but no dedicated advancement-graph processor. During gold-client setup MineScape therefore opens the real advancement screen, enables Controlify's virtual mouse there, and preserves the class entry and binding generated by Controlify itself; the family is also taught the explicit toggle. MineScape does not add Better Advancements, a quest-book mod, or a second progression screen.

Controller support is nevertheless a release gate, not an assumption. On each child's actual controller profile, a keyboard and mouse must be disconnected while the tester can:

1. Open Advancements from the pause menu, switch among all three Matcha roots, engage the virtual mouse, drag/pan the complete graph in every direction, focus all 64 displayed nodes, read multiline tooltips and criteria, distinguish completed/uncompleted state, and return to play.
2. Use recipe-book filters, paging, selection, search and the on-screen keyboard; then use the crafting table, kiln/smoker, oven/furnace, blast furnace, inventory move/split/drop/quick-move, equipment, bundles/containers, ordinary and asylum-seeker villager trades, anvil, smithing, enchanting, books, signs, chat, death, respawn, and bed confirmation.
3. Equip and use representative Matcha tools, armour, prepared foods, fishing, refugee forms, held-use items, item-on-block mechanics, shields/offhand, Warding Stone creation/placement, Clay Fetishes, Crystal Hearts, and every interaction required through End progression.
4. See correct controller glyphs without Matcha's resource pack obscuring Controlify guides, cursor focus, advancement icons, text, or the on-screen keyboard.

MineDeck's **Progress** page mirrors advancement state for the parent when a child needs help, but it is read-only and never completes a node. If the vanilla advancement screen has a controller defect, V1 pauses for a Controlify binding/configuration fix or a narrow client accessibility fix; progression is never moved into MineDeck or replaced by a quest engine.

## 4. Exact pinned V0 compatibility target

These are the initial artifacts for the compatibility laboratory. The final lock manifest records the download URL, Modrinth version ID, creator, license, filename, SHA-512, placement, load order, and removal risk for every artifact.

### World-critical content

| Component | Exact target | Installation |
|---|---|---|
| [Matcha Flavoured](https://modrinth.com/datapack/matcha-flavoured/version/RVX0a6It) | 1.02 | Unchanged zip in each world's `datapacks`; its 1.02 assets are the initial required client resource, later replaceable only by the approved resource-only derivative |
| [Terralith](https://modrinth.com/datapack/terralith/version/CzijfXJQ) | 2.6.4 datapack | Install before the first world boot; never switch later to its different mod variant |
| [Amplified Nether](https://modrinth.com/datapack/amplified-nether/version/xIayvf8F) | 1.2.15 datapack | Install before the Nether exists |
| [Nullscape](https://modrinth.com/datapack/nullscape/version/prWWpjSv) | 1.2.20 datapack | Install before the End exists |
| MineScape Compatibility | Built from the exact four inputs | Highest-priority private compatibility datapack |
| MineScape Village Adapter | Built from pinned vanilla 26.2 and Matcha 1.02 data, with Terralith identifiers used only for runtime recognition | Restored living villages, suppressed Matcha beta geometry/haunting, curio-cache assignment, and automatic living-village/fort ward registration |
| MineScape Loot Adapter | Built from exact Matcha 1.02 and Terralith 2.6.4 tables | Original Terralith rolls plus at most one deterministic Matcha enrichment per eligible start, exact Matcha food-component normalization, fishing-biome classification, and provenance |
| MineScape Foundations | First-party private datapack | Stable game rules, settlement/restoration tags, release markers, and migrations |
| MineScape Settlement Registry | First-party signed runtime manifest | Generated structure identities, source hashes, bounds, buildings, public/private storage, census, ward anchors, and restoration state |

The datapack variants are used consistently. Terralith itself warns that it cannot be safely removed from an existing world and that its mod and datapack variants should not be swapped. [Terralith: No Structures 1.1](https://modrinth.com/datapack/terralith-no-structures/version/YBjMgOVJ) remains catalog-only and is not installed. MineScape never shadows Terralith's structure sets or loot tables; its runtime enrichment is a separately identified post-roll composition layer.

### Production server

| Component | Exact target | Purpose and policy |
|---|---|---|
| [Fabric server](https://fabricmc.net/use/server/) | Minecraft 26.2, Loader 0.19.3, Launcher 1.1.1 | `fabric-server-mc.26.2-loader.0.19.3-launcher.1.1.1.jar` |
| [Fabric API](https://modrinth.com/mod/fabric-api/version/lVXlbH4w) | 0.155.2+26.2 | Required server foundation |
| [Lithium](https://modrinth.com/mod/lithium/version/UPNexAfy) | 0.25.2 | Invisible server optimization |
| [FerriteCore](https://modrinth.com/mod/ferrite-core/version/d5ddUdiB) | 9.0.0 | Memory optimization |
| Enhanced Falling Trees | 0.7.1 beta | Conditional on its V0 gate; requires [Cloth Config 26.2.155](https://modrinth.com/mod/cloth-config/version/Nv3xnWXd) |
| [World Border](https://modrinth.com/mod/world-border/version/1e9MjNpZ) | 26.2.0-4.9 | Finite per-dimension rectangle; requires [Collective 8.39](https://modrinth.com/mod/collective/version/M75JwjyS); looping explicitly off |
| [BlueMap](https://modrinth.com/plugin/bluemap/version/VTvifNPN) | 5.22 Fabric | Private rendering backend for MineDeck, never the authority for exploration |
| [Ledger](https://modrinth.com/mod/ledger/version/KpVLPOJk) | 1.3.23 | Block/entity audit and rollback; database backed up separately |
| MineScape Bridge/Core | First-party Fabric mod | Health/control channel, scarred Mortal Hearts, deterministic post-roll Loot Adapter, factual Stewardship events, 1× rare-event enforcement/corrections, exploration telemetry, batched civic wards, settlement/provenance registry, and narrow authenticated administration |

[LuckPerms 5.5.57](https://modrinth.com/plugin/luckperms/version/UHUghDkV) is held in the artifact catalog but is not installed by default. A whitelisted family server with ordinary players and narrow authenticated MineDeck/Bridge capabilities does not need a general permission system. The parent is not a blanket Minecraft operator. LuckPerms enters only if a real granular-permission requirement appears.

[Simple Voice Chat 2.6.21](https://modrinth.com/plugin/simple-voice-chat/version/3SOh5iiX) is also conditional. It is useful only if the family plays from different rooms or locations, and it adds a client requirement plus UDP port 24454.

### MineJammer-only tools

| Component | Exact target | Policy |
|---|---|---|
| [WorldEdit](https://modrinth.com/plugin/worldedit/version/j2w3GmPv) | 7.4.4 Fabric | Staging construction and inspection; not present during ordinary production play |
| [Chunky](https://modrinth.com/plugin/chunky/version/4Eotm6ov) | 1.5.3 Fabric | Seed tests and measured spawn-envelope pregeneration; never blindly generate the entire border |

### Default client profile

MineDeck will build a dedicated Fabric 26.2 game directory for the official Launcher rather than contaminating the user's ordinary `.minecraft` profile.

| Layer | Exact target | Default |
|---|---|---|
| Loader/API | Fabric Loader 0.19.3; [Fabric API 0.155.2](https://modrinth.com/mod/fabric-api/version/lVXlbH4w) | Required |
| Gameplay/resource | Frozen Matcha 1.02 assets initially; later only an approved resource-only derivative under §3.2 | Required and hash-verified |
| MineScape Client | First-party Fabric client module paired to Bridge/Core | Required black/blocked-heart HUD, server-state sync, visible Steward indicator, and compatibility diagnostics; grants no authority |
| Falling trees | Enhanced Falling Trees 0.7.1 + Cloth Config 26.2.155 | Required only if its server gate passes |
| Performance | [Sodium 0.9.1](https://modrinth.com/mod/sodium/version/2Yom1N68), [ImmediatelyFast 1.16.2](https://modrinth.com/mod/immediatelyfast/version/uJHxuQxy), [Entity Culling 1.10.5](https://modrinth.com/mod/entityculling/version/iiF6U3Ne), FerriteCore 9.0.0, Lithium 0.25.2 | Enabled |
| Configuration | [Mod Menu 20.0.1](https://modrinth.com/mod/modmenu/version/njXb639R) + [Text Placeholder API 3.1.0-beta.1+26.2](https://modrinth.com/mod/placeholder-api/version/NDqH16LT) | Enabled if the combined client QA remains clean |
| Controller | [Controlify 3.1.2](https://modrinth.com/mod/controlify/version/ww7bBOmX) + [YACL 3.9.6](https://modrinth.com/mod/yacl/version/cnfPzuFU) | Enabled on controller-using machines |
| Quiet visuals | [Continuity 3.0.1](https://modrinth.com/mod/continuity/version/mgUN5Xz2), [Falling Leaves 2.0.7](https://modrinth.com/mod/fallingleaves/version/JYRSvzQW) | Enabled after visual QA |
| Shaders | [Iris 1.11.2](https://modrinth.com/mod/iris/version/oaD6KQls) + [Complementary Reimagined r5.8.1](https://modrinth.com/shader/complementary-reimagined/version/yCCduG44) | Installed but disabled or conservative per machine; use OpenGL until Vulkan is proven |
| Pickup clarity | [Pick Up Notifier 26.2.0](https://modrinth.com/mod/pick-up-notifier/version/DWaZDkc8) + [Puzzles Lib 26.2.1](https://modrinth.com/mod/puzzles-lib/version/HfGQTxSR) + [Forge Config API Port 26.2.1](https://modrinth.com/mod/forge-config-api-port/version/rSd3GiG8) | Optional audition; accept only if Matcha item names and controller layout remain clear |
| Tool clarity | [Durability Tooltip 1.1.6](https://modrinth.com/mod/durability-tooltip/version/cvbPblt6) + [SuperMartijn642's Config Lib 1.1.8](https://modrinth.com/mod/supermartijn642s-config-lib/version/tg619S8t) | Enabled after one Matcha-equipment/controller QA pass |
| World history | [Better Statistics Screen 5.5.4](https://modrinth.com/mod/better-stats/version/Qn69b7mm) + [TCDCommons 5.5.4](https://modrinth.com/mod/tcdcommons/version/6YYTlCvO) | Enabled after its screen passes Controlify QA |
| Audio | [Sound Physics Remastered 1.5.1 beta](https://modrinth.com/mod/sound-physics-remastered/version/d8iioMMp) | Optional audition, off by default |
| Dynamic light | [LambDynamicLights 4.12.2](https://modrinth.com/mod/lambdynamiclights/version/jBLH7Qy8) | Off by default because it changes darkness |

Fresh Animations, EMF, and ETF are withheld from V1.0. Matcha and Fresh Animations collide on 44 actual entity textures (46 archive paths total), so a casual resource-pack order would erase part of one creator's visual voice.

### Parent and MineJammer client tools

The ordinary Family profile remains compact. A separate parent Steward game directory adds [OrthoCamera 0.1.11+26.2](https://modrinth.com/mod/orthocamera/version/nURmQGL6), the selected orthographic projection tool. It is client-side, grants no server authority, and is configured for Sodium compatibility; large scales require block-face-culling QA. [Camera Utils 1.1.2](https://modrinth.com/mod/camera-utils/version/ftzKCT0z) and [JEI 30.14.0.93 beta](https://modrinth.com/mod/jei/version/2jnbg93a) are MineJammer-only because detached cameras can peek around danger and a complete item/recipe browser exposes discovery. [Mouse Tweaks 2.31](https://modrinth.com/mod/mouse-tweaks/version/jOiTBIaB) is optional on the parent's keyboard/mouse profile and absent from controller machines. AppleSkin is MineJammer diagnostics only because its hunger/saturation projection does not describe Matcha's food-as-healing model reliably.

### Candidate-list verdict

| Candidate group | V1.0 verdict | Reason |
|---|---|---|
| [Sodium](https://modrinth.com/mod/sodium), [Iris](https://modrinth.com/mod/iris), [Complementary Reimagined](https://modrinth.com/shader/complementary-reimagined) | Keep | Current, established, replaceable client layer; shaders remain per-machine and hot-disableable |
| [Indium](https://modrinth.com/mod/indium) | Veto | Obsolete and incompatible with modern Sodium; Sodium now includes Fabric Rendering API support |
| Noisium/[NoisiumForked](https://modrinth.com/mod/noisiumforked) | Veto | Being discontinued; canonical worldgen gains do not justify another retiring optimization |
| [Fast Noise](https://modrinth.com/mod/zfastnoise/version/GJk7sVtP) | MineJammer benchmark only | Modern replacement candidate, but it touches world-generation internals and enters production only after byte/parity and measured-benefit proof |
| [Distant Horizons](https://modrinth.com/mod/distanthorizons/version/gBf0SaV1) | Veto for V1.0 | Beta complexity and far-LOD streaming can undermine discovery and MineDeck fog of war |
| [AppleSkin](https://modrinth.com/mod/appleskin/version/uo5bAN1Y) | MineJammer only | Misleading against Matcha's hunger/healing rewrite until proven otherwise |
| [Pick Up Notifier](https://modrinth.com/mod/pick-up-notifier/version/DWaZDkc8) | Optional audition | Controller-friendly feedback, but extra dependencies and HUD noise require a real family-profile test |
| [Durability Tooltip](https://modrinth.com/mod/durability-tooltip/version/cvbPblt6), [Better Statistics Screen](https://modrinth.com/mod/better-stats/version/Qn69b7mm) | Approve after QA | Quiet information with long-world value; verify Matcha tooltips and Controlify navigation |
| [Camera Utils](https://modrinth.com/mod/camera-utils/version/ftzKCT0z), [JEI](https://modrinth.com/mod/jei/version/2jnbg93a) | Parent/MineJammer only | Useful inspection tools but too revealing for the children's discovery profile |
| [Mouse Tweaks](https://modrinth.com/mod/mouse-tweaks/version/jOiTBIaB), [Smooth Scrolling](https://modrinth.com/mod/smooth-scroll/version/UnyB1ePl) | Parent optional / defer | Mouse-specific or low-value on controller-first clients |
| [Sounds](https://modrinth.com/mod/sound/version/kv2RxMQu), [AmbientSounds](https://modrinth.com/mod/ambientsounds/version/jGlT0HRa) | Defer auditions | Strong second audio authors that may mask Matcha's deliberate soundscape |
| [Sound Physics Remastered](https://modrinth.com/mod/sound-physics-remastered/version/d8iioMMp) | Optional, off | Coherent effect rather than a content library, but current 26.2 build is beta |
| [ImmersiveThunder](https://modrinth.com/mod/immersivethunder), [Eating Animation](https://modrinth.com/mod/eating-animation), [Cave Dust](https://modrinth.com/mod/cave-dust), [Immersive UI](https://modrinth.com/mod/immersive-ui), [Tiny Item Animations](https://modrinth.com/mod/tiny-item-animations), [Flow](https://modrinth.com/mod/flow) | Veto for target | No supported official Fabric 26.2 build; old jars are never forced |
| [Continuity](https://modrinth.com/mod/continuity), [Falling Leaves](https://modrinth.com/mod/fallingleaves) | Keep after visual QA | Small client-only polish with no exact Matcha archive-path collisions |
| [BetterEnd](https://modrinth.com/mod/betterend), [BetterNether](https://modrinth.com/mod/betternether) | Veto/defer for V1.0 | No official 26.2 builds; current chain requires alpha/beta BCLib and World Weaver, and the mods add mobs, gear, food, rituals, dungeons, cities, and separate progression rather than quiet terrain |
| [Geophilic](https://modrinth.com/datapack/geophilic/version/6uLCMJCR), [Tectonic](https://modrinth.com/datapack/tectonic/version/CmzMQNDL) | Veto | Redundant worldgen authors and additional collision/maintenance surface beside Terralith |
| Listed 3D/bushy/icon resource packs | Defer exact-pack audit | The supplied URLs are truncated and Matcha already owns extensive block/item/GUI/entity assets; exact path diff comes before aesthetics |

BetterX's compatibility claim is credible in principle: Nullscape explicitly names BetterEnd as an exception to its normal End-worldgen incompatibility, Amplified Nether names BetterNether as a commonly compatible Nether layer, and BCLib/World Weaver is designed to merge those biome sources. It is still unusable for this release. The nearest BetterEnd and BetterNether builds target Minecraft 26.1.2, not 26.2; their shared [World Weaver](https://modrinth.com/mod/worldweaver) dependency describes itself as early development with unstable APIs and possible irreversible game-breaking world damage. A later stable exact-version appearance would remove only the technical veto, not the separate design decision to accept BetterX as a second gameplay author.

Matcha contains 1,637 textures, 696 models, 294 item definitions, and substantial GUI, sound, blockstate, and equipment content. The visual-resource rule is therefore exact, not categorical: **the approved Matcha resource archive wins every unresolved visual collision.** Item/equipment, entity/model, GUI, sound, and environment packs require an archive-path diff plus visual and progression QA; resource-pack order is not a merge and grants no data-pack authority.

### Complementary “Family Clear” preset

The attached Minecraft 1.20 transcript is inspiration, not an install recipe. Its Indium step is obsolete and its shader option names predate Complementary Reimagined r5.8.1. MineDeck will generate the preset from r5.8.1's real schema and visually verify it on each client.

The intended look keeps vanilla legibility: Integrated PBR/auto normals and noise-coated textures off unless a matching audited PBR pack exists; ordinary sky color; modest stars; cave-light floor around 48–64 rather than 128; handheld-light distance around 16–18 rather than 28; block flicker and dungeon outlines off; leaves waving on with rain intensity near 1.5–2 rather than 4; restrained shafts, bloom, and sharpening; water refraction/absorption/caustics only if the device remains smooth; underwater visibility around 64 rather than 128; rain puddles optional; motion blur, depth of field, chromatic aberration, and camera distortion off. MineDeck also supplies **Shaders Off** and **Parent Cinematic** presets. No shader setting is world-critical.

## 5. Installation order and reproducibility

The completed V1 installer is required to perform the following operations. At this construction checkpoint, these remain explicit release-gated targets unless the build-status document says otherwise:

1. Install/publish MineDeck as a self-contained Windows application. It must not require the user to install a system-wide development runtime.
2. Install a private Java 25 LTS runtime under the MineScape tree and always launch Fabric with its absolute path.
3. Download artifacts only from their authoritative Minecraft, Fabric, or Modrinth URLs.
4. Verify every artifact against the lock manifest before it can enter either server.
5. Create MineScape and MineJammer as separate Fabric instances with distinct ports, configs, logs, and writable worlds.
6. Install libraries and operational server mods into each instance's `mods` directory.
7. Put the exact world-critical datapacks into the empty world's `datapacks` directory **before the first boot**. Assert the expected load order, with MineScape Compatibility highest.
8. Boot only MineJammer, accept the EULA through an explicit setup screen, and validate logs plus `/datapack list`.
9. Have MineDeck serve the exact Matcha zip through a LAN-accessible resource-pack-only endpoint, configure its SHA-1 in `server.properties`, and require it on connection. The MineDeck dashboard and raw BlueMap remain private.
10. Copy the required packs into BlueMap's pack configuration so its render agrees with the game.
11. Build isolated Family and parent Steward client game directories, install their exact manifests, start each once through the official Launcher, join manually once, and pin ordinary MineScape Family Play in Quick Play.
12. Export a private offline recovery bundle containing the exact server launcher, Java runtime, approved artifacts, hashes, configs, compatibility overlay, and documentation. Third-party licenses and redistribution restrictions are preserved.

No setup step resolves to “latest.” Every executable and content file resolves to a version ID and cryptographic hash.

## 6. Repository and runtime architecture

```text
<repository>\
├─ MineScape\                    Fabric Bridge/Core and paired client source
├─ MineJammer\                   compatibility, seed, and promotion laboratory
├─ MineDeck\                     Windows supervisor and parent dashboard source
├─ ops\                          build, acquisition, assembly, and install scripts
├─ artifacts\                    lock metadata plus ignored verified cache
├─ var\
│  ├─ MineScape\                 inert production skeleton, then permanent world
│  ├─ MineJammer\                isolated disposable staging server
│  └─ host\MineDeck\             ignored published Windows application
├─ docs\                         canonical contract and operating guides
└─ legacy\bedrock-v0\            inactive tagged prototype
```

No symbolic links or shared writable world directories are permitted between production and staging. Runtime worlds, secrets, databases, logs, third-party binaries, and backups are excluded from Git. Source, schemas, manifests, tests, docs, and first-party code are committed.

### Public-safe GitHub source repository

GitHub is the off-site home for the **sanitized instructions and machinery that can reconstruct MineScape**, not the family world itself. The existing `briggskellogg/minescape` repository is public, so it contains only MineDeck and Bridge source, first-party datapacks, merge tools, migrations, tests, documentation, example configuration, and nonsecret version/hash manifests. Branches and pull requests provide reviewable change history; tagged releases identify every tested MineScape source version. Automated GitHub checks may build/test source but never hold credentials, acquire restricted archives, read family state, or deploy directly to MineScape. Every release must still pass through MineJammer.

The repository excludes world/region files, backups, player names and UUIDs, whitelist/operator files, exploration databases, BlueMap tiles, logs, passwords/tokens, Tailscale state, production addresses, and third-party jars or packs whose licenses do not permit redistribution. A strict `.gitignore`, example-only secrets, and automated secret scanning enforce this boundary.

GitHub is not a suitable live-world backup. Minecraft region files are compressed binary files that change repeatedly; Git cannot store useful small differences for them. Git LFS stores a new full binary object for each changed version, so ordinary play would create fast, expensive, and awkward history. The live world therefore uses MineDeck's versioned backup system and a genuinely separate storage destination.

## 7. MineDeck V1.0

MineDeck is a Windows-native .NET LTS application with a persistent background mode, system-tray indicator, local ASP.NET dashboard, and atomic JSON operational state. Secrets stay outside Git and configuration: the Bridge token is read from a restricted local token file/environment boundary, while the administrator credential is stored only as a salted password hash. Repeated shortcut clicks use a single-instance lock and cannot start duplicate server processes. Scheduled-task registration and production secret provisioning remain release gates.

MineDeck is an out-of-game family-server control plane, not a Minecraft content system. It creates no menu item, currency, quest book, NPC, home token, warp network, land claim, or child-facing progression layer. The Windows owner authenticates to MineDeck separately from the parent's Minecraft session; every write reaches the server through a narrow typed Bridge capability and an immutable administrative audit rather than blanket console access.

### Status indicator

MineDeck performs a real Minecraft Server List Ping against the server. A Java process is not considered proof of life.

| State | Tray presentation |
|---|---|
| Offline | Gray, with the word “Offline” |
| Starting/stopping/backup/maintenance | Amber with the current operation |
| Online | Green only after a valid Minecraft response; tooltip shows players, ping, uptime, version, and backup age |
| Fault | Red with a plain-language reason |
| MineJammer active | Blue/J badge in addition to the applicable server state |

MineDeck separately checks the Bridge, BlueMap, free disk, backup age, and manifest integrity. Closing the dashboard leaves the supervisor and server running in the tray.

### Required shortcuts

**Launch MineScape & MineDeck** will ensure MineDeck is running, validate the manifest, start MineScape if necessary, wait for real online status, and open/focus the dashboard.

**Play MineScape** will perform the same idempotent startup, wait for online status, and open/focus the official Launcher for its pinned MineScape Quick Play entry. Its safe V1.0 promise includes one final Launcher click, as described in the release gate.

No PowerShell window is the health indicator or required user interface. No shortcut simulates Launcher clicks, toggles a gamemode, or selects the Steward profile. If the official Launcher changes, startup and health verification continue working and MineDeck falls back to plain manual profile guidance instead of guessing.

### MineDeck pages

- **Home:** MineScape/MineJammer state, players, start/stop, backup health, and plain-language problems.
- **Players:** pending join requests, approve/deny, access role, whitelist state, suspend/revoke, current session mode, and whether movement counts toward family exploration. MineDeck has no home, claim, teleport, or starter-kit system.
- **Characters:** capacity/usable/black-blocked hearts, Crystal renewals, character generation, valid death history, Retirement Pending review, technical pardons, Hall of Ancestors, and transactional new-character start.
- **Stewardship Journal:** private factual event timeline, provenance/evidence, parent notes, and append-only reversals; explicitly no score, ranking, moral label, or gameplay effect.
- **Rewards:** locked natural Matcha economy at 1×, rare-event history, stable dragon entitlement, and the narrow authenticated Reward Correction flow.
- **Progress:** read-only per-player mirror of the frozen Matcha advancement tree, showing visible/completed nodes and criteria for parent support; it cannot grant, complete, skip, or redefine an advancement.
- **World:** fog-of-war map, settlements, borders, milestones, and content provenance.
- **MineJammer:** create/refresh, launch, play, compare, approve, commit, discard, and restore.
- **Settlements:** register a friendly settlement, review detected existing buildings, and place/lock civic stones; no property or home assignment.
- **Remote Access:** Tailscale/MagicDNS state, private Mac connection address, last remote test, and an explicit statement that no public port is open.
- **Backups:** create, verify, test-restore, name milestone, and recover.
- **Updates:** world gameplay/worldgen is shown as frozen; stage only an approved Matcha visual-resource candidate or replaceable operational/client maintenance, never apply automatically.
- **Activity:** Ledger links, promotions, restores, ward changes, and administrative audit.
- **Steward:** arm the parent client, request/end a temporary Creative lease, enter a reason, inspect the recovery point and affected Ledger events, and open the official Launcher without automating it.

### Parent Family Play and Steward mode

The parent's enrolled UUID defaults to ordinary **Family Play**: Survival, Mortal Hearts, factual Stewardship journaling, and family fog of war all apply exactly as they do for the children. MineDeck administration does not make that Minecraft character an operator.

The parent can administer process health, whitelist access, backups/restores, family assistance, civic wards, character review, fog/map policy, and MineJammer promotions from MineDeck without changing the in-game character. Direct arbitrary command execution is not a MineDeck feature. Exceptional world intervention uses only the explicit Steward workflow below.

An explicit, reauthenticated **Steward** session excludes future movement from family fog, hearts, the family Stewardship journal, statistics, and advancements; its actions remain in the separate administrative audit. It begins in Survival. Creative is a separate audited lease, normally 10–15 minutes and never more than 30, available only to the parent UUID. Bridge snapshots inventory, armor, offhand, XP, effects, health, location, spawn, recipes, advancements, and statistics; supplies a temporary inventory; blocks pickup, dropping, trading, container leakage, and player/entity damage; and restores the snapshot, Survival mode, and starting location on completion, timeout, disconnect, MineDeck failure, Bridge restart, or server restart. A visible in-game `STEWARD CREATIVE` indicator remains for the lease.

Bulk building is redirected to MineJammer. A planned production Creative lease requires a recent verified recovery point and a written reason. World edits that do occur remain in Ledger and are recoverable. Child UUIDs are categorically ineligible, client profile metadata grants no authority, and failure always returns to Survival with no administrative permission.

MineDeck's **Open Steward Client** button arms a short-lived session and opens/focuses the official Launcher for the isolated parent profile containing OrthoCamera. It is deliberately not a third desktop shortcut. The parent chooses the pinned profile if the Launcher cannot expose a supported selection mechanism.

### Player approval and first arrival without advance usernames

The whitelist is always enabled, but no Java names are needed during construction. MineDeck creates no neutral homes, claims, kits, or per-player spawn points. Enrollment works as follows:

1. A new player tries to join while signed into a legitimate Java account.
2. The server authenticates the account but refuses world entry because it is not yet approved. MineScape Bridge records the authenticated profile name and immutable UUID as a short-lived **Pending Player** request.
3. MineDeck shows the avatar, current name, UUID, request time, and connection source. The parent approves or denies it and assigns `Child`, `Family`, `Guest`, or the unique `Parent` access role.
4. Approval writes the UUID-backed whitelist entry. On first successful login the player receives only the frozen Matcha/vanilla initialization and an empty ordinary player inventory—no MineScape items, currency, equipment, teleport token, or claimed property.
5. The player's first position is the single public world-spawn point on a safe natural path beside the starter village bell/common area. `spawnRadius` is zero or tightly bounded after collision testing. Players without a valid bed respawn there through ordinary Minecraft behavior; sleeping in a bed changes their spawn normally.

MineDeck also supports manual add-by-current-name, suspend, revoke, and rename reconciliation. Pending requests expire, are rate-limited, and never cause the whitelist to be disabled. Session mode—not a silent per-player checkbox—controls exploration: Parent Family Play counts, while explicit Steward and all MineJammer sessions do not. Switching modes affects only future movement. Player identity and access records remain local and never enter GitHub.

## 8. MineDeck fog of war

MineScape Bridge records exploration; BlueMap only renders terrain. Raw BlueMap is administrator-only and cannot bypass the mask.

Raw history is stored per child, while the default family map shows the union:

1. **Currently visible:** chunks within the configured server-visible radius of any online child, shown from current BlueMap state.
2. **Previously explored:** chunks once visible to that child/family but not currently visible, shown darkened and desaturated from the last-known snapshot rather than omniscient live state.
3. **Never explored:** no child has made the chunk visible; it is opaque even if an administrator, MineJammer, or pregeneration touched it.

Child Family Play and parent Family Play count. Explicit parent Steward, all Creative, and all MineJammer movement are excluded. Merely owning the Parent role does not erase ordinary family exploration, and switching modes never changes history retroactively. Travel sampling accounts for boats, horses, minecarts, portals, and elytra. Each dimension has its own history. Switching from Steward back to Family Play while standing in never-explored territory requires returning to remembered terrain or explicitly revealing the current visibility footprint; orthographic or Creative scouting can never become a hidden teleport benefit.

For later additions, MineDeck proposes only parcels that are never explored and normally at least 1,024 blocks from remembered territory. Deliberately changing an explored place to create “that was not there before” requires an explicit audited override; remembered map tiles remain old until the family returns.

## 9. Seed and world construction

1. Build the exact locked stack and compatibility overlay before any seed world exists.
2. Generate temporary MineJammer seed laboratories for `6246468738900744`. Administrator scouting in these worlds never enters canonical exploration history.
3. Audit spawn terrain, biome variety, oceans, the complete natural Far Rim approach, all vanilla structures, ordinary village distribution, and every official Terralith structure family. Count rather than merely locate the finite progression supply: multiple usable strongholds; multiple fortresses with blaze and Nether-wart access; multiple bastions and smithing-template opportunities; every critical Nether biome/resource; End gateways inside the boundary; enough End-city ships and elytra for the enrolled family plus recovery margin; and enough trial chambers, ancient cities, and spawners for the intended heart economy. Confirm that Matcha beta villages cannot locate and that every expected Terralith family can.
4. Prefer the best naturally generated ordinary Minecraft village near safe spawn terrain, searching approximately 3,000 blocks before proposing a seed change. Terralith structures remain natural discoveries rather than being moved or hand-placed.
5. Preserve the starter village: repair only unsafe path holes or inaccessible doors, improve spawn-proof lighting without architectural replacement, install civic wards, and leave every building, farm, lot, road, and future home choice to ordinary play.
6. Set the public world spawn to a safe natural path at the village bell/common point and test a zero or tightly bounded `spawnRadius`. Configure `online-mode=true`, whitelist, no blanket player operator, `spawn-protection=0`, Easy onboarding difficulty with a parent switch to Normal, insomnia off, and the Matcha-compatible mob-griefing choice established by the test suite. All privileged operations travel through authenticated, typed MineDeck/Bridge capabilities.
7. Configure World Border per dimension with looping off.
8. Pregenerate and benchmark a measured 2,048-block starting envelope. Use its real time and bytes-per-chunk to estimate the complete finite world. If the host and second backup target can comfortably hold it, freeze the full Overworld and Nether before V1.0; otherwise retain the exact generator stack and generate on demand. Validate the End before any broad End pregeneration. Pregenerated chunks remain “never explored” until a child sees them.
9. Validate automatic registration, house detection, and wards across representative plains/desert/savanna/snowy/taiga villages plus both Terralith fort variants. Prove that vanilla village containers resolve to frozen Matcha tables, Terralith containers perform their original Terralith roll before any one-time bonus, the Loot Adapter enriches at most one container per eligible start, food normalization is exact, and Matcha global profession trades apply to ordinary fort villagers. Classify zombie/abandoned vanilla variants by census and template evidence before consecration; empty or hostile variants become restorable sites, never automatic sanctuaries. Seed-lab scouting never enters canonical fog; production settlements remain unseen until Family Play reaches them.
10. Promote the complete release candidate as a gold master, create permanent V1.0 backups, and only then permit child accounts to join MineScape.

## 10. Starter town and friendly settlements

No player receives a MineScape-created or assigned home. The selected natural Minecraft starter village is a shared beginning, not a finished hub or property system. It contains:

- One active registered Warding Stone in every designated building.
- One active registered stone at the bell/town center.
- A modest ordinary-villager census with Matcha trades, refugee-growth capacity, and one iron golem; no quest NPCs or prewritten dialogue.
- Useful beds, lighting, basic food/cooking support, and empty building space.
- No MineScape starter kit, gift chest, rare alloy, Divine Fragment, Crystal Heart, portal menu, warp token, or prescribed story. Natural upstream loot and the fixed world-wide Loot Adapter remain where structure generation places them; the starter receives no special guaranteed cache.
- A safe public arrival point on the natural path by the bell/common area. Players choose an existing building with one another or build elsewhere; MineDeck records no ownership claim.

Only the civic stones are protected; houses, containers, farms, roads, and ordinary town blocks remain governed by Minecraft itself. MineDeck records coordinates, settlement, building, placement source, Matcha version, batched-sanctuary membership, and administrator action. Parent-only MineDeck controls can unlock or relocate a stone after creating a backup.

Generated ordinary and Terralith villages are registered automatically from their structure starts and jigsaw pieces only after a living-settlement census check. Zombie, empty, or hostile variants are recorded as restorable sites and receive no automatic wards. Minecraft has no durable universal concept for a later player-built town, so MineDeck also provides **Register Friendly Settlement**:

1. Select settlement bounds.
2. Scan beds, doors, bells, workstations, paths, and building clusters.
3. Suggest one stone position per building plus a center.
4. Let the parent approve or move each suggestion.
5. Place, protect, and record the stones.

Thus every naturally generated living village and every later player-built civilization recognized as friendly can share the same warding culture. Registration never makes surrounding houses, containers, or town blocks unbreakable; it protects only the civic ward anchors.

## 11. MineJammer and safe commits

MineJammer never shares a writable file with MineScape. It can create a clean seed lab, an empty construction world, or a consistent staging clone made from a verified MineScape backup. It has a conspicuous staging icon, MOTD, dashboard banner, and in-game warning.

The first V1.0 promotion is a complete gold-master promotion before any child enters. Later world promotion is restricted to **reserved parcels**:

1. MineDeck finds never-explored territory and reserves a rectangle aligned to 512×512-block Anvil regions with a safety buffer.
2. It records the source backup, exact manifest, fog state, purpose, and baseline hashes.
3. MineJammer is created from that consistent backup.
4. Work occurs only inside the reservation.
5. MineDeck stops MineJammer and reports changed regions, entities, points of interest, containers, footprint, and provenance.
6. It rejects the commit if a child explored the parcel, production hashes changed, global data changed, or no fresh rollback backup exists.
7. It stops MineScape cleanly and transactionally replaces the matching `region`, `entities`, and `poi` files using temporary files and atomic renames.
8. It merges settlement/ward registrations through the Bridge rather than copying global save data.
9. It boots and validates MineScape; any failure restores the pre-commit hashes automatically.

Later parcel commits never copy MineJammer's `level.dat`, player data, advancements, statistics, scoreboards, maps, or unrelated global state. Configuration/mod changes use a separate manifest promotion workflow.

## 12. Backups, security, and recovery

MineDeck never copies a running world casually. It requests a save/flush and uses a Windows Volume Shadow Copy snapshot when available. If a consistent snapshot cannot be obtained, it performs a short controlled maintenance stop; it never silently falls back to an unsafe live copy.

Retention target:

- 24 recent session/hourly backups.
- 30 daily backups.
- 12 monthly backups.
- Permanent milestones: seed accepted, V1.0 gold master, first child login, dragon defeated, major settlements, and every pre-upgrade/pre-promotion state.
- A second copy on another physical disk or NAS.
- Optional encrypted off-site copy.

Every archive is hashed and includes world data, configs, manifests, first-party packs, MineDeck databases, Ledger data, settlement/ward registry, and provenance. A restore is first verified and booted in MineJammer. Production is swapped only after that test and its displaced world remains recoverable until validation completes.

On the home network, only Minecraft production and its single hash-addressed resource-pack endpoint are reachable by the children's devices. MineDeck controls, MineScape Bridge, MineJammer, and raw BlueMap bind to localhost by default. No public Internet exposure or router forwarding is part of V1.0.

### Private access from the parent's Mac

[Tailscale](https://tailscale.com/pricing) creates an encrypted private network, called a **tailnet**, between explicitly enrolled devices. Install it on the Windows host and the parent's Mac only; the children's LAN devices do not need it. The Windows host is named `minescape-server`, and [MagicDNS](https://tailscale.com/docs/features/magicdns) lets the Mac use that stable name instead of remembering an IP address.

- Minecraft is reachable from the Mac at `minescape-server:25565` only through the tailnet.
- [Tailscale Serve](https://tailscale.com/docs/features/tailscale-serve) privately proxies MineDeck's localhost dashboard to tailnet-only HTTPS. Funnel, which would make it public, is explicitly disabled.
- Tailscale grants permit only the parent's identity to reach Minecraft and MineDeck ports; MineJammer and Bridge remain inaccessible unless a temporary, audited maintenance grant is enabled.
- MineDeck retains its own parent authentication even behind Tailscale.
- Tailscale and MineDeck run unattended after Windows reboot, and the host's plugged-in power policy prevents sleep while serving.

This permits road play without opening the router. It does not wake a powered-off computer: remote access still depends on the Windows PC being on and awake, the server service being healthy, and the home internet connection working. Wake-on-LAN and public fallback access are outside V1.0.

### What the backup destinations mean

- **External physical disk:** a USB hard drive or SSD attached to the host. It is simple and gives recovery if the internal drive fails; the safest routine disconnects or isolates it between backup jobs.
- **NAS:** a dedicated storage box elsewhere on the home network. It is convenient for automatic backups and physically separate from the PC, but it is still in the same house.
- **Encrypted cloud storage:** a private, encrypted copy stored off-site. It protects against theft, fire, or loss of both home devices, but may have a subscription and a slow first upload.

GitHub fills none of those roles for the live world. The V1.0 release gate requires at least one verified second copy outside the PC's primary internal drive. Its size and whether an external drive needs to be purchased are decided after the 2,048-block pregeneration sample measures the real world and backup footprint.

### Purchase policy

No Realms subscription, paid hosting, BuiltByBit city, commercial dungeon, paid mod, or paid server control panel is required. The approved Modrinth/Fabric foundation, MineDeck, MineJammer, and the private compatibility work cost nothing beyond the hardware and accounts already owned. Tailscale's current Personal plan is free for up to six users and unlimited user devices.

Every simultaneous player does need a Minecraft: Java Edition license on that player's Microsoft account; Java Edition cannot use Microsoft Store family sharing. Possible later purchases are therefore limited to missing child licenses and, after measurement, a suitable backup drive. A UPS is a sensible optional protection against short power failures, and encrypted cloud backup is optional defense against a house-level loss.

## 13. Ten-year support policy

No mod can honestly promise support for Minecraft versions ten years in the future. MineScape's preservation strategy is **reproducibility**, not continuous forced upgrading.

- Archive the exact Java runtime, server launcher, mods, datapacks, resource packs, configs, manifests, checksums, licenses, and source.
- Pin Minecraft 26.2 as long as necessary. A forever world does not require the latest client.
- Classify frozen Matcha 1.02 gameplay data, Terralith, Amplified Nether, Nullscape, MineScape Compatibility, MineScape Foundations, the Village/Loot Adapters and Settlement Registry, and the Mortal Hearts/reward-entitlement portions of Bridge/Core and MineScape Client as red/world-critical.
- Classify Fabric support mods, BlueMap, Ledger, replaceable Bridge telemetry, and MineDeck's user interface as amber/operational.
- Classify client performance, shader, visual, and audio mods as replaceable/green after testing.
- Freeze every third-party terrain and gameplay archive used by the gold master. The only elective authored-content update MineDeck may ever offer is a later official Matcha archive used as a **resource pack only**, under the compatibility and rollback gate in §3.2. It may never update Matcha data or any Stardust pack in the canonical world.
- Operational maintenance is separate from world content: a runtime, loader, security, performance, map, or administration replacement may be staged only when necessary and only with a backup, MineJammer clone, automated tests, manual playtest, explicit approval, and rollback point.
- Change one dependency category at a time.
- Never remove or swap a terrain generator in the canonical world.
- Keep a human-readable `INSTALL_HOST`, `INSTALL_CLIENT`, `OPERATIONS`, `RECOVERY`, `UPDATE_POLICY`, `WORLD_CONSTITUTION`, and `THIRD_PARTY` guide alongside automated setup.
- Perform a full recovery drill before each major change and regularly during the first months of operation.

## 14. Build sequence

### Phase 0 — decision freeze

Freeze the complete upstream Terralith structure/base-loot contract, at-most-one-container Matcha Loot Adapter, exact food normalization, fishing-biome classification, narrow Village Adapter, deterministic 19:4:1 curio-cache rule, Stardust-base compatibility ownership, public first-arrival/no-custom-home rule, controller acceptance contract, conventional boundary, supported Launcher behavior, family dragon-credit rule, visible-scar/full-renewal Mortal Hearts, factual Stewardship Journal, locked 1× per-event Matcha economy, visual-only Matcha update lane, Reward Correction, and parent Steward model. Player UUIDs arrive later through MineDeck's pending-player flow; hardware capacity and the second-backup size are measured rather than guessed.

### Phase 1 — preservation scaffold

Create the repository layout, schemas, artifact lock format, dependency/license catalog, downloader/verifier, private Java runtime, logging, and recovery bundle format.

### Phase 2 — MineDeck operational core

Build the Windows supervisor, tray, dashboard, real Minecraft ping, process management, typed Bridge operations, manifest validation, service recovery, and both shortcuts.

### Phase 3 — MineScape Bridge and Foundations

Build the narrow Fabric Bridge/Core and paired client HUD, exploration telemetry, capacity/blocked Mortal Hearts state machine, full Crystal renewal, cross-generation dragon entitlement, factual Stewardship Journal, 1× reward enforcement and correction grants, local authenticated controls, temporary Steward Creative leases, batched immutable civic-stone registry, automatic village/fort registration, deterministic relic/enrichment cache state, food normalization, save coordination, audit events, and first-party datapacks.

### Phase 4 — compatibility laboratory

Install and hash the exact upstream archives, detect collisions automatically, generate the Stardust-base compatibility overlay plus Village/Loot Adapters, validate load order, compare biomes/features/spawns/fishing tags, restore living vanilla villages, and suppress only Matcha's beta geometry/haunting. Prove that every official Terralith structure, placement set, template, processor, loot table, mob, and map tag remains upstream-authoritative; audit the whole Matcha acquisition graph, natural shared-table composition, 19:4:1 curio cache, one-per-start enrichment, exact food components, and no-reroll persistence; test the entire Matcha progression, scarred-heart HUD/full Crystal renewal, rewards, controller advancement chart, and Nullscape/End behavior; then decide whether the complete combined release passes.

### Phase 5 — isolated servers and client profile

Create MineScape/MineJammer instances, install the approved stack, enforce ports and security, serve the frozen Matcha 1.02 resource archive, build isolated Family and parent Steward official-Launcher profiles, and validate the entire Matcha interface with real controllers plus orthographic, shader, and fallback hardware presets. Exercise the resource-only candidate/rollback path without changing server data.

### Phase 6 — seed and settlement release candidate

Audit seed `6246468738900744`, measure ordinary villages and every official Terralith structure family at native placement, choose the natural starter village, set the public bell/common-area spawn and boundaries, and leave all homes to ordinary play. Validate vanilla-village Matcha loot, original-plus-capped Terralith enrichment, relic-cache completeness, fishing in every Terralith climate, automatic registration/ward anchors, and the batched sanctuary union; then test day/night, first arrival, scarred hearts and Crystal renewal, refugees, progression, caves, Nether, End, finite resource supply, and every dimension.

### Phase 7 — preservation and curation systems

Finish backups/restores, BlueMap proxy, three-state fog of war, last-known snapshots, character retirement/restart recovery, Stewardship Journal and Reward Correction review, unseen reservations, MineJammer cloning, comparison, transactional parcel promotion, and rollback.

### Phase 8 — V1.0 release

Run the acceptance suite, restore the release candidate from backup into MineJammer, create the immutable gold-master milestone, promote it to MineScape, configure Quick Play, and admit the children only after every gate passes.

## 15. V1.0 acceptance contract

V1.0 cannot ship until all of the following are demonstrated:

- Both shortcuts are idempotent and never start duplicate Java processes.
- Green status appears only after a valid Minecraft protocol response.
- Closing the dashboard leaves MineScape running; a hung server becomes Fault.
- MineScape and MineJammer cannot resolve to the same writable world path.
- A MineJammer edit changes no production byte before approval.
- The world seed and exact worldgen manifest are recorded and reproducible.
- The compatibility overlay demonstrably preserves the selected Matcha and Stardust behavior.
- Matcha 1.02 gameplay data is immutable and its resource pack, complete advancement tree, loot, Warding Stones, sleep behavior, and post-dragon surface change work.
- A later Matcha archive can be served as a resource pack without entering the world's datapacks, changing server data, breaking any frozen asset/item/GUI/sound reference, or preventing instant rollback to 1.02.
- All five living vanilla village families, both Terralith fort variants, and every other official Terralith structure family locate in appropriate biomes; Matcha beta villages cannot locate.
- The preferred seed contains a coherent natural Far Rim and the counted finite progression supply for every enrolled child plus recovery margin; a future content drop is never required to repair a missing fortress, wart source, bastion, stronghold, gateway, End ship, elytra, or critical resource.
- Realized village, fort, and complete Terralith structure density has been counted after native biome/exclusion filtering and remains suitable for the finite world; this gate may reject the seed but never rewrites Stardust placement.
- Generated living villages and forts automatically register every designated house and center ward without trusting decorative lodestones.
- Zombie, empty, and hostile village variants cannot be misclassified or automatically consecrated, and every starter-town loot table has been resolved and audited before release.
- Every restored vanilla-village chest resolves to Matcha's frozen `minecraft:chests/village/*` table, every Terralith container performs its original upstream table/roll unchanged, and ordinary fort villagers receive Matcha's frozen global profession trades.
- The Loot Adapter selects at most one eligible container per Terralith structure start, adds exactly one allowlisted frozen Matcha reference roll after the upstream roll, never rerolls after open/restart/restore, records provenance, and never injects Divine Favour or a Divine Fragment. Danger-tier Crystal Hearts can occur only through an unmodified referenced Matcha table at its native chance.
- Every Terralith biome is classified into one audited Matcha fishing climate or an explicit no-fish exception; the hot-wet predicate correction works; all reachable fish/food recipes work; and componentless Terralith foods receive exactly the matching frozen Matcha components without identity/count/slot/probability changes.
- Matcha's two Clay Fetishes remain naturally obtainable through frozen desert-well/trail-ruins archaeology. The four beta-only curios occur only through the one-time namespaced vanilla-village relic-cache rule; every cache is deterministic/auditable, none replaces upstream loot, none contains a progression-power reward, and the finite seed contains at least one complete set.
- Multiplayer dragon participation and Divine Favor distribution match the family rule chosen before release.
- Representative Overworld, Amplified Nether, and Nullscape areas generate without errors or broken transitions.
- Every civic stone resists players, pistons, and explosions; administrative unlock is backed up and audited.
- The loaded starter settlement remains at 20 TPS with comfortable tick headroom.
- A new character shows ten usable slots and zero black slots. Each valid death blocks exactly one visible slot; normal healing cannot fill it. A genuine Crystal Heart clears every blocked slot and adds exactly one capacity slot up to 30, so 7 usable + 3 black becomes 11 usable + 0 black.
- Duplicate, PvP, Steward, Creative, MineJammer, rollback, and administrative death events consume no permanent heart.
- A valid all-slots-black death enters Retirement Pending without setting impossible zero maximum health, deleting data, banning the account, or revealing terrain; technical pardon and a ten-usable-heart new-generation reset are transactional and recoverable.
- The estate-vault workflow can transfer an explicitly reviewed finite-world heirloom exactly once without deleting, duplicating, or leaking character-bound trophies and entitlements.
- Character reset, advancement reset, and restore never reopen a stable family person's one-time dragon entitlement.
- Falling a representative set of vanilla/Terralith trees neither duplicates resources nor damages unrelated builds.
- Pregenerated-but-unvisited chunks remain hidden.
- Child movement changes visible → remembered correctly; admin/staging movement does not.
- Parent Family Play counts toward fog/hearts/Stewardship; future movement in explicit Steward mode does not, and a mode change cannot erase already explored chunks.
- Remembered tiles do not reveal off-screen changes; never-explored tiles reveal nothing.
- Raw BlueMap cannot bypass fog of war.
- Parcel commits reject explored, divergent, out-of-bounds, global-state, or unbacked changes.
- An interrupted/failed promotion leaves either the old or new complete parcel and rolls back automatically.
- A production backup restores and boots successfully in MineJammer with world, dimensions, players, wards, and databases intact.
- Unwhitelisted users cannot join and management services are not reachable from another LAN device.
- An authenticated unknown player can create only a pending request; approval adds the correct UUID without disabling the whitelist, and revoke removes access.
- A newly approved player arrives at the single safe public village spawn with an ordinary empty inventory, no kit, claimed house, private ward, teleport token, or personal spawn assignment; bed spawn then behaves normally.
- The parent is ordinary Survival and cannot execute operator commands during Family Play; child UUIDs cannot acquire a Steward or Creative lease.
- A Creative lease requires reauthentication, reason, recovery point, hard expiry, and a conspicuous indicator; inventory/XP/effects/location/advancements/statistics restore exactly and no Creative item leaks through a drop, container, trade, restart, disconnect, or failure.
- OrthoCamera in the isolated parent profile grants no server authority, and Creative/orthographic travel 10,000 blocks away changes no family fog.
- Stewardship facts, parent notes, and reversals survive restart and remain private; V1.0 exposes no score, ranking, automatic moral label, or gameplay effect.
- Natural Divine Fragment and Divine Favour events remain exactly 1×; Divine Favour and Crystal Hearts cannot be multiplied.
- Reward Correction accepts only an authenticated allowlisted exact grant, never double-delivers across timeout/restart, suspends at retirement, and records world epoch plus immutable grant status.
- If MineScape never receives a future parcel, capital, dungeon, mob, item, or quest, the family can still progress from first spawn through every dimension and sustain building, exploration, collection, combat, automation, and settlement play indefinitely.
- Player names/UUIDs, exploration state, production addresses, credentials, worlds, and backups are absent from the Git repository and its history.
- From the enrolled Mac, Tailscale reaches Minecraft and the authenticated MineDeck view; from an unenrolled internet host, neither service is reachable.
- After a Windows reboot with no interactive login, unattended Tailscale and MineDeck recover; if the PC sleeps or powers off, MineDeck reports remote access unavailable rather than claiming the server is online.
- Clean Family and parent Steward client profiles can be rebuilt from their manifests and join through the official Launcher. With keyboard and mouse disconnected, the entire Matcha advancement chart—including tab changes, graph panning, node focus, multiline criteria/tooltips, and returning to play—plus every required recipe, crafting, inventory, container, trade, text-entry, death, and respawn screen is usable by controller alone.

## 16. Construction defaults now frozen

No child names, Java names, exact RAM allocation, paid content, or final backup-drive size is required to begin V0 construction. The current defaults are:

1. Maintain the private exact-version compatibility overlay with Terralith/Nullscape as the complete base definitions and only enumerated progression-critical Matcha deltas. If that cannot preserve complete Matcha progression without erasing Stardust behavior, release stops for an explicit decision; load order never chooses silently.
2. Keep the two durable shortcuts and the supported final official-Launcher click; never automate Microsoft authentication or Launcher UI coordinates.
3. Keep conventional non-looping limits of ±16,384 Overworld, ±2,048 Nether, and ±8,192 End, with an invisible V1 safety boundary, a complete seed-audited natural Far Rim at release, and any later authored spectacle restricted to unseen outer territory.
4. Keep Matcha's physical Divine Favour behavior and record shared MineDeck credit for children present at the first victory.
5. Restore ordinary living Minecraft villages at pinned vanilla frequency; keep Terralith 2.6.4's complete official structures, native placement, templates, mobs, map behavior, and every original loot roll; then add the frozen at-most-one-container Matcha enrichment/food-normalization layer chosen above. Suppress only Matcha beta villages and reserve post-V1 great cities for ultra-rare unseen-parcel curation.
6. Start every character at ten usable hearts; black one visible slot per valid death; make a genuine Crystal Heart clear every black slot and add one capacity; retire rather than destroy a character when all unlocked slots are black.
7. Keep only a private factual Stewardship Journal in V1.0; defer numerical karma and all moral effects.
8. Lock natural Matcha rewards at 1× with no per-player multiplier; permit only the narrow audited Divine Fragment Reward Correction.
9. Keep the parent an ordinary Survival player by default, with no blanket operator and only short audited Steward Creative leases.
10. Give every newly approved player the same public natural village arrival with no custom home, property claim, kit, or personal ward; ordinary beds and building choices belong to play.
11. Freeze all terrain and gameplay archives after gold master. Only a later official Matcha archive may be considered as a tested, reversible client resource-pack update; it never enters server data.
12. Use Tailscale with no router forwarding; Easy onboarding with a parent switch to Normal; batched but visibly functional immutable civic wards; and no paid/additive structure content in V1.0.

Construction begins when the user gives the explicit build instruction. Before child access—not before coding—MineDeck must also have a verified second backup destination outside the host's primary drive, selected after the storage benchmark, and the children's licensed accounts/devices must be enrolled by UUID through the pending-player flow.
