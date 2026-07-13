# MINESCAPE — private family Bedrock server (the forever project)

Owner: Briggs (gamertag **m00gu1**, operator). Players: two kids on Nintendo Switch
(LAN discovery; their gamertags live in `config/allowlist.json` — untracked, since
this repo is public), invited friends later.
Vision: a hand-built story RPG inside a free-roam survival world — set pieces,
dungeons, quests, characters with music and voices. Safety-first: allowlist-only,
Xbox-authenticated, self-hosted.

## Architecture

- **BDS 1.26.33.2** (official Bedrock Dedicated Server) in `server/`. World:
  `server/worlds/Minescape` — seed 6246468738900744 (lake village at spawn — beloved,
  never regenerate). Script API stable: `@minecraft/server` 2.8.0, `@minecraft/server-ui` 2.1.0.
- **Moogul Core** add-on — source of truth in `packs/`, deployed copies in
  `server/behavior_packs/` + `server/resource_packs/`. ALWAYS edit `packs/` then sync.
  - `moogul_core_bp/scripts/main.js` — entry; event router: `/scriptevent moogul:<name>`
    dispatches to `EVENTS` in `events.js`. Add features as new entries, never touch plumbing.
  - `events.js` — event library (ping, narrate, announce, celebrate, storm, calm,
    nightfall, gift, rally, genesis, weather, timelock).
  - `genesis.js` — crater carver (runJob) + sword-in-the-stone entity spawn + whisper-on-
    interact. Fired in-game: `/scriptevent moogul:genesis` (needs player source). NOT YET FIRED.
  - `ambience.js` — weather locks (dynamic property `moogul:weatherLock`, re-asserted
    every 600 ticks) + time locks. `moogul:weather clear|rain|thunder|natural`,
    `moogul:timelock day|night|natural`.
  - `telemetry.js` — chat/join/leave/death/block break-place/container-open/villager-
    &-PvP-hurt/position-heartbeat, one JSON line per event to stdout (`[MOOGUL-T]`
    prefix) for `deck.js` to pick up. Every subscription individually try/caught.
  - `marks.js` — named location bookmarks (`moogul:mark <name>` / `moogul:marks`),
    JSON blob in a world dynamic property. Everything targetable (dungeons, later epic
    events) points through a mark.
  - `karma.js` — automatic karma (flavor-only, reacts to telemetry's villager_hurt/pvp
    today; `moogul:karma <player> <+/-amt> <reason>` covers the rest by hand until
    dedicated detection exists) + a judge-only layer never triggered automatically:
    `moogul:jail/release/fine/grant`. Gold-bar economy + karma live in one dynamic
    property (`moogul:economy`); mirrored to the deck over the telemetry pipeline since
    scripts can't expose dynamic properties any other way. **Karma marker**: since
    there's no "opened inventory" event in the scripting API at all (client-side-only
    screen, the server never learns it happened), karma is shown by keeping a colored
    concrete block (5-tier gradient, lime→green→white→orange→red) pinned in inventory
    slot 9 at all times — pure color, no reading required, refreshed every ~2s.
  - `dungeon-modules.js` — the modular building system: small reusable procedural
    generators (corridor/bossArena/lootVault) composed into dungeons, same
    generator-function-with-`yield` pattern as genesis.js's crater. See
    `dungeons/README.md` for the hand-authored (`.mcstructure`) alternative path.
  - `dungeons.js` — placed, protected structures (not full dimensions). Registry in a
    dynamic property, locked/open gating via a key item at the door, ghost-block traps
    via a live position-poll watcher, boss-death clears the instance.
  - Custom entity `moogul:sword_in_stone` (BP entity + RP model/texture). Unbreakable,
    whispers when touched. Quest hook #1.
  - Custom entity `moogul:alligator_knight` — the first dungeon's boss (hand-authored
    low-poly geometry, not a vanilla mob reskin — see `dungeons/README.md` for why).
- **Command Deck** — `deck/deck.js` (Node, zero deps, localhost:8420 only) wraps
  bedrock_server.exe: SSE log feed, POST /cmd → stdin, crash watchdog (3 strikes/10 min),
  nightly 4AM backup (save hold/query/resume protocol, keeps 14), music pipeline:
  `.ogg` in `music/` → POST /rebuild-music → RP rebuilt + version bumped → restart ships
  to clients → `playsound moogul.music.<slug> @a`. UI: `deck/index.html` (pixel/CRT aesthetic).
- **Ops**: `1-SETUP.bat` (download/install BDS), `2-START-SERVER.bat` (deck, falls back to
  bare console without Node), `3-BACKUP.bat`, `4-TEST-WORLD.bat` (Spelljammer — a same-seed
  replica world for building/testing that never changes Minescape on its own; `refresh`
  copies Minescape → Spelljammer, `build`/`play` flip which one `server.properties`
  points at, `commit <name>` copies a finished `.mcstructure` exported from Spelljammer
  into `packs/moogul_core_bp/structures/`, the shared source of truth — see
  `dungeons/README.md`'s Path B for the rest of that flow), `FIX-LOCAL-JOIN.bat` (UWP
  loopback exemption, already run), `INSTALL-AUTOSTART.bat` (schtasks logon task +
  never-sleep — installed and run).

## Conventions & cautions

- Engine namespace stays `moogul:` / pack folders `moogul_core_*` (family brand);
  player-facing brand is **Minescape** (renamed from "Mooguland"; world folder, level.dat
  LevelName, server.properties all updated. server-name=m00gu1 so tile reads "m00gu1's world").
- `config/` holds source-of-truth server.properties/allowlist/permissions; setup.ps1 applies
  them (copy-if-missing-or-blank for allowlist/permissions).
- **Don't invite players via the `allowlist add` console command** — confirmed live to fail
  with `[ERROR] Could not add X to the allowlist` (BDS tries an Xbox Live lookup at
  add-time that doesn't reliably succeed). Use the deck's `POST /allowlist/add` endpoint
  (Ops tab → Invite) instead — it writes `server/allowlist.json` directly and sends
  `allowlist reload`, bypassing that lookup entirely. See `deck/deck.js`'s
  `addToAllowlist()`.
- Briggs's operator XUID lives in `config/permissions.json` (untracked — public repo).
- Never edit `server/worlds/Minescape` while the server runs. level.dat is binary NBT.
- Bedrock clients on consoles auto-update; after BDS updates, re-check @minecraft/server
  module version pinned in the BP manifest.
- Always syntax-check scripts before restart (`node --check` per file after stripping
  import/export, or just careful review) — a broken events.js kills the whole engine
  ("[Scripting] ... SyntaxError" in the deck feed / ContentLog).

## Roadmap

**READ `DIRECTIONS-FOR-CLAUDE-CODE.md` — it is the current product brief and supersedes
the list below.** It covers: Moogul design system + deck v2 (pixel, Hades-grade, from
Briggs's red/blue/yellow archetype trinity), world feed v2 + telemetry/parental oversight,
marks/dungeons/epic events, characters/stats/quests, karma, item creator, add-on installer,
dual-mode play + give console, QR invite cards.

## Original roadmap (superseded, kept for history)

1. **World Events system** — designed, AWAITING BRIGGS'S APPROVAL: cue engine, 5 families
   (Sky/Season, Heaven&Earth, Blessing, Omen, Threat), target/intensity/duration dials,
   `moogul:mark <name>` location bookmarks, ACTIVE-events strip in deck.
2. NPCs (native dialogue) → 3. Dungeons/set pieces → 4. Artifacts (cooldown abilities) →
   5. Quests (per-player state). Legends-style rally banner someday.
6. Remote friends (later, deliberately): port-forward UDP 19132 + DuckDNS + MCXboxBroadcast
   friend-join for consoles. Watch for CGNAT.
7. Migration target: always-on mini-PC (~N100) + UPS. The folder is the whole server — copy it.

## Current status

Foundation, the Moogul Design System, and World Feed v2/telemetry are built and
deck-verified. Marks, karma/judge (jail/fine/grant), and the first dungeon
(`alligator-knight-lair`, an Alligator Knight boss behind a ghost-block-trapped
corridor) are built but **not yet verified against a live server** — this is real
Bedrock scripting-API code written from an environment with no Minecraft to test
against. First restart after pulling: watch the World Feed closely for
`[Scripting] ... SyntaxError` or the per-module registration warnings each file logs on
a missing API (`[moogul] telemetry/karma/dungeons: '<x>' did not register...`). Genesis
(crater+sword) still not yet fired. Autostart not installed. No custom music uploaded yet.
