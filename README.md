# MineScape

MineScape is a preservation-first Minecraft: Java Edition family server built around three deliberately separate systems:

- **MineScape** — the canonical world and its first-party Fabric Bridge/Client code.
- **MineJammer** — the disposable compatibility, seed-audit, testing, and staged-change laboratory.
- **MineDeck** — the Windows tray supervisor and parent dashboard.

The children's first playable world is V1.0. V0.x worlds exist only inside MineJammer and may be discarded. The preferred seed is `6246468738900744`; it becomes canonical only after the exact locked stack passes the seed and finite-resource audit.

## World promise

Minecraft remains the game. Matcha Flavoured 1.02 is the frozen gameplay/economy author. Stardust Labs supplies the complete Terralith, Amplified Nether, and Nullscape geography. MineScape restores living vanilla villages, suppresses Matcha's beta-village geometry, preserves the complete Terralith structure suite, adds a small deterministic Matcha enrichment to eligible Terralith discoveries, and adds family safety/preservation systems—not quests, classes, custom NPCs, Pokémon, or a parallel RPG.

New players arrive at one public natural village spawn with an ordinary empty inventory. MineScape creates no custom home, property claim, starter kit, personal ward, warp, or questline. Every designated building in a friendly generated settlement receives an immutable functional civic Warding Stone, but houses remain ordinary editable Minecraft buildings.

Mortal Hearts are visible: deaths black out one unlocked heart; a genuine Crystal Heart clears every black heart and adds one capacity up to 30; all slots black retires that character without deleting the person or world.

## Repository map

```text
minescape/
├─ MineScape/                 Fabric Bridge/Core, client HUD, datapacks/config
├─ MineJammer/                artifact, compatibility, seed and promotion tools
├─ MineDeck/                  Windows supervisor, tray and dashboard
├─ docs/                      canonical plan, operations and preservation guides
├─ ops/                       build/install/shortcut/preflight scripts
├─ artifacts/                 lock metadata only; downloaded binaries are ignored
├─ var/                       local MineScape/MineJammer worlds and state (ignored)
└─ legacy/bedrock-v0/         tagged, inactive Bedrock prototype
```

## Construction status

This repository is the reconstructible source and control plane. It intentionally does not commit Minecraft worlds, player identities, exploration history, credentials, backups, third-party jars, or licensed archives.

The audited construction checkout has resolved and hash-verified all 43 pinned third-party files, acquired the official Minecraft server bundle, reviewed the exact-input Matcha compatibility policy and its five source-hash-bound heart overrides, and generated the Villages, Fishing, and Compatibility packs. The current source suites pass 19/19 Java tests and 12/12 MineDeck executable tests. Those caches and generated archives are deliberately ignored, so a fresh clone must reconstruct them by following the host-install sequence.

Construction and automated tests do not require family accounts. A gold-master world still cannot be released: production remains `eula=false`; the real game adapters must be completed and explicitly enabled through the code-owned 11-component promotion interlock; exact-stack `/reload`, crash/restart, respawn-grace, seed, and controller runs need evidence; the Family and Parent Steward client profiles must be built; and an independent backup destination must pass a restore drill. Children join only afterward through the UUID pending-player flow.

Start with [the canonical master plan](docs/MINESCAPE_V1_MASTER_PLAN.md), then follow [host installation](docs/INSTALL_HOST.md) and [client installation](docs/INSTALL_CLIENT.md). The authoritative build state is in [BUILD_STATUS.md](docs/BUILD_STATUS.md).

## Preservation rule

The world-critical stack freezes after gold master. Matcha 1.02 gameplay and all Stardust worldgen stay pinned. A later official Matcha release may contribute only individually audited assets to a reversible resource-only derivative built against the frozen 1.02 data. Operational software changes are staged in MineJammer and may always be rejected.

The previous Bedrock prototype remains recoverable as the baseline folder/tag
`legacy/bedrock-v0` and as the final Bedrock-only Git tag `legacy-bedrock-v0-final`; neither is
part of the Java build.
