# MineScape Bridge/Core

This directory is the first-party Fabric 26.2 mod. It intentionally keeps Minecraft-specific hooks thin and puts permanent world rules in ordinary, tested Java classes.

Implemented core:

- loopback-only, bearer-authenticated, typed HTTP Bridge endpoints for health, players, UUID-based whitelist access, and Family Assist requests;
- server-authoritative Mortal Hearts state: 10 initial slots, one visible blocked slot for each valid Family Survival death, full repair plus one capacity for a proven genuine Matcha Crystal Heart (maximum 30), and `RETIREMENT_PENDING` before zero maximum health;
- a server-to-client heart-state payload and a Vulkan-safe Fabric HUD element which draws every usable and blocked slot;
- family/Steward/MineJammer-separated exploration telemetry so administrative scouting and pregeneration never reveal the family map;
- a narrow Steward session state, UUID-backed civic-ward registry, and atomic durable state files;
- deterministic, append-only post-roll loot enrichment: at most one eligible container per structure start, one allowlisted frozen-Matcha table reference, and persisted provenance/idempotence. Because the reference is evaluated by Matcha itself, it carries that table's real prepared foods, climate fish, component-bearing equipment, diamonds, records, and rare special items rather than MineScape imitations;
- exact food-component normalization planning so a componentless Terralith-emitted loaf or cooked fish can receive the audited Matcha 1.02 components without changing its item ID, count, slot, probability, or original roll.

## Release-gated adapters

`releasegate/` contains interfaces for hooks whose exact 26.2 game behavior must be proven against the pinned Matcha 1.02 and Terralith 2.6.4 archives. The Mortal Hearts Fabric lifecycle is now implemented, including exact-Survival classification, durable monotonic death identity, genuine Crystal consumption, max-health enforcement, retirement lockout, and the black-slot client overlay. It remains release-gated for exact-stack crash injection, respawn grace, resource-pack visuals, and controller proof. Production still lacks the game adapters for structure-start/container discovery, post-vanilla-loot composition, settlement detection/ward protection, chunk visibility sampling, and native whitelist mutation.

The Fabric entrypoint starts the real authenticated Bridge, installs Mortal Hearts, and registers the heart sync payload. The client entrypoint registers its receiver and HUD. Until every remaining release-gate adapter and exact-stack test passes, the jar is a development artifact, not a production world mod.

Exact status at this checkpoint:

| Boundary | Status |
|---|---|
| Fabric server lifecycle → loopback Bridge | Wired and compiling |
| Bridge authentication/routing → durable whitelist intentions and Family Assist tickets | Wired; native game mutation/action execution remains gated |
| Heart payload registration/sender → client receiver/black-slot HUD | Wired; pure layout tests cover capacities 10–30 and absorption, while exact visual/resource-pack proof remains gated |
| Death hook and valid-death classification | Wired with an exact-Survival classifier and a durable vanilla death-ordinal receipt; exact-stack crash/respawn-grace proof remains gated |
| Genuine Matcha Crystal Heart consumption | Wired with exact frozen-1.02 components and durable playerdata reconciliation; crash injection remains gated |
| Usable max-health attribute and Retirement Pending join lockout | Wired; authenticated MineDeck retirement/pardon/new-generation workflow remains gated |
| Ward break/piston/explosion enforcement | Release-gate interface only |
| Terralith structure/container discovery and post-roll application | Release-gate interface only; the deterministic planner, marker contract, and provenance ledger are complete |
| Family/Steward/MineJammer chunk sampling | Release-gate interface only; the separated durable ledger and fog-state logic are complete |
| Steward inventory isolation and timed game-mode restore | Release-gate interface only; the lease authority/state is complete |

## Build

Required: JDK 25, Gradle 9.5.1, Minecraft 26.2, Fabric Loader 0.19.3, Fabric API 0.155.2+26.2, and Loom 1.17. Minecraft 26.2 ships unobfuscated Mojang names directly, so this project intentionally declares no Yarn or separate mapping artifact.

The repository includes the pinned Gradle wrapper metadata and wrapper jar. With JDK 25 available, run:

```powershell
.\gradlew.bat clean test build
```

The Bridge binds only to `127.0.0.1`. Its token is generated at first start and must be read by MineDeck from the local host; it must never be committed or placed in a desktop shortcut.

## HTTP contract

All routes require `Authorization: Bearer <token>` and return JSON.

- `GET /v1/health`
- `GET /v1/players`
- `GET /v1/whitelist`
- `POST /v1/whitelist` with `{"uuid":"...","displayName":"optional"}`
- `DELETE /v1/whitelist/{uuid}`
- `GET /v1/family-assist`
- `POST /v1/family-assist` with `{"playerId":"...","action":"SET_EASY|SET_NORMAL|START_AT_DAWN|RESCUE_TO_PUBLIC_SPAWN|MATCHA_HINT","reason":"..."}`

The Bridge exposes no arbitrary console command endpoint.
