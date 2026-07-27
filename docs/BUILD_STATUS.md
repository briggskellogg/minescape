# Build status

This file is updated by construction and preflight; it is deliberately more conservative than the design plan.

## Current state

- Canonical V1 design: frozen.
- Legacy Bedrock project: baseline preserved under `legacy/bedrock-v0` and tag
  `legacy-bedrock-v0`; the later Spelljammer/autostart/invite-fix tip is preserved separately by
  tag `legacy-bedrock-v0-final`.
- MineDeck source: builds cleanly on .NET 10; 12/12 executable tests pass. Current Bridge routes interoperate, and production launch is bound to the exact server bundle, family instance mode, finite-world configuration, and authenticated manifest-verified Bridge identity; unfinished mutations fail closed.
- Bridge/Core and MineScape Client source: builds cleanly on Java 25/Fabric 26.2; 19/19 tests pass. The Mortal Hearts Fabric lifecycle, durable death/Crystal receipts, exact-Survival classifier, retirement lockout, and absorption-aware black-heart HUD are implemented. Exact-stack crash injection, respawn-grace behavior, and visual/controller proof remain release gates, as do the game adapters for wards, loot, fog, native whitelist, Steward operations, backups, and promotion.
- MineJammer artifact/compatibility toolchain: implemented and tested. The exact Matcha/Terralith/Nullscape hashes, empty biome-delta decision, and five source-hash-bound Matcha heart overrides are reviewed; the pinned official vanilla input is acquired and `MineScape-Villages.zip`, `MineScape-Fishing.zip`, and `MineScape-Compatibility.zip` are generated. Real exact-stack evidence remains a closed gate.
- Pinned third-party artifacts: all 43 exact Modrinth files are downloaded into the ignored local cache and their published SHA-512 values verify; no third-party binary is committed.
- Fabric server launcher: exact 26.2 / Loader 0.19.3 / Launcher 1.1.1 artifact is pinned by SHA-512 and staged only in the ignored cache and MineJammer lab. Production deliberately has no launcher.
- Local V0 runtime checkpoint: inert MineScape and MineJammer skeletons are reconstructed under ignored `var`; the seven ordered datapacks and managed finite-world configuration are installed, production has `launch_permitted=false` with its launcher withheld, both retain `eula=false`, and no world exists.
- Production world: not generated.
- Seed `6246468738900744`: preferred, not yet accepted by an exact-stack seed audit.
- Desktop shortcuts: created only after the repository is moved to its permanent Desktop path and MineDeck is published/configured there.
- V1.0 family release: blocked until all acceptance gates pass. The code-owned 11-component adapter-completion interlock is explicitly committed incomplete with `promotion_permitted=false`; manual evidence cannot unlock promotion around unfinished adapters.

The major remaining evidence is exact-stack `/reload`, crash/restart, respawn-grace, progression, seed, and controller testing; completion of the real game adapters; a verified independent backup and restore drill; and reproducible Family and Parent Steward client profiles. MineJammer laboratory boot requires the owner's separate EULA acceptance; production must remain `eula=false` until the owner intentionally performs the later gold-master release step.

No child should join a world produced from this repository until `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\preflight.ps1 -Release` reports success and a separate backup destination has passed a restore drill.
