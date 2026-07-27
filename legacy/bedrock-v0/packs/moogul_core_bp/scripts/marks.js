// ============================================================
// MARKS — named location bookmarks
// Every targetable feature (dungeon placement, epic events, later
// quests) points at the world through a mark instead of raw
// coordinates. Stored as one JSON blob in a world dynamic property
// (same pattern genesis.js uses for moogul:swordPos).
//   /scriptevent moogul:mark <name>      (fire from chat, standing at the spot)
//   /scriptevent moogul:marks            (list them)
// ============================================================
const KEY = "moogul:marks";

export function getMarks(world) {
    try {
        const raw = world.getDynamicProperty(KEY);
        return typeof raw === "string" && raw ? JSON.parse(raw) : {};
    } catch (e) {
        return {};
    }
}

function saveMarks(world, marks) {
    world.setDynamicProperty(KEY, JSON.stringify(marks));
}

export function getMark(world, name) {
    const marks = getMarks(world);
    return marks[name] || null;
}

export function setMark({ world, message, source }) {
    const name = (message ?? "").trim();
    if (!source) { world.sendMessage("§c[moogul] mark needs a player source (fire it from chat, standing where you want it)."); return; }
    if (!name) { world.sendMessage("§c[moogul] usage: /scriptevent moogul:mark <name>"); return; }
    const marks = getMarks(world);
    marks[name] = {
        x: Math.round(source.location.x),
        y: Math.round(source.location.y),
        z: Math.round(source.location.z),
        dim: (source.dimension?.id || "minecraft:overworld").replace("minecraft:", ""),
    };
    saveMarks(world, marks);
    world.sendMessage(`§b[moogul] mark set: §f${name}§b at ${marks[name].x}, ${marks[name].y}, ${marks[name].z}`);
}

export function listMarks({ world }) {
    const marks = getMarks(world);
    const names = Object.keys(marks);
    if (!names.length) { world.sendMessage("§7[moogul] no marks yet — /scriptevent moogul:mark <name> to set one."); return; }
    world.sendMessage("§b[moogul] marks: §f" + names.map((n) => `${n} (${marks[n].x},${marks[n].y},${marks[n].z})`).join("  §7|§f  "));
}
