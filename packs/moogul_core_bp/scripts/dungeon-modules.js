// ============================================================
// DUNGEON MODULES — the modular building system
//
// Dungeons are composed from small, reusable generator functions —
// the same generator-function pattern genesis.js already proved out
// for the crater (yield between fill commands so a big dungeon
// spreads across many ticks instead of freezing the server).
//
// Each module takes an origin + a facing direction and returns
// { run: generatorFunction, footprint: {length, width, height},
//   trapTiles: [{x,y,z}, ...] } — dungeons.js runs `run` via
// system.runJob and hands trapTiles to the ghost-block watcher.
//
// This is the pattern to imitate for future hand-authored dungeons
// too — see dungeons/README.md — whether you're composing these same
// module functions in a new arrangement, or (for a hand-built
// dungeon) exporting real .mcstructure files room-by-room.
// ============================================================

// direction -> unit step vector along the corridor's long axis
const DIR = {
    north: { dx: 0, dz: -1 },
    south: { dx: 0, dz: 1 },
    east: { dx: 1, dz: 0 },
    west: { dx: -1, dz: 0 },
};

function perp(dir) {
    // perpendicular unit vector, for corridor width
    return dir.dx !== 0 ? { dx: 0, dz: 1 } : { dx: 1, dz: 0 };
}

// -- corridor: a walkable hallway, optionally with a ghost-block trap
// section partway along it. Returns the trap tile coordinates so the
// caller can register them with the live watcher. --------------------
export function corridorModule(origin, direction, length, opts = {}) {
    const dir = DIR[direction] || DIR.north;
    const side = perp(dir);
    const width = opts.width ?? 3;
    const height = opts.height ?? 4;
    const trapStart = opts.trapStart ?? Math.floor(length / 2) - 1;
    const trapLength = opts.trapLength ?? 2;
    const floorBlock = opts.floorBlock ?? "polished_blackstone";
    const wallBlock = opts.wallBlock ?? "blackstone";

    const trapTiles = [];
    for (let i = 0; i < length; i++) {
        if (i >= trapStart && i < trapStart + trapLength) {
            const cx = origin.x + dir.dx * i;
            const cz = origin.z + dir.dz * i;
            for (let w = -Math.floor(width / 2); w <= Math.floor(width / 2); w++) {
                trapTiles.push({ x: cx + side.dx * w, y: origin.y - 1, z: cz + side.dz * w });
            }
        }
    }

    function* run(dimension) {
        const half = Math.floor(width / 2);
        for (let i = 0; i < length; i++) {
            const cx = origin.x + dir.dx * i;
            const cz = origin.z + dir.dz * i;
            const isTrap = i >= trapStart && i < trapStart + trapLength;
            try {
                // floor — real block even under a trap tile; the ghost-block
                // watcher swaps it to air live rather than never placing it,
                // so it LOOKS identical to the rest of the corridor until triggered
                dimension.runCommand(
                    `fill ${cx - half} ${origin.y - 1} ${cz - half} ${cx + half} ${origin.y - 1} ${cz + half} ${isTrap ? floorBlock : floorBlock}`
                );
                // walls + ceiling (hollow box for this slice)
                dimension.runCommand(
                    `fill ${cx - half} ${origin.y} ${cz - half} ${cx + half} ${origin.y + height - 1} ${cz + half} ${wallBlock} hollow`
                );
                // interior air
                dimension.runCommand(
                    `fill ${cx - half + 1} ${origin.y} ${cz - half + 1} ${cx + half - 1} ${origin.y + height - 2} ${cz + half - 1} air`
                );
                if (isTrap) {
                    dimension.runCommand(`fill ${cx - half} ${origin.y} ${cz - half} ${cx + half} ${origin.y} ${cz + half} air`);
                    dimension.runCommand(
                        `fill ${cx - half} ${origin.y - 1} ${cz - half} ${cx + half} ${origin.y - 1} ${cz + half} ${floorBlock}`
                    );
                }
            } catch (e) { /* unloaded chunk — fine, move on */ }
            yield;
        }
    }

    const endX = origin.x + dir.dx * length;
    const endZ = origin.z + dir.dz * length;
    return { run, trapTiles, end: { x: endX, y: origin.y, z: endZ }, endDirection: direction };
}

// -- boss arena: a round-ish room, floor + walls + open ceiling for
// sky/camera drama, sized for a boss fight. -----------------------
export function bossArenaModule(origin, opts = {}) {
    const radius = opts.radius ?? 9;
    const height = opts.height ?? 7;
    const floorBlock = opts.floorBlock ?? "polished_blackstone";
    const wallBlock = opts.wallBlock ?? "blackstone";
    const trimBlock = opts.trimBlock ?? "gilded_blackstone";

    function* run(dimension) {
        for (let dz = -radius; dz <= radius; dz++) {
            const half = Math.floor(Math.sqrt(Math.max(0, radius * radius - dz * dz)));
            try {
                dimension.runCommand(
                    `fill ${origin.x - half} ${origin.y - 1} ${origin.z + dz} ${origin.x + half} ${origin.y - 1} ${origin.z + dz} ${floorBlock}`
                );
                dimension.runCommand(
                    `fill ${origin.x - half} ${origin.y} ${origin.z + dz} ${origin.x + half} ${origin.y + height} ${origin.z + dz} air`
                );
            } catch (e) { }
            yield;
        }
        // ring wall (approximate circle via a fill+hollow bounding box, cheap and good enough)
        try {
            dimension.runCommand(
                `fill ${origin.x - radius} ${origin.y - 1} ${origin.z - radius} ${origin.x + radius} ${origin.y + height} ${origin.z + radius} ${wallBlock} hollow`
            );
            dimension.runCommand(
                `fill ${origin.x - radius} ${origin.y - 1} ${origin.z - radius} ${origin.x + radius} ${origin.y - 1} ${origin.z + radius} ${trimBlock} outline`
            );
        } catch (e) { }
        yield;
        // re-carve the interior air (the ring wall fill above touches it at the seams)
        for (let dz = -radius + 1; dz <= radius - 1; dz++) {
            const half = Math.floor(Math.sqrt(Math.max(0, (radius - 1) * (radius - 1) - dz * dz)));
            try {
                dimension.runCommand(
                    `fill ${origin.x - half} ${origin.y} ${origin.z + dz} ${origin.x + half} ${origin.y + height - 1} ${origin.z + dz} air`
                );
            } catch (e) { }
            yield;
        }
    }

    return { run, center: origin, radius };
}

// -- loot vault: a tiny room with a single loot-table chest, reused
// as the reward after a boss is cleared. ----------------------------
export function lootVaultModule(origin, opts = {}) {
    const lootTable = opts.lootTable ?? "chests/simple_dungeon";
    function* run(dimension) {
        try {
            dimension.runCommand(`fill ${origin.x - 1} ${origin.y - 1} ${origin.z - 1} ${origin.x + 1} ${origin.y + 2} ${origin.z + 1} blackstone hollow`);
            dimension.runCommand(`fill ${origin.x} ${origin.y} ${origin.z} ${origin.x} ${origin.y + 1} ${origin.z} air`);
            dimension.runCommand(`setblock ${origin.x} ${origin.y} ${origin.z} chest`);
            dimension.runCommand(`loot replace block ${origin.x} ${origin.y} ${origin.z} slot.container 0..26 loot "${lootTable}"`);
        } catch (e) { }
        yield;
    }
    return { run, center: origin };
}
