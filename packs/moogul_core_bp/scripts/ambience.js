// ============================================================
// AMBIENCE ENGINE — weather domination, time locks
// Weather locks persist (dynamic property) and re-assert
// themselves every 30s, so the world stays how you set it —
// through rain ticks, sleep skips, and restarts — until you
// hand the sky back to nature.
//   /scriptevent moogul:weather clear|rain|thunder|natural
//   /scriptevent moogul:timelock day|night|natural
// ============================================================
import { world, system } from "@minecraft/server";

const OW = () => world.getDimension("overworld");

// Re-assert a locked weather pattern every 600 ticks (~30s)
system.runInterval(() => {
    try {
        const lock = world.getDynamicProperty("moogul:weatherLock");
        if (typeof lock === "string" && lock.length > 0) {
            OW().runCommand(`weather ${lock} 1000000`);
        }
    } catch (e) { }
}, 600);

export function setWeather({ world, message }) {
    const mode = (message ?? "").trim().toLowerCase();
    if (mode === "clear" || mode === "rain" || mode === "thunder") {
        world.setDynamicProperty("moogul:weatherLock", mode);
        try { OW().runCommand(`weather ${mode} 1000000`); } catch (e) { }
        world.sendMessage(`§7[ambience] the sky obeys: §f${mode}§7 (locked)`);
    } else if (mode === "natural") {
        world.setDynamicProperty("moogul:weatherLock", "");
        try { OW().runCommand("weather clear 600"); } catch (e) { }
        world.sendMessage("§7[ambience] the sky returns to its natural rhythm");
    } else {
        world.sendMessage("§c[ambience] weather wants: clear | rain | thunder | natural");
    }
}

export function setTimelock({ world, message }) {
    const mode = (message ?? "").trim().toLowerCase();
    try {
        if (mode === "day") {
            OW().runCommand("gamerule dodaylightcycle false");
            OW().runCommand("time set noon");
            world.sendMessage("§7[ambience] the sun stands still");
        } else if (mode === "night") {
            OW().runCommand("gamerule dodaylightcycle false");
            OW().runCommand("time set midnight");
            world.sendMessage("§7[ambience] endless night (locked)");
        } else if (mode === "natural") {
            OW().runCommand("gamerule dodaylightcycle true");
            world.sendMessage("§7[ambience] time flows again");
        } else {
            world.sendMessage("§c[ambience] timelock wants: day | night | natural");
        }
    } catch (e) { }
}
