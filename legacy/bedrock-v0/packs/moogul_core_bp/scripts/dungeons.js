// ============================================================
// DUNGEONS — placed, protected structures (NOT full dimensions —
// see DESIGN.md's "dungeons vs. dimensions" note). Composed from the
// modules in dungeon-modules.js. A locked dungeon needs its key item
// presented at the door; an open one never gates entry.
//
//   /scriptevent moogul:dungeon place <name> [mark]
//   /scriptevent moogul:dungeon reset <name>
//
// SAFETY: like telemetry.js and karma.js, every event registration
// here is individually try/caught — a missing/renamed API can only
// cost this one system its live enforcement, never the whole engine.
// ============================================================
import { getMark } from "./marks.js";
import { corridorModule, bossArenaModule, lootVaultModule } from "./dungeon-modules.js";

const REGISTRY_KEY = "moogul:dungeonRegistry";
const KEY_ITEM = "moogul:alligator_key";
const BOSS_TYPE = "moogul:alligator_knight";

function getRegistry(world) {
    try {
        const raw = world.getDynamicProperty(REGISTRY_KEY);
        return typeof raw === "string" && raw ? JSON.parse(raw) : {};
    } catch (e) { return {}; }
}
function saveRegistry(world, reg) {
    world.setDynamicProperty(REGISTRY_KEY, JSON.stringify(reg));
}

// deck.js can't read this dynamic property directly (same platform
// constraint as karma's economy blob) — mirror status changes over the
// existing telemetry pipeline so the Dungeons tab can show them.
function logStatus(name, status, where) {
    try { console.log("[MOOGUL-T] " + JSON.stringify({ t: Date.now(), kind: "dungeon", who: null, what: { name, status, label: DEFINITIONS[name]?.label }, where })); } catch (e) { }
}

// -- the one dungeon authored so far. Adding another is: write a new
// entry here composing the same modules in a different arrangement —
// no engine changes needed, which is the point of the module system. --
const DEFINITIONS = {
    "alligator-knight-lair": {
        label: "The Alligator Knight's Lair",
        locked: true,
        bossType: BOSS_TYPE,
        build(origin) {
            // entrance faces north from the mark; a locked door blocks the
            // corridor mouth until the key item is presented.
            const corridor = corridorModule(
                { x: origin.x, y: origin.y, z: origin.z - 2 },
                "north",
                14,
                { trapStart: 6, trapLength: 2 }
            );
            const arena = bossArenaModule(
                { x: corridor.end.x, y: corridor.end.y, z: corridor.end.z - 10 },
                { radius: 9, height: 8 }
            );
            const vault = lootVaultModule(
                { x: arena.center.x + 6, y: arena.center.y, z: arena.center.z },
                { lootTable: "chests/simple_dungeon" }
            );
            const lockPos = { x: origin.x, y: origin.y, z: origin.z - 1 };
            const bossSpawn = { x: arena.center.x, y: arena.center.y, z: arena.center.z };

            function* run(dimension) {
                // door frame + the lock block itself (barrier — swapped to an
                // open doorway once the key is presented)
                try {
                    dimension.runCommand(`fill ${origin.x - 2} ${origin.y} ${origin.z - 1} ${origin.x + 2} ${origin.y + 4} ${origin.z - 1} blackstone hollow`);
                    dimension.runCommand(`setblock ${lockPos.x} ${lockPos.y} ${lockPos.z} barrier`);
                    dimension.runCommand(`setblock ${lockPos.x} ${lockPos.y + 1} ${lockPos.z} barrier`);
                } catch (e) { }
                yield;
                yield* corridor.run(dimension);
                yield* arena.run(dimension);
                yield* vault.run(dimension);
                try {
                    dimension.spawnEntity(BOSS_TYPE, bossSpawn);
                } catch (e) { console.warn("[moogul] dungeons: Alligator Knight failed to spawn (" + e + ") — the room is built but empty."); }
            }

            return { run, trapTiles: corridor.trapTiles, lockPos, bossSpawn };
        },
    },
};

export function placeDungeon({ world, system, message }) {
    const parts = (message ?? "").trim().split(/\s+/);
    const sub = parts[0];
    const name = parts[1];
    const def = DEFINITIONS[name];
    if (!def) {
        world.sendMessage(`§c[moogul] unknown dungeon '${name}'. Known: ${Object.keys(DEFINITIONS).join(", ") || "(none yet)"}`);
        return;
    }
    if (sub !== "place" && sub !== "reset") {
        world.sendMessage("§c[moogul] usage: /scriptevent moogul:dungeon place <name> [mark]  |  /scriptevent moogul:dungeon reset <name>");
        return;
    }
    const markName = parts[2];
    const mark = markName ? getMark(world, markName) : null;
    if (markName && !mark) { world.sendMessage(`§c[moogul] no mark named '${markName}' — /scriptevent moogul:mark ${markName} first, standing where the entrance should be.`); return; }
    if (!mark) { world.sendMessage("§c[moogul] usage needs a mark: /scriptevent moogul:dungeon place " + name + " <markName>"); return; }

    const dimension = world.getDimension(mark.dim || "overworld");
    const built = def.build(mark);
    const reg = getRegistry(world);
    reg[name] = { defName: name, origin: mark, dim: mark.dim || "overworld", status: def.locked ? "sealed" : "open", lockPos: built.lockPos, placedAt: Date.now() };
    saveRegistry(world, reg);
    registerTrapTiles(name, dimension, built.trapTiles);
    logStatus(name, reg[name].status, mark);

    world.sendMessage(`§5[moogul] placing '${def.label}' at mark '${markName}'... this spreads across several seconds, don't stand in the footprint.`);
    system.runJob(built.run(dimension));
}

// -- ghost-block traps: a live watcher, not a one-time placement.
// Polls online players' feet position against every registered
// dungeon's trap tiles; the moment someone's standing on one, the
// floor block vanishes under them (then quietly restores a few
// seconds later so the trap resets for next time). ------------------
const activeTraps = new Map(); // dungeonName -> { dimension, tiles: [{x,y,z}] }
const triggeredAt = new Map(); // "x,y,z" -> timestamp, so we don't re-trigger while it's still gone

function registerTrapTiles(name, dimension, tiles) {
    activeTraps.set(name, { dimension, tiles: tiles || [] });
}

function watchTraps(world, system) {
    system.runInterval(() => {
        try {
            const now = Date.now();
            for (const [, { dimension, tiles }] of activeTraps) {
                if (!tiles.length) continue;
                for (const p of world.getAllPlayers()) {
                    if (p.dimension?.id !== dimension.id) continue;
                    const fx = Math.floor(p.location.x), fy = Math.floor(p.location.y) - 1, fz = Math.floor(p.location.z);
                    const hit = tiles.find((t) => t.x === fx && t.y === fy && t.z === fz);
                    if (!hit) continue;
                    const key = `${hit.x},${hit.y},${hit.z}`;
                    if (triggeredAt.has(key)) continue; // already sprung, waiting to reset
                    triggeredAt.set(key, now);
                    try { dimension.runCommand(`setblock ${hit.x} ${hit.y} ${hit.z} air`); } catch (e) { }
                    try { p.playSound("mob.zombie.wood_break"); } catch (e) { }
                }
            }
            // reset sprung tiles after 4s
            for (const [key, t] of triggeredAt) {
                if (now - t < 4000) continue;
                const [x, y, z] = key.split(",").map(Number);
                for (const { dimension } of activeTraps.values()) {
                    try { dimension.runCommand(`setblock ${x} ${y} ${z} polished_blackstone`); } catch (e) { }
                }
                triggeredAt.delete(key);
            }
        } catch (e) { }
    }, 4);
}

// -- locked door: presenting the key item at the lock position opens
// it (barrier -> air) for everyone, permanently, for that instance. --
function watchLocks(world) {
    world.afterEvents.playerInteractWithBlock.subscribe((ev) => {
        try {
            const reg = getRegistry(world);
            for (const name of Object.keys(reg)) {
                const d = reg[name];
                if (d.status !== "sealed" || !d.lockPos) continue;
                const b = ev.block;
                if (!b || Math.floor(b.location.x) !== d.lockPos.x || Math.floor(b.location.y) !== d.lockPos.y || Math.floor(b.location.z) !== d.lockPos.z) continue;
                const held = ev.itemStack?.typeId;
                if (held !== KEY_ITEM) { ev.player?.sendMessage("§7The door is locked. It needs a key."); return; }
                const dim = world.getDimension(d.dim || "overworld");
                try {
                    dim.runCommand(`fill ${d.lockPos.x} ${d.lockPos.y} ${d.lockPos.z} ${d.lockPos.x} ${d.lockPos.y + 1} ${d.lockPos.z} air`);
                } catch (e) { }
                d.status = "open";
                saveRegistry(world, reg);
                logStatus(name, "open", d.origin);
                world.sendMessage(`§5[moogul] '${DEFINITIONS[name]?.label || name}' is unsealed.`);
            }
        } catch (e) { }
    });
}

// -- clearing: the boss dying marks THAT dungeon cleared. Matches by
// bossType, not just "any dungeon" — important once a second dungeon
// definition with a different boss exists. ---------------------------
function watchBossDeaths(world) {
    world.afterEvents.entityDie.subscribe((ev) => {
        try {
            const deadType = ev.deadEntity?.typeId;
            if (!deadType) return;
            const reg = getRegistry(world);
            let changed = false;
            for (const name of Object.keys(reg)) {
                const def = DEFINITIONS[reg[name].defName];
                if (!def || def.bossType !== deadType || reg[name].status === "cleared") continue;
                reg[name].status = "cleared";
                changed = true;
                logStatus(name, "cleared", reg[name].origin);
                world.sendMessage(`§6[moogul] The boss of '${def.label}' falls. Cleared.`);
            }
            if (changed) saveRegistry(world, reg);
        } catch (e) { }
    });
}

export function initDungeons(world, system) {
    try { watchTraps(world, system); } catch (e) { console.warn("[moogul] dungeons: trap watcher failed to start (" + e + ")"); }
    try { watchLocks(world); } catch (e) { console.warn("[moogul] dungeons: lock watcher did not register (" + e + ") — doors won't open for key-holders."); }
    try { watchBossDeaths(world); } catch (e) { console.warn("[moogul] dungeons: boss-clear watcher did not register (" + e + ")"); }

    // tell the deck what's authored + what's already placed, since it
    // can't read either the DEFINITIONS registry or the dynamic-property
    // instance registry directly.
    try {
        const catalog = Object.entries(DEFINITIONS).map(([name, def]) => ({ name, label: def.label, locked: !!def.locked }));
        console.log("[MOOGUL-T] " + JSON.stringify({ t: Date.now(), kind: "dungeon_catalog", who: null, what: catalog, where: null }));
        const reg = getRegistry(world);
        for (const name of Object.keys(reg)) logStatus(name, reg[name].status, reg[name].origin);
    } catch (e) { }

    console.log("[moogul] Dungeons online. Known: " + Object.keys(DEFINITIONS).join(", "));
}
