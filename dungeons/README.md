# Building a dungeon

Two ways to build one. Both end up as the same kind of thing to the engine — a
`build(origin)` function registered in `packs/moogul_core_bp/scripts/dungeons.js`'s
`DEFINITIONS` — they just fill that function differently.

## The modular mental model (either path)

Don't build a dungeon as one big bespoke thing. Build it as a small set of *pieces* —
an entrance, a corridor, a trap section, a boss arena, a loot vault — and compose them.
`alligator-knight-lair` (the first dungeon) is built exactly this way in code: it's a
`corridorModule` + a `bossArenaModule` + a `lootVaultModule` from
`packs/moogul_core_bp/scripts/dungeon-modules.js`, placed one after another. The next
dungeon should reuse those same three module functions in a new arrangement before
reaching for a fourth — a corridor is a corridor whether it leads to an alligator or
something else.

If you hand-build in creative mode, do the same thing: build one room at a time (an
entrance, a trap corridor, an arena), export each as its own small `.mcstructure`, and
compose them in a manifest rather than exporting one enormous structure. Small, reusable
pieces are easier to place, easier to reuse in the *next* dungeon, and — same as the code
side — much cheaper to test one piece at a time.

## Path A: procedural (code) — what shipped first

Fastest way to get a dungeon in the world without touching Minecraft's editor at all.
Write a generator function using `dimension.runCommand("fill ...")` /
`"setblock ..."`, spread across ticks with a `function*` + `yield` (see
`genesis.js`'s crater carver — this is the same pattern) so a big build doesn't freeze
the server. `dungeon-modules.js` has three ready-made pieces to compose from:

```js
import { corridorModule, bossArenaModule, lootVaultModule } from "./dungeon-modules.js";

build(origin) {
  const corridor = corridorModule(origin, "north", 14, { trapStart: 6, trapLength: 2 });
  const arena = bossArenaModule(corridor.end, { radius: 9, height: 8 });
  const vault = lootVaultModule({ x: arena.center.x + 6, y: arena.center.y, z: arena.center.z });
  function* run(dimension) {
    yield* corridor.run(dimension);
    yield* arena.run(dimension);
    yield* vault.run(dimension);
    dimension.spawnEntity("moogul:your_boss", arena.center);
  }
  return { run, trapTiles: corridor.trapTiles };
}
```

Downsides: you're building blind (no visual editor), so keep shapes simple — boxes,
circles-via-radius, straight corridors. Good for structural dungeons; not the tool for
anything that needs to actually *look* hand-crafted in a way boxes can't capture.

## Path B: hand-built in creative mode, exported as `.mcstructure`

The tool for when you want to actually build the thing yourself.

1. **Build in a flat creative world** — a superflat world (or a far-off creative area of
   the main world) so you're not fighting terrain. Build one room/piece at a time.
2. **Export with a structure block**: place a Structure Block (`/give @s structure_block`),
   set it to **Save** mode, give it a name (e.g. `moogul:my_dungeon_entrance`), define the
   bounding box around your build (the block has size/offset controls in its UI), hit
   **Export**. This writes a `.mcstructure` file.
3. **Move the exported file** into `packs/moogul_core_bp/structures/` (create that folder
   if it doesn't exist — Bedrock auto-loads any `.mcstructure` under a behavior pack's
   `structures/` folder, keyed by the name you gave the structure block).
4. **Load it from a dungeon definition** with the native `structure load` command instead
   of `fill`/`setblock`:

   ```js
   build(origin) {
     function* run(dimension) {
       dimension.runCommand(`structure load moogul:my_dungeon_entrance ${origin.x} ${origin.y} ${origin.z}`);
       yield;
       // ...load the next piece at an offset, same idea as the procedural corridor.end pattern
     }
     return { run, trapTiles: [] };
   }
   ```
5. **Register it** in `dungeons.js`'s `DEFINITIONS` object, same as any other dungeon —
   there's no separate registration path for hand-built vs. procedural.

## Ghost-block traps, on either path

A trap tile is just a floor coordinate the live watcher in `dungeons.js` (`watchTraps`)
polls against player position — it works the same whether the floor under it came from
a `fill` command or a loaded structure. Return a `trapTiles: [{x,y,z}, ...]` array from
your `build()` (world-space coordinates, one per tile) and the watcher handles the rest:
the block vanishes the instant someone steps on it, and quietly restores a few seconds
later. This is a **polling** mechanism (checked a few times a second), not instant —
good enough for "the floor gives way," not built for split-second precision traps.

## The boss creature

`alligator_knight` is a small hand-authored low-poly entity (a handful of boxes in
`packs/moogul_core_rp/models/entity/alligator_knight.geo.json`, same technique as the
existing sword-in-the-stone model) rather than a retextured vanilla mob — Bedrock's
vanilla geometry/texture identifiers aren't something that could be verified from the
environment this was built in, so a from-scratch simple model was the safer bet. If you
want a genuinely different-looking boss for the next dungeon, either author another
simple geometry the same way, or reskin this same rig (swap the texture, adjust
`packs/moogul_core_bp/entities/<name>.json`'s stats/loot) — much less work than a new
model.

## Known limitations (first pass, honestly flagged)

- **Jail cages aren't cleaned up.** `moogul:jail` builds a small cage wherever the
  offending player currently is and doesn't remove it after release — cheap materials
  (iron bars, stone, bedrock cap), but they'll accumulate wherever kids get jailed. Fine
  for now; worth a cleanup pass if it bothers you in practice.
- **Karma auto-detection only covers villager-hurt and PvP** — trades, gifts, theft from
  a "tracked chest," and grief on a "tagged build" all need dedicated detection that
  doesn't exist yet. Use `/scriptevent moogul:karma <player> <+/-amount> <reason>` by
  hand for those until it's built.
- **Everything in `packs/moogul_core_bp/scripts/{karma,dungeons,dungeon-modules,marks}.js`
  is genuinely untested against a live server** — built and syntax-checked from an
  environment with no access to a real Bedrock Dedicated Server. Watch the deck's World
  Feed closely the first time you restart after pulling this in; every event
  registration is individually try/caught so one bad one degrades gracefully instead of
  taking the engine down, but "degrades gracefully" still means "go check."
