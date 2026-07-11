# MINESCAPE — the forever server

A private, allowlisted Minecraft Bedrock world for dotmoogul, puffmoogul, and invited friends.
Native Bedrock end to end: everything renders faithfully on their Switches. No translation layers.

## First night — three double-clicks

1. **`1-SETUP.bat`** — downloads the official Bedrock Dedicated Server (1.26.33.2),
   installs the config, allowlist, and Moogul Core packs. One-time (safe to re-run).
2. **`2-START-SERVER.bat`** — launches the world through the **Command Deck**
   (http://localhost:8420 opens automatically). Windows Firewall will ask once — Allow.
   *Deck needs Node.js (LTS, from nodejs.org). Without it, this starts a plain console instead.*
3. Before the kids join: edit `server/allowlist.json` and replace `YOUR_GAMERTAG_HERE`
   with your own gamertag (or use the deck's INVITE box).

## Making yourself operator (one-time)

Join the world once, then look in the deck's World Feed for your join line — it shows your
`xuid`. Put it in `server/permissions.json`:

```json
[ { "permission": "operator", "xuid": "PASTE_XUID_HERE" } ]
```

Restart. You now have full command power in-game; the deck has it regardless.

## How the kids connect (Switch)

Same Wi-Fi as this PC: Play → Servers/Friends tab — the server should appear as a LAN game.
If it doesn't, the fallback is BedrockConnect (change the Switch DNS to a BedrockConnect
server, pick any Featured Server, enter this PC's local IP, port 19132). For friends joining
from *their* houses you'll need port forwarding (UDP 19132) — do this deliberately, later.

## The Command Deck (http://localhost:8420)

Your control room. Live world feed, invite box, and one-click world events:
storm, calm, nightfall, celebration, gift drops, narration, and titled announcements.
Anything you'd type in a server console works in the raw command box.

**In-game storyteller commands** (type in chat, as op):

| Command | Effect |
|---|---|
| `/scriptevent moogul:genesis` | Carve the great crater beneath you + plant the sword in the stone |
| `/scriptevent moogul:rally` | Teleport everyone to you |
| `/scriptevent moogul:narrate <text>` | Italic story whisper to all players |
| `/scriptevent moogul:announce Title\|Subtitle` | Dramatic titled announcement |

The sword in the stone cannot be pulled, pushed, or broken. It whispers when they try.

## The world

- **Seed `6246468738900744`** — a cherry-grove lake valley with two villages: a relaxed,
  beautiful canvas for late-night building. Alternates if you ever regenerate:
  `302304127329527063` (four villages at spawn), `6942710633571786` (frozen-peak valley).
- Survival, easy difficulty, allowlist ON, Xbox-authenticated only, max 8 players.
- Cheats enabled (needed for events/ops) — fine on a private server.

## Folder map

```
MinecraftServer/
├── 1-SETUP.bat / 2-START-SERVER.bat / 3-BACKUP.bat
├── config/            ← source of truth for server settings
├── packs/
│   ├── moogul_core_bp/   ← the ENGINE: event router, genesis, sword, scripts
│   └── moogul_core_rp/   ← the LOOK: models, textures; music & voices go here later
├── deck/              ← Command Deck (Node): deck.js + pixel UI
├── scripts/           ← setup.ps1, backup.ps1
├── world_templates/   ← pack wiring copied into new worlds
├── server/            ← created by setup: BDS + the live world (worlds/Minescape)
└── backups/           ← created by 3-BACKUP.bat (keeps newest 30)
```

**Back up `server/worlds/` like family photos.** `3-BACKUP.bat` does it with one click
(best while the server is stopped).

## On "mods" — how content works here

Bedrock's equivalent of mods is **add-ons** (behavior + resource packs). The rock-solid ones
for a private server are the ones we build — they can't break your world or your trust.
When you want community content, the trustworthy sources are CurseForge (Bedrock section)
and MCPEDL — download the `.mcaddon`/`.mcpack`, and we'll vet and install it together into
`server/behavior_packs` / `resource_packs` + the world JSONs. Marketplace packs can't be
installed on a dedicated server — that content stays on the client.

## Roadmap (the deck grows with the story)

- **Music & voices per location**: audio files into `moogul_core_rp/sounds/` +
  `sound_definitions.json`, triggered by proximity scripts. (TTS or recorded voices both work.)
- **Dungeons-style artifacts**: custom items with cooldown abilities.
- **Hordes & bosses**: wave events and multi-phase fights via the event router.
- **Legends-style rally banner**: allied mobs that follow and fight for you.
- **NPC characters**: Bedrock's native dialogue system — questgivers with your writing.
- When the world outgrows this PC: move the whole folder to an always-on mini-PC. The world
  is just files; the forever plan is a $150 N100 box in a closet.
