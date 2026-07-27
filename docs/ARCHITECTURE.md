# Architecture

## Trust boundaries

| Boundary | Owns | May not do |
|---|---|---|
| MineScape | Permanent world, players, first-party server state | Read/write MineJammer worlds; auto-update worldgen |
| MineJammer | Disposable clones, artifact analysis, seed labs, candidate parcels | Write production before a backed, validated promotion |
| MineDeck | Process supervision, typed administration, audit and presentation | Expose a raw console; make the parent OP; create child content systems |
| Bridge/Core | UUID identity, hearts, fog events, wards, loot provenance, narrow commands | Trust client authority; accept unauthenticated remote requests |
| MineScape Client | Black-heart HUD, controller/accessibility diagnostics, visible Steward state | Grant server permissions or reveal unexplored terrain |

Production and staging never share a writable world path. Runtime state lives under `var/` and is excluded from Git. The source repository can reconstruct an empty tested deployment but never contains a family world.

## Main flows

```text
Desktop shortcut -> MineDeck single instance -> validate manifest -> start/attach server
                                                -> protocol ping -> tray state
                                                -> official Launcher (Play flow)

Minecraft clients <-> Fabric server <-> Bridge typed loopback API <-> MineDeck
                           |                    |
                           |                    +-> audit/state
                           +-> worlds/logs/first-party state

MineScape backup -> MineJammer clone -> tests/diff -> approval -> transactional promotion
```

Fog of war has three states per dimension and family: currently visible, remembered from a last-known snapshot, and never explored. Pregeneration, BlueMap, MineJammer, Creative, and explicit Steward scouting do not mark family exploration.

## Content composition

Pinned upstream archives remain unmodified. Generated first-party layers are reproducible outputs tied to their input hashes. Stardust definitions own collided terrain. Matcha additions are enumerated. Restored vanilla village structures naturally call Matcha's frozen village tables. Terralith keeps every original loot roll; a separate idempotent post-roll adapter may enrich at most one eligible container per structure start.
