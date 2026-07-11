// ============================================================
// GENESIS — one-time world sculpting: the Crater & the Sword
//
// Stand where the crater should be centered, then in chat:
//   /scriptevent moogul:genesis
// Carves a huge stepped crater beneath you and plants the
// sword in the stone at its floor. Fire it once. (If you hate
// the spot, /scriptevent moogul:genesis again elsewhere — the
// old crater stays, the sword moves.)
// ============================================================

const RADIUS = 34;      // crater mouth radius (blocks)
const DEPTH = 42;       // how deep it bites
const FLOOR_R = 6;      // flat floor at the bottom

export function carveCrater({ world, system, source }) {
    if (!source) {
        world.sendMessage("§c[moogul] Fire genesis from chat, standing at the crater center.");
        return;
    }
    const dim = source.dimension;
    const c = {
        x: Math.round(source.location.x),
        y: Math.round(source.location.y),
        z: Math.round(source.location.z),
    };
    // lift the caster clear of the excavation
    try { source.teleport({ x: c.x, y: c.y + 12, z: c.z + RADIUS + 8 }, { dimension: dim }); } catch (e) { }
    world.sendMessage("§5[moogul] Genesis begins. The earth remembers what falls from the sky...");

    system.runJob(carveJob(world, system, dim, c));
}

function* carveJob(world, system, dim, c) {
    // Bowl profile: radius shrinks with depth (smooth-ish curve)
    for (let dy = 0; dy <= DEPTH; dy++) {
        const t = dy / DEPTH;
        const r = Math.max(FLOOR_R, Math.round(RADIUS * Math.sqrt(1 - t * t)));
        const y = c.y - dy;
        // carve this layer in strips (each fill stays under command limits)
        for (let dx = -r; dx <= r; dx++) {
            const halfSpan = Math.floor(Math.sqrt(r * r - dx * dx));
            try {
                dim.runCommand(
                    `fill ${c.x + dx} ${y} ${c.z - halfSpan} ${c.x + dx} ${y} ${c.z + halfSpan} air`
                );
            } catch (e) { /* unloaded chunk or empty strip — fine */ }
            yield; // spread work across ticks: no lag spike
        }
    }

    // Scorched rim + glow at the floor
    const fy = c.y - DEPTH;
    try {
        dim.runCommand(`fill ${c.x - FLOOR_R} ${fy - 1} ${c.z - FLOOR_R} ${c.x + FLOOR_R} ${fy - 1} ${c.z + FLOOR_R} blackstone`);
        dim.runCommand(`fill ${c.x - 2} ${fy - 1} ${c.z - 2} ${c.x + 2} ${fy - 1} ${c.z + 2} gilded_blackstone`);
        dim.runCommand(`setblock ${c.x + 4} ${fy} ${c.z + 4} lantern`);
        dim.runCommand(`setblock ${c.x - 4} ${fy} ${c.z - 4} lantern`);
    } catch (e) { }
    yield;

    // The stone, and the sword that will not move
    try {
        dim.runCommand(`fill ${c.x - 1} ${fy} ${c.z - 1} ${c.x + 1} ${fy} ${c.z + 1} stone`);
        // clear any previous sword so genesis is re-runnable
        dim.runCommand(`event entity @e[type=moogul:sword_in_stone] moogul:despawn`);
    } catch (e) { }
    try {
        const sword = dim.spawnEntity("moogul:sword_in_stone", { x: c.x + 0.5, y: fy + 1, z: c.z + 0.5 });
        sword.nameTag = "";
        world.setDynamicProperty("moogul:swordPos", JSON.stringify({ x: c.x, y: fy + 1, z: c.z }));
    } catch (e) {
        world.sendMessage("§c[moogul] Sword entity failed to spawn: " + e);
    }
    world.sendMessage("§5[moogul] It is done. Something waits at the bottom.");
}

// ---- The sword resists --------------------------------------
const WHISPERS = [
    "§o§7The sword does not move. It is waiting for something.",
    "§o§7Your hands slip from the hilt. Not yet.",
    "§o§7A voice, far away: 'Not by strength alone.'",
    "§o§7The stone is colder than it should be.",
];

export function armSwordWhispers(world) {
    world.afterEvents.playerInteractWithEntity.subscribe((ev) => {
        try {
            if (ev.target?.typeId !== "moogul:sword_in_stone") return;
            const p = ev.player;
            p.sendMessage(WHISPERS[Math.floor(Math.random() * WHISPERS.length)]);
            p.playSound("mob.warden.heartbeat");
        } catch (e) { }
    });
}
