# MINESCAPE v2 — Build Directions for Claude Code

You are picking up a real, running project with a fresh context window. Read `CLAUDE.md`
first (architecture, conventions, cautions). This file is the forward plan: what Briggs
wants built next, in what order, and to what standard. Treat it as the product brief;
treat `CLAUDE.md` as the map.

**Progress so far:** §1 (design system) and most of §2 (world feed v2/telemetry) are
built and deck-verified — see `DESIGN.md`. From §3: marks and the first dungeon
(`alligator-knight-lair`, boss + ghost-block trap) are built; epic events are not
started. From §5: karma + a judge layer (jail/fine/grant — this went beyond §5's
original flavor-only scope, at Briggs's direction, folding in a dungeon-conversation
request) are built. The test-world tooling mentioned in the "Test in a throwaway world"
guideline below is also built: `4-TEST-WORLD.bat` / `scripts/testworld.ps1` — Spelljammer,
a same-seed replica world, chose the level-name-swap flow (not a second BDS copy),
plus a `commit` action for bringing hand-built `.mcstructure` pieces back into Minescape
one at a time. Everything past §2 is **unverified against a live server** — see
`CLAUDE.md`'s Current Status. §4 (characters/stats/quests), most of §6 (creator tools),
§7 (dual-mode/give console), §8 (invite cards) are not started.

## 0. State of the world (what already exists and works)

- Live Bedrock Dedicated Server 1.26.33.2 in `server/`, world `server/worlds/Minescape`
  (seed 6246468738900744, lake village at spawn — NEVER regenerate or edit while running).
- `packs/moogul_core_bp` + `packs/moogul_core_rp` — the engine. Event router in
  `scripts/main.js`: `/scriptevent moogul:<name>` → `EVENTS` in `events.js`
  (ping, narrate, announce, celebrate, storm, calm, nightfall, gift, rally),
  `genesis.js` (crater + unbreakable whispering sword-in-stone entity, not yet fired),
  `ambience.js` (persistent weather locks + time locks).
- Command Deck: `deck/deck.js` (Node, zero deps, localhost:8420) wraps the server exe —
  SSE log feed, POST /cmd, crash watchdog, 4AM backups, music pipeline
  (`music/*.ogg` → /rebuild-music → RP rebuild + version bump). UI `deck/index.html`.
- Ops bats in root; `INSTALL-AUTOSTART.bat` may not have been run yet — check with Briggs.
- Players: m00gu1 (Briggs, op — XUID in untracked `config/permissions.json`), plus two
  kids on Switch via LAN (gamertags in untracked `config/allowlist.json`; this repo is
  public, keep it that way). Allowlist-only, Xbox auth. This server hosts CHILDREN —
  safety and content-appropriateness are non-negotiable in every feature below.

**First session, before any feature work:** `git init`, commit everything as `v0.1-foundation`,
verify every `packs/**/*.js` parses (the engine died once from a truncated file — see
CLAUDE.md cautions), and confirm with Briggs that ping/weather/deck all work in-game.

## 1. THE MOOGUL DESIGN SYSTEM (do this before touching deck UI)

Briggs rejects the current deck aesthetic (generic magenta/cyan CRT hacker). The target
is **Moogul**: his personal system made bright, poppy, fun — for a family world console —
while staying unmistakably his. This is a real design task; research before pixels.

Research inputs:
- `https://briggskellogg.com` — his personal site. Restrained, literary, lowercase,
  quote-driven, minimal. Study `logo.png` and especially the three archetype sigils
  (`/assets/archetypes/instinct.webp`, `logic.webp`, `psyche.webp`) — **instinct/logic/psyche
  is his red/blue/yellow trinity**. Extract the actual hex values from those images
  (script it: download, sample dominant colors). That trio is the palette's DNA.
- His taste (from the site): Hades II, Valorant, Disco Elysium / Columbus, Dune,
  Marty Supreme / Ren, Sufjan Stevens, John Mayer. Note what unites them: strong
  silhouettes, disciplined palettes, emotional weight under a stylized surface.
- Briggs's explicit direction (his words): **pixelated**, and "cool developer pages that
  have vibes." The reference class is indie game-dev portfolio/landing pages: pixel-art
  sprites as UI furniture, a scene-setting hero moment, tiny ambient animations (a torch
  flicker, drifting particles, a sprite that blinks), hover states with personality,
  hidden delights. The deck should feel like the title screen of a game Supergiant would
  ship — not a admin panel, not briggskellogg.com's restraint, and not generic hacker-CRT.
- **Hades/Hades II is the north star for how "bright but designed" works**: saturated
  accents earn their heat because they sit on deep, warm darks; every element has a
  hand-crafted edge; glow is used like punctuation, not wallpaper. Valorant contributes
  the clean geometric layout discipline that keeps it legible. Disco Elysium contributes
  the courage to let text have mood.
- Minecraft itself: chunky pixels, hard edges, honest grids — the deck should feel
  cousin to the game the kids are inside.
- Concrete vibe moves to consider: pixel-art Minescape wordmark/crest (the sword in the
  stone as sprite logo), animated sprite accents per panel (weather panel has a tiny
  animated sky, music panel a bouncing note), feed entries that slide in like game toasts,
  a subtle parallax star/terrain footer. Motion small, purposeful, 60fps, never noisy.

Deliverable — `deck/design-system.css` + a short `DESIGN.md`:
- Palette: the three archetype colors as semantic tokens (e.g. red=action/danger/threat,
  blue=world/info/calm, yellow=reward/celebration/attention), lifted to bright poppy
  values, on warm near-black or warm paper — NOT the current neon-on-void. Test legibility.
- Type: a pixel display face for headers (Press Start 2P or similar, self-hosted in
  `deck/assets/` — the deck must work offline), a clean readable face for body/feed
  (system stack fine). Pixel type is for identity, never for paragraphs.
- Components: buttons, panels, tabs, feed entries, toggles, modals, form fields — chunky
  2px borders, hard shadows, pixel corner details, satisfying hover/press states.
  Playful but LEGIBLE — Briggs's words: "specific and beautiful to stay legible."
- Iconography: pixel-art icons per event family (16×16/24×24), no emoji in the final UI.
- Then rebuild `deck/index.html` on this system. Multi-page or tabbed app is fine now
  (Ambience / Events / Dungeons / Players / Quests / Library / Ops). Keep zero-build
  (plain HTML/CSS/JS served by deck.js) unless there's a compelling reason otherwise.

## 2. WORLD FEED v2 + PLAYER TELEMETRY (the parent dashboard — high priority)

Briggs needs to see, in one feed in plain English, everything that matters — and drill
into any player. This is both storytelling radar and parental oversight.

Architecture (scripts can't write files — route through stdout):
- BP module `telemetry.js`: subscribe to chat (`beforeEvents.chatSend` — log, don't block),
  join/leave, death (with cause), block break/place, container open, entity hurt
  (esp. villagers + player-vs-player), item pickup where the API allows. Emit one-line
  JSON to stdout: `console.log("[MOOGUL-T] " + JSON.stringify({t, kind, who, what, where}))`.
- deck.js: parse `[MOOGUL-T]` lines from the child's stdout → append to
  `deck/data/telemetry-YYYY-MM-DD.jsonl` (rotate daily, keep 90 days).
- Feed UI: translate BOTH telemetry and raw server log into human sentences —
  "kid1 joined", "kid2 fell from a high place near the lake village",
  "Briggs made it rain", "kid1 → kid2: 'come look at this'". Filters:
  All / Chat / Builds / Combat / Events / System. Raw log stays available under a
  collapsed "engine room" view. Deck event buttons must log in plain language, not code.
- Player pages: per-player timeline, chat history, deaths, blocks placed/broken counts,
  karma (see §5), last seen, current location snapshot.
- Forensics: "who broke blocks near X within N minutes" query — enough to settle
  "someone stole my diamonds" disputes with receipts. All local, nothing leaves the PC.

## 3. MARKS, DUNGEONS, AND EPIC EVENTS (the world-building arm)

**Marks first** — everything else targets locations through them:
`/scriptevent moogul:mark <name>` saves the caster's position (world dynamic property);
`moogul:marks` lists; deck gets a Marks manager (rename/delete/teleport-to). Every
targetable feature below accepts a mark name.

**Dungeon system** — dungeons as a *function*, placed into the world:
- Author dungeons as `.mcstructure` files in `dungeons/` (buildable in a flat creative
  world, exported with the structure command — document this workflow for Briggs) plus a
  `dungeon.json` manifest: name, size, spawn rules, loot, locked/open, key item, quest hooks.
- `moogul:dungeon place <name> [mark]` — loads the structure into the world at the mark
  (structure command via runJob for big footprints), registers it in a world-level
  dungeon registry, optionally carves an approach/entrance.
- **Locked dungeons**: gate blocks + a scripted door that opens only for holders of the
  key item (custom item) or players with a quest flag. **Open dungeons**: always enterable.
- Deck Dungeons tab: library of authored dungeons, place-at-mark flow, registry of placed
  dungeons with status (sealed/open/cleared), reset button (re-place structure).

**Epic world events** — the "god descending / black hole" tier. Build these as *cue
sequences* on a small cue engine (timeline of: announce, sound, camera, particles,
spawn, fill, wait). All target a mark or a player. Ship these six first:
- `godray` — a divine descent: column of light (beacon beam + particles), thunderous
  sound bed, camera pull for all players (Bedrock camera API), a glowing entity descends,
  speaks (narrate lines), ascends. Pure awe, no damage.
- `blackhole` — swirling particle vortex at a mark, ambient dread sound, nearby item
  entities get pulled in (velocity impulses), light dims (thick fog via fog command),
  ends with a shockwave particle burst. Cosmetic-destructive only unless Briggs opts in.
- `starfall` — meteor shower of light streaks landing loot capsules around a mark.
- `awakening` — the crater sword resonates: heartbeat sound worldwide, whispers to each
  player by name. (Ties to the existing sword set piece.)
- `swarm` / `siege` — wave assault on a mark (intensity-scaled, always cleans up).
- Keep the existing small events; migrate them onto the cue engine only if cheap.

## 4. CHARACTERS, STATS, QUESTS (the RPG spine)

- **Character creation**: on first join (and via a totem item later), a server-ui form:
  choose a calling — mirror the archetype trinity (e.g. Warden/instinct-red,
  Sage/logic-blue, Trickster/psyche-yellow; Briggs names them). Store on player dynamic
  properties. Each calling: starting kit, a small passive, a cosmetic title in chat.
- **Stats**: per-player: might, wit, spark (again the trinity) + hearts/karma. Stats grow
  from deeds (telemetry-driven: mining, exploring, helping) not grinding menus. Show via
  `moogul:me` (form UI) and on the deck player page.
- **Quest engine**: quests as JSON in `quests/` — id, giver, steps (talk/fetch/reach/
  defeat/discover), flags, rewards, calling-specific branches. Per-player quest state in
  dynamic properties (mind the size budget — serialize compactly). NPC dialogue via
  Bedrock's native NPC system + scripted `npc_dialogue` hooks. Deck Quests tab: authoring
  form (create/edit quest JSON), live view of every player's progress, manual
  grant/advance/reset controls. Quest lines can require callings and karma thresholds.

## 5. KARMA (adaptable, fun, never preachy)

- Score per player fed by telemetry: villager trades/gifts/heals and helping teammates
  raise it; hurting villagers, stealing from tracked chests, griefing tagged builds,
  excessive PvP lower it.
- **Consequences are flavor, not punishment**: high karma → villagers cheer and discount
  trades, occasional gift drops, friendly wolves; low karma → villagers hide, crows follow
  you (parrots re-skinned or bats), traders overcharge, the occasional dramatic personal
  raincloud. Nothing that hard-blocks play; everything reversible by doing good.
- ALL weights and consequences live in `config/karma.json` so Briggs tunes without code.
  Deck shows karma per player with a manual adjust (parent override) + event log of what
  moved it. Kids fighting → Briggs checks the feed, sees who hit whom first, karma answers.

## 6. CREATOR TOOLS (deck tabs that write game content)

- **Item creator**: deck form → name, rarity, base item, lore text, enchant-like effects
  (speed, glow, damage, cooldown ability from a preset list), texture (upload 16×16 PNG
  or pick from a generated palette-swapped set) → writes BP item JSON + RP texture/atlas
  entries + adds to a `moogul:give` registry → bumps pack versions → prompts restart.
  Custom items must be *giveable* (see §7) and usable as dungeon keys / quest rewards.
- **Add-on installer**: deck flow to install `.mcaddon`/`.mcpack` files Briggs buys or
  downloads (drop file in `addons/` or upload via deck) → unzip, validate manifests,
  surface what's inside (BP? RP? scripts? experimental toggles needed?) with a clear
  human summary BEFORE install → on confirm: copy to server packs + wire world JSONs +
  restart prompt. Refuse anything with malformed manifests. Keep an installed-addons
  registry with one-click disable (remove from world JSONs, keep files). Note honestly
  in the UI: Marketplace-purchased content is client-licensed and generally can't be
  installed server-side; this is for independently distributed add-ons.

## 7. PLAYING BOTH ROLES + GIVE CONSOLE (quality of Briggs's life)

- **Dual-mode**: `moogul:gm` toggle (or deck button targeting Briggs) flipping him
  survival ↔ creative with a saved-inventory swap (stash survival inventory to a data
  property/ender-chest-like store, restore on return) so admin mode never contaminates
  his player progress. Plus `moogul:ghost` (spectator-ish: invisible + noclip-adjacent
  via camera or gamemode spectator now that Bedrock has it) for watching the kids' quests
  unseen.
- **Give console** (deck Players tab): pick player → pick item (vanilla search list +
  every custom item from the registry) → quantity → give to inventory, or drop at their
  feet, or place into the chest nearest a mark (`replaceitem`/`give`/structure-aware
  chest fill). Preset kits (starter kit, explorer kit) as one-click bundles.

## 8. INVITE CARDS (friends → allowlist, kid-proof)

Reality check: consoles can't self-serve onto an allowlist; Briggs must add gamertags.
So make the *ask* frictionless:
- Deck generates a printable business card (PNG + print CSS page): Minescape branding
  (design system!), "You're invited to Minescape", a short unique invite code, and a QR.
- QR encodes `mailto:me@briggskellogg.com?subject=Minescape invite <CODE>&body=My gamertag is: ____`
  (or an SMS URI — ask Briggs which he wants). Parent scans, sends gamertag, done.
- Deck Invites tab: generate cards, track codes (created/claimed), one-click "allowlist
  this gamertag" when the reply arrives, and a note field (whose friend, which kid).
- QR generation: vendor a tiny zero-dep QR lib into `deck/` (keep the deck offline-capable).

## 9. Engineering ground rules (hold these)

- `packs/` is source of truth; sync to `server/` only via robocopy/PowerShell on Windows.
  Never trust a copy you didn't verify — the project already ate one truncated-file crash.
- `node --check` every JS file (engine files: strip import/export first or use a tiny
  esm-aware checker script) before any restart. A broken script kills the whole engine.
- Git: small commits per feature, tag before every risky change, `backups/` stays gitignored
  (world backups are big); DO commit `packs/`, `deck/`, `config/`, `dungeons/`, `quests/`.
- Restart discipline: pack-version bump + restart ships content to consoles; batch content
  changes to minimize restarts while kids are online (deck shows who's on — check it).
- Test in a throwaway world first for anything world-mutating (dungeons, epic events):
  `server.properties` level-name swap or a second BDS copy — document the flow you choose.
- Scope guard: this file is months of work. Build in the order above; each section ships
  something Briggs can touch that week. Ask him before re-architecting anything in
  CLAUDE.md's "Conventions & cautions".
