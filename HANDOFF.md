# Handoff runbook — from "code merged" to "kids can play"

Everything in this file happens on Briggs's actual machine — nothing here can be run
from wherever this repo's Claude Code session lives (that environment has no access to
the local PC, the live server, or the running deck). This is written to be handed to
**whatever is executing it** — a human, or an agent like Cowork with some mix of
terminal/file/in-game/browser access — without assuming which capabilities it has.
Every step below lists more than one way to do it; use whichever one you actually have.

**Everything built through this point is genuinely untested against a live Bedrock
server.** Every event registration in the new scripts is individually try/caught so one
bad one degrades gracefully instead of crashing the engine — but "degrades gracefully"
still means something is broken and needs a look. Take the QA gates seriously, in order.

## The three ways to drive this project

1. **In-game chat, as operator (m00gu1).** This is the *primary*, most reliable
   interface — literally how the engine is designed to be driven. Every judge/dungeon/
   marks/karma action is a `/scriptevent moogul:<name> <args>` command (see the table
   below). Anything with keyboard access to a logged-in, op'd Minecraft client can do
   every game-logic step this way.
2. **The Command Deck's web UI**, `http://localhost:8420` on the machine running the
   server. Click-driven; every deck button is described below alongside its scriptevent
   equivalent. Screenshot-and-click automation covers this fine.
3. **A terminal on the host machine** (PowerShell) — for the one-time git pull, running
   `1-SETUP.bat`/`2-START-SERVER.bat`, and optionally hitting the deck's HTTP API
   directly (`Invoke-RestMethod` / `curl`) as a third way to do anything the deck UI
   does — e.g. `Invoke-RestMethod -Uri http://localhost:8420/cmd -Method Post -Body
   '{"cmd":"scriptevent moogul:ping"}' -ContentType "application/json"`. Needs real
   shell access; only use this path if you actually have it.

**Before you start:** if more than one clone of this repo exists on the machine (this
came up during development — a `Desktop\GitHub\minescape` and a
`Desktop\Projects\minescape` were both referenced at different points), confirm which
one `2-START-SERVER.bat` actually runs from and do everything in that one. Mixing
clones means uncommitted setup/config drifts between them silently.

## Step 1 — get the code, sync it into the live server

Terminal, in the repo root:
```powershell
git pull origin main
.\1-SETUP.bat
```
`1-SETUP.bat` is the step people forget — `2-START-SERVER.bat` alone only starts the
deck, it does **not** copy `packs/` into `server/behavior_packs`/`resource_packs`. Every
script/entity/item built this session lives in `packs/` and needs this sync to actually
run. Safe to re-run; it refreshes packs/config without touching the world or the live
allowlist. Then:
```powershell
.\2-START-SERVER.bat
```
This opens `http://localhost:8420` automatically.

## QA gate 1 — does the engine even come up?

Watch the World Feed (deck UI, or the raw console) for the first ~10 seconds after
start. **Pass**: a line like `[moogul] Telemetry online.` / `[moogul] Karma + judge
online.` / `[moogul] Dungeons online. Known: alligator-knight-lair`, or warnings shaped
like `[moogul] telemetry: '<x>' did not register (...)` (a single signal quietly
no-op'd, everything else still runs — fine, not a failure). **Fail**: any
`SyntaxError`, or the engine not coming up at all. If it fails, stop here and report
back — don't touch anything else in-game yet.

## Step 2 — invite the kids

In-game chat (op): `/allowlist add "puffmoogul"` and `/allowlist add "dotmoogul"`.
Deck UI: Ops tab → Gatekeeping panel → type the gamertag → Invite. Terminal (if you have
deck API access): `POST /cmd` with `{"cmd":"allowlist add \"puffmoogul\""}`, same again
for dotmoogul.

## Step 3 — sanity + place the first dungeon

| Action | In-game chat | Deck UI |
|---|---|---|
| Sanity check | `/scriptevent moogul:ping` → should chat "pong" | Events tab → Ping button |
| Set a mark at your position | `/scriptevent moogul:mark entrance` | (marks are chat-only for now — no deck UI for this yet) |
| List marks | `/scriptevent moogul:marks` | — |
| Place the dungeon at that mark | `/scriptevent moogul:dungeon place alligator-knight-lair entrance` | Dungeons tab → select "The Alligator Knight's Lair" → type `entrance` → Place |

Placement spreads across several seconds (ticks), so don't stand in the footprint while
it's building.

## QA gate 2 — the dungeon itself

This is the one that most needs a human eye, not a script:
- Walk the corridor. Does the ghost-block trap section actually drop you when you step
  on it, and does the floor come back a few seconds later?
- Look at the boss. It's a hand-authored low-poly custom entity, never rendered before
  now — **does it actually appear** (not invisible, not a T-pose error)? This is the one
  genuinely unverified visual risk flagged in `dungeons/README.md`.
- Fight it. Does it attack, take damage, die, drop loot (diamonds/gold/emeralds, small
  chance of an enchanted diamond sword)?
- Check the loot vault chest has something in it.
- Deck's Dungeons tab should flip the status to "cleared" after the boss dies.
- Try the locked door *before* you have the key (`moogul:alligator_key`) — should block
  you with a chat message. `/give @s moogul:alligator_key`, then interact with the door
  — should open and stay open.

## Step 4 — exercise the judge tools

| Action | In-game chat | Deck UI |
|---|---|---|
| Jail a player (minutes optional, default 10) | `/scriptevent moogul:jail <name> <minutes>` | Players tab → select player → Judge panel → Jail |
| Release | `/scriptevent moogul:release <name>` | Judge panel → Release |
| Fine (gold bars) | `/scriptevent moogul:fine <name> <amount> <reason...>` | Judge panel → amount + reason → Fine |
| Grant (gold bars) | `/scriptevent moogul:grant <name> <amount> <reason...>` | Judge panel → amount + reason → Grant |
| Manual karma adjust | `/scriptevent moogul:karma <name> <+/-amount> <reason...>` | (chat-only for now) |

Use a throwaway/test account for this if you have one; otherwise do it to yourself.

## QA gate 3 — does jail actually feel right?

Get jailed once. Does the cage build around wherever you were standing? Can you still
place blocks ("build up") but not break any, anywhere? Does release teleport you back to
where you were? **Known limitation**: the cage isn't cleaned up after release — it'll
leave a small iron-bars structure behind. Fine for now, flagged in
`dungeons/README.md` if it bothers you in practice.

## QA gate 4 — the karma marker (built for the non-reading kid)

Open a player's inventory and look at the first slot in the main grid (right below the
hotbar row). **Pass**: there's a colored concrete block there — white/gray at karma 0,
shifting toward green as karma goes up, toward orange/red as it drops — and it updates
within a couple seconds of a `moogul:karma`/`fine`/`grant`/villager-hurt/PvP event.
Try to move it out of that slot — it should either refuse to move (locked) or reappear
there within ~2 seconds either way. That slot is intentionally reserved for this; it's
one slot out of 36 and not a hotbar slot, so it shouldn't cost anyone a tool slot in
practice, but it's worth noticing if it feels intrusive.

## If something's badly broken

Revert to the last known-good commit before this session's work:
```powershell
git log --oneline   # find the commit before this session (8535cde, "Minescape v0.1 - foundation")
git checkout 8535cde -- packs/ deck/ config/karma.json
.\1-SETUP.bat
.\2-START-SERVER.bat
```
Then report back what broke — don't force-push or rewrite history, just tell the repo's
Claude Code session and it'll fix forward.

## Everything already checked, so you don't have to re-check it

Every `.js` file parses (`node --check`), every new/changed JSON is valid, and the
deck's own side (telemetry ingest, player index, Judge panel, Dungeons tab, all new HTTP
endpoints) was exercised end-to-end against a synthetic test harness in a real headless
browser with zero console errors. What's *not* checked, because it can't be from outside
a live server: the actual Bedrock scripting-API calls, and anything about how the
Alligator Knight looks or moves in-game. That's what the QA gates above are for.
