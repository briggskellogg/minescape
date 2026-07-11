# The Moogul Design System

Design language for the Command Deck (and, later, in-game UI touchpoints). Built to
Briggs's brief in `DIRECTIONS-FOR-CLAUDE-CODE.md` §1: bright, poppy, pixel-forward,
game-console-grade — Hades II's restraint, Valorant's geometric clarity, Disco Elysium's
willingness to let text carry weight, Minecraft's honest chunky grid. Not the deck's old
neon-on-void hacker-CRT look.

## Added taste anchor: Planescape × Spelljammer, in Minecraft pixels

Standing direction from Briggs, folded into the system for every phase from here on —
most load-bearing in **Phase 3 (Marks, Dungeons, Epic Events)**, since that's where the
world's cosmology actually gets built:

- **Planescape** (Sigil, the City of Doors; the Lady of Pain; portals each keyed to a
  specific symbol/item; law–chaos–good–evil factions as *philosophies*, not just teams;
  Tony DiTerlizzi's baroque, elongated, symbol-heavy illustration) → **dungeon entrances
  are portal-gates, not doorways.** A locked dungeon (§3 of the brief) is narratively a
  *planar rift* sealed until its key item — itself a small sigil/rune object — is
  presented. Pixel iconography: a ring/arch with a swirling color-keyed core (already
  primed by the trinity palette — an instinct-red rift reads as hostile/dangerous, a
  logic-blue rift as a calm waypoint, a psyche-yellow rift as a reward vault). The
  existing calling trinity (Warden/Sage/Trickster, §4) already functions like a
  Planescape faction system — three competing philosophies, not just three classes —
  lean into that instead of downplaying it. Marks (§3, `moogul:mark`) are the sigil: a
  personal waypoint rune placed in the world, not a generic pin.
- **Spelljammer** (helmed ships crossing wildspace between crystal spheres; brass-and-
  wood nautical hulls sailing a starfield; the sense that the overworld is one "sphere"
  among many reachable ones) → the epic events (§3: godray, blackhole, starfall,
  awakening, swarm/siege) get a *wildspace* visual vocabulary — drifting star debris,
  void-dimming fog, portals as the connective tissue between "here" and "elsewhere"
  rather than one-off VFX. `starfall` in particular should read as *something arriving
  from another sphere*, not generic meteors. A drifting spelljammer-hull silhouette is a
  strong candidate for a future footer easter egg or a rare wandering-trader set piece —
  small, discoverable, not a redesign of the whole deck chrome (the deck itself stays
  Hades/Valorant-restrained per the section below; the *planar/cosmic* voice belongs to
  the world-building systems, not the admin UI).
- **Practical split**: the Command Deck's own chrome (buttons, panels, tabs) stays
  exactly as specified below — this new pillar is about *world content* (dungeons,
  marks, epic events, the crest/lore layer), not a second competing UI language. Where
  it does touch the deck is additive: a couple of new icon glyphs (portal ring, sigil/
  rune) join the existing set when Phase 3 needs them, and the Dungeons tab's copy/
  iconography should speak in this voice once it's built.

## ⚠️ Palette status: provisional

The brief asks for the red/blue/yellow trinity to be **extracted from the actual pixels**
of the three archetype sigils at `briggskellogg.com/assets/archetypes/{instinct,logic,psyche}.webp`
— "that trio is the palette's DNA." This build environment's outbound network policy
blocks `briggskellogg.com` (confirmed: proxy returns `403` on every attempt), so the site
could not be reached to sample real hex values.

The three trinity colors below are a **reasoned placeholder** — a saturated poppy red,
a clear cobalt blue, a warm golden yellow — consistent with the instinct/logic/psyche
naming, not measured from the real sigils.

**To fix:** open the three sigil images yourself (browser devtools color-picker, or any
image editor) and send me the three hex values. They're defined in exactly one place —
the `TRINITY` block at the top of `deck/design-system.css` — so swapping them is a
one-line-per-color edit; nothing else in the system depends on the specific values.

```css
--instinct: #E8382A;   /* provisional — replace with real instinct.webp color */
--logic:    #1F5FFF;   /* provisional — replace with real logic.webp color */
--psyche:   #F5B700;   /* provisional — replace with real psyche.webp color */
```

## Palette

Three accent hues, each with one job, each used **at most one per surface** — a fourth
color anywhere is a special-occasion event, not a default:

| Token | Job | Where it shows up |
|---|---|---|
| `--instinct` (red) | Danger / destructive / irreversible | remove-from-allowlist, deny, delete dungeon, crash watchdog — **kept rare and precise**, since this is a kids' server and the Gatekeeping panel shouldn't read as alarming by default |
| `--logic` (blue) | Info / world-state / default chrome | ambience, server status, general UI — the most-used accent, since most of the deck is descriptive, not urgent |
| `--psyche` (yellow) | Reward / celebration / attention | new feed entry, event fired, invite claimed — a flash that *decays*, never a resting background |

Sitting on a **warm** near-black ramp (`--bg-deep` → `--bg-page` → `--bg-panel` →
`--bg-card`, each a step lighter/warmer) — not the old blue-black void. Accents earn
their saturation by sitting on warm dark and staying small (borders, icon fills, stripes)
rather than covering large areas. Glow (soft blur) is reserved for hover/active/arrival
punctuation — never an ambient resting effect.

## Type

Two faces, two jobs, deliberately maximized contrast between them:

- **`Press Start 2P`** (self-hosted, `deck/assets/fonts/`, SIL Open Font License 1.1 —
  free to vendor/redistribute) — wordmark, panel headers, tab labels, short button labels,
  HUD-flourish numbers. Never below 10px, never a full sentence.
- **System font stack** (`system-ui, -apple-system, "Segoe UI", sans-serif`) — everything
  meant to be *read*: World Feed sentences, form fields, help text, future quest/NPC
  dialogue. This is a parental-oversight tool as much as a game console — the sentence
  "kid1 fell from a high place near the lake village" must never fight the chrome around it
  for attention.

## Components

- **Borders**: 2px hard-edged default, 3px on outermost frames. No anti-aliased hairlines.
- **Shadows**: solid offset duplicates at rest (`2px 2px 0`/`4px 4px 0`/`6px 6px 0`, zero
  blur) — the retro-HUD "pixel drop shadow." Soft glow-blur only appears layered on top,
  only on hover/active/arrival.
- **Corners**: sharp rectangles by default; a small stepped pixel-notch (`clip-path`, not
  `border-radius`) on panel headers, tabs, and modals where a corner needs interest.
- **Buttons**: rest = fill + accent border + hard shadow; hover = border/text brighten,
  shadow gains a glow layer (~150ms); press = shadow collapses to 0 and content shifts by
  the shadow's former offset — a pixel-snap, never a scale transform.
- **Tabs**: chunky notched folder-tabs. The active tab sits flush with the panel below
  (no shadow); inactive tabs carry the hard shadow and a muted fill — geometry itself
  signals state, not just color (Valorant's lesson: shape > color for reducing cognitive
  load).
- **Feed entries**: card with a 4px colored left-edge stripe by category, small pixel
  category icon, plain-language sentence in the clean face, mono timestamp. New entries
  slide in (180ms, stepped/pixel-snap easing) with a one-second glow-decay highlight.
- **Toggles**: chunky physical lever (stepped travel, not an iOS pill) with a tiny 3-frame
  "thunk" squash on flip — these are switches on the actual world (weather lock, timelock),
  not generic settings.
- **Modals**: heaviest border/shadow weight in the system; open with a fast hard scale-snap
  over a translucent warm-dark scrim (no bounce); close reverses the same snap.
- **Form fields**: inset "socket" treatment (shadow direction reversed) so a field reads as
  recessed, not raised; focus adds a sparing accent-color glow.

## Motion — the short version

Quiet by default; real fireworks reserved for things that deserve them. Concretely:
one idle "tell" per panel maximum (and it should double as real status — the server LED
only breathes while the process is actually live; it doesn't loop for decoration). Nothing
loops faster than once a second. Never more than one attention-grabbing animation on
screen at once. Body text and feed sentences never animate while being read. Destructive
actions never auto-confirm via a countdown. `prefers-reduced-motion` disables every idle/
ambient animation, keeping only instant functional feedback.

## What shipped in this pass

- `deck/design-system.css` — the full token + component system described above.
- `deck/assets/fonts/PressStart2P-Regular.{woff2,ttf}` + `OFL.txt` — self-hosted, offline.
- `deck/assets/icons/sprite.svg` — a hand-authored 16×16 pixel icon set (sword, speech
  bubble, pickaxe, star, megaphone, gift, flag, ping, rain/thunder clouds, sun, moon,
  locks, door, key, person, scroll, book, cloud, wrench, status LEDs) plus a 24×24
  sword-in-the-stone crest for the wordmark. No emoji in the final UI, per the brief.
- `deck/index.html` rebuilt on the system as a tabbed app: **Ambience / Events / Ops** are
  fully wired to the existing engine (weather, timelock, music, world events, narrate/
  announce, server start/stop, raw console, allowlist). **Dungeons / Players / Quests /
  Library** are styled, consistent placeholder tabs — their real functionality is Phases
  2–7 of `DIRECTIONS-FOR-CLAUDE-CODE.md` and needs the live server to build and test against
  in-game, not something buildable blind from outside your machine.

## Next design debt

- Real trinity hex values (see above).
- Once Phase 2 (telemetry) lands, the World Feed filter bar (All/Chat/Builds/Combat/
  Events/System) becomes real — right now the feed is re-skinned on the new system but
  still only categorizes what today's engine actually emits (`[deck]` system lines,
  `[moogul]` event lines, errors).
