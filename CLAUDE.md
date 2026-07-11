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
  - Custom entity `moogul:sword_in_stone` (BP entity + RP model/texture). Unbreakable,
    whispers when touched. Quest hook #1.
- **Command Deck** — `deck/deck.js` (Node, zero deps, localhost:8420 only) wraps
  bedrock_server.exe: SSE log feed, POST /cmd → stdin, crash watchdog (3 strikes/10 min),
  nightly 4AM backup (save hold/query/resume protocol, keeps 14), music pipeline:
  `.ogg` in `music/` → POST /rebuild-music → RP rebuilt + version bumped → restart ships
  to clients → `playsound moogul.music.<slug> @a`. UI: `deck/index.html` (pixel/CRT aesthetic).
- **Ops**: `1-SETUP.bat` (download/install BDS), `2-START-SERVER.bat` (deck, falls back to
  bare console without Node), `3-BACKUP.bat`, `FIX-LOCAL-JOIN.bat` (UWP loopback exemption,
  already run), `INSTALL-AUTOSTART.bat` (schtasks logon task + never-sleep — NOT YET RUN).

## Conventions & cautions

- Engine namespace stays `moogul:` / pack folders `moogul_core_*` (family brand);
  player-facing brand is **Minescape** (renamed from "Mooguland"; world folder, level.dat
  LevelName, server.properties all updated. server-name=m00gu1 so tile reads "m00gu1's world").
- `config/` holds source-of-truth server.properties/allowlist/permissions; setup.ps1 applies
  them (copy-if-missing-or-blank for allowlist/permissions).
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

Server + deck running with Ambience Engine; awaiting in-game verification of repaired
events.js (`/scriptevent moogul:ping` → pong) and first FORCE RAIN test. Autostart not
installed. No custom music uploaded yet. Genesis (crater+sword) not yet fired.
