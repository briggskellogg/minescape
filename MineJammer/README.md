# MineJammer

MineJammer is MineScape's disposable compatibility laboratory. It acquires exact third-party inputs into an ignored local cache, records provenance and SHA-512, builds deterministic private adapters, and refuses unsafe world promotion. It is not the family server and never owns a production world.

## Safety model

- The canonical input manifest is `manifests/artifacts.json`; `schemas/artifact-manifest.schema.json` and `schemas/artifact-lock.schema.json` describe source and resolved lock forms.
- Third-party jars/zips belong only in ignored `artifacts/` or another private cache. They are never Git content.
- Every acquired Modrinth file is checked against the SHA-512 published for its exact immutable version ID. Project license and API provenance enter the lock.
- Pack builds are deterministic zips. Unknown collisions, schema drift, missing tables, stale structure mappings, ambiguous food components, or an unreviewed compatibility allowlist stop the build.
- World cloning and parcel planning are dry-run first. Production, staging, and the pristine backup must be disjoint. Parcel plans accept only aligned region/POI/entity files in a reserved unseen area, require a still-verifiable applied clone receipt, and reject deletions or global state.
- Production parcel application is intentionally unavailable in this version. MineJammer re-derives the entire diff and every path/dimension/store/region/bounds identity, but it will not write until Bridge Ed25519 verification and atomic one-time token consumption exist.

## Run it

From this directory in PowerShell:

```powershell
.\minejammer.cmd manifest-validate manifests\artifacts.json
```

Resolve all 43 exact Modrinth pins, download them, verify SHA-512, inspect archive safety, and write the literal lock and acquisition report:

```powershell
.\minejammer.cmd acquire manifests\artifacts.json `
  --cache artifacts `
  --lock manifests\manifest.lock.json `
  --report reports\local\acquisition.json
```

Use `--metadata-only` to validate IDs, versions, projects, licenses, and URLs without downloading binaries. A full offline verification is:

```powershell
.\minejammer.cmd verify-lock manifests\manifest.lock.json --cache artifacts
```

Core laboratory flow:

```powershell
.\minejammer.cmd collisions artifacts\matcha\Matcha_Flavoured.zip artifacts\terralith\Terralith.zip --left-name matcha --right-name terralith --report reports\local\matcha-terralith.json
.\minejammer.cmd compat-candidates --matcha artifacts\matcha\Matcha_Flavoured.zip --terralith artifacts\terralith\Terralith.zip --nullscape artifacts\nullscape\Nullscape.zip --report reports\local\compat-candidates.json
.\minejammer.cmd build-compat --matcha artifacts\matcha\Matcha_Flavoured.zip --terralith artifacts\terralith\Terralith.zip --nullscape artifacts\nullscape\Nullscape.zip --policy config\compatibility.policy.json --output build\MineScape-Compatibility.zip --report reports\local\compatibility.json
.\minejammer.cmd build-village-adapter --vanilla artifacts\minecraft\vanilla-data.zip --matcha artifacts\matcha\Matcha_Flavoured.zip --output build\MineScape-Villages.zip --report reports\local\villages.json
.\minejammer.cmd build-fishing-adapter --matcha artifacts\matcha\Matcha_Flavoured.zip --terralith artifacts\terralith\Terralith.zip --policy config\fishing-climates.json --output build\MineScape-Fishing.zip --report reports\local\fishing.json
.\minejammer.cmd build-loot-spec --matcha artifacts\matcha\Matcha_Flavoured.zip --terralith artifacts\terralith\Terralith.zip --policy config\loot-adapter.policy.json --output build\loot-adapter.lock.json
.\minejammer.cmd build-food-spec --matcha artifacts\matcha\Matcha_Flavoured.zip --terralith artifacts\terralith\Terralith.zip --output build\food-normalizer.lock.json
.\minejammer.cmd build-curio-spec --matcha artifacts\matcha\Matcha_Flavoured.zip --output build\curio-cache.lock.json
```

The compatibility policy is reviewed only for the exact frozen Matcha, Terralith, and Nullscape SHA-512s. It preserves Stardust's complete collided biome JSON and emits five exact-source Matcha heart-mechanic overrides so FabricHeartLifecycle is the sole heart authority. Any archive hash, collision count, or reviewed function hash change invalidates that decision. Village restoration also needs vanilla 26.2 data extracted from the pinned official server distribution; MineJammer does not redistribute Mojang data.

For a future official Matcha release, the resource-only builder emits `assets/**`, pack metadata, credits/licenses, and provenance—never `data/**`. Its conservative reference guard requires every frozen 1.02 asset to remain unless individually approved:

```powershell
.\minejammer.cmd build-resource-only --frozen artifacts\matcha\Matcha_Flavoured.zip --candidate artifacts\matcha-candidate\Matcha_Flavoured.zip --policy config\resource-only-allow-missing.json --output build\Matcha-Visual-Only.zip --report reports\local\matcha-visual.json
```

World operations:

```powershell
.\minejammer.cmd clone-world --source D:\MineScape\world --destination D:\MineJammer\staging
.\minejammer.cmd clone-world --source D:\MineScape\world --destination D:\MineScapeBackups\pre-parcel --receipt private\backup-receipt.json --apply
.\minejammer.cmd parcel-plan --production D:\MineScape\world --staging D:\MineJammer\staging --policy config\parcel.policy.example.json --exploration private\explored-regions.json --backup-receipt private\backup-receipt.json --output private\parcel-plan.json
```

The first clone command is a dry run; rerun it with `--apply` before editing staging. Keep the pristine backup untouched: planning and later readiness checks hash its entire tree again. `parcel-commit` presently performs readiness validation only when given a current, structurally valid `minescape.bridge-offline-token.v1`; `--apply` always fails closed. The token contract is bound to the exact plan ID, production digest, world path, five-minute validity window, and one-time use, but those claims are not authority until MineJammer can verify Bridge's signature and atomically consume the token. `parcel-recover` remains available only for inspecting or recovering a transaction journal created by a future authorized implementation.

Seed/release harness:

```powershell
.\minejammer.cmd seed-audit --evidence private\seed-evidence.json --policy config\seed.policy.json --report reports\local\seed.json
.\minejammer.cmd preflight --policy config\release-gates.json --evidence-dir ..\var\release\evidence --lock ..\artifacts\artifacts.lock.json --minecraft-server-lock ..\artifacts\minecraft-server.lock.json --cache ..\artifacts\cache --report reports\local\preflight.json
```

Preflight uses code-owned evaluator/schema pairs for every canonical gate; editing the policy cannot substitute a weaker evaluator. A separate code-owned 11-component adapter-completion interlock is committed with every component incomplete and `promotion_permitted=false`; even fully populated manual evidence cannot bypass it. Acquisition, compatibility, village, and fishing reports are evaluated structurally and tied back to the artifact lock. Village evidence must also name the exact inner vanilla-server SHA-512 in the official Minecraft server lock. The seed gate runs `seed-audit` rather than trusting a result flag. Manual gates are metadata-bound human attestations, not automated proof that a feature exists: they require the exact gate/schema identity, world epoch, current artifact-lock hash, a server-bundle hash, UTC start/completion timestamps, a named tester and machine, and at least one concrete observation. They expire after the policy's configured window, and the separate adapter interlock prevents them from standing in for unfinished code. A file containing only `{"passed": true}` is never evidence.

## Known release barriers

This repository is tooling, policy, and automated verification—not proof that Minecraft has passed the gates. In the audited construction checkout, all pinned artifacts and official vanilla data have been acquired, the empty Matcha biome-delta decision and five exact-source heart overrides are reviewed and hash-bound, and the Villages, Fishing, and Compatibility packs have been generated. Those binaries and outputs are ignored local products, not Git content; every fresh clone must reacquire the official inputs and rebuild them through [the host-install sequence](../docs/INSTALL_HOST.md).

Before V1, the preferred seed must still be measured inside the finite border; the implemented Mortal Hearts lifecycle must pass exact-stack `/reload`, crash-injection, respawn-grace, HUD, and real-controller exercises; runtime game adapters for one-time loot, wards, fog, whitelist/Steward operations, backups, and promotion must be completed and proven; isolated Family and Parent Steward client profiles must be built; and a physically independent backup must pass a restore drill. A failed gate is expected to stop release.

## Tests

The repository uses only Python's standard library:

```powershell
$Python = '..\.tools\python-3.13.14\python.exe'
& $Python -c "import sys,unittest; sys.path.insert(0,'.'); suite=unittest.defaultTestLoader.discover('tests'); result=unittest.TextTestRunner(verbosity=2).run(suite); raise SystemExit(not result.wasSuccessful())"
```
