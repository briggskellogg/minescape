// ============================================================
// MOOGUL CORE — entry point
// The engine room of Minescape. Everything routes through here.
// ============================================================
import { world, system } from "@minecraft/server";
import { EVENTS } from "./events.js";
import { armSwordWhispers } from "./genesis.js";
import { initTelemetry } from "./telemetry.js";
import { initKarma } from "./karma.js";
import { initDungeons } from "./dungeons.js";

// The sword in the stone resists all who try
armSwordWhispers(world);

// Each of these is isolated in its own try/catch so a problem in one
// system can never take the event router or the others down with it —
// the whole reason every module below also try/catches its own event
// registrations individually (see each file's own SAFETY notes).
try {
    initTelemetry(world, system);
} catch (e) {
    console.error("[moogul] telemetry failed to initialize: " + e);
}
try {
    initKarma(world, system);
} catch (e) {
    console.error("[moogul] karma failed to initialize: " + e);
}
try {
    initDungeons(world, system);
} catch (e) {
    console.error("[moogul] dungeons failed to initialize: " + e);
}

// ---- Welcome new + returning players -----------------------
world.afterEvents.playerSpawn.subscribe((ev) => {
    if (!ev.initialSpawn) return;
    const p = ev.player;
    try {
        const isNew = !p.hasTag("moogul:known");
        if (isNew) p.addTag("moogul:known");
        system.runTimeout(() => {
            try {
                p.onScreenDisplay.setTitle(isNew ? "§dWelcome to Minescape" : "§dMinescape", {
                    fadeInDuration: 10,
                    stayDuration: 60,
                    fadeOutDuration: 20,
                    subtitleText: isNew ? "§7A world built for you" : "§7Welcome back",
                });
                p.playSound("random.levelup");
            } catch (e) { /* player may have left */ }
        }, 40);
    } catch (e) {
        console.warn("[moogul] welcome failed: " + e);
    }
});

// ---- Event router -------------------------------------------
// The Command Deck (and you, from chat) trigger world events with:
//   /scriptevent moogul:<eventName> [optional payload]
// Add new events in events.js — this router never needs to change.
system.afterEvents.scriptEventReceive.subscribe((ev) => {
    const { id, message, sourceEntity } = ev;
    if (!id.startsWith("moogul:")) return;
    const name = id.slice("moogul:".length);
    const handler = EVENTS[name];
    if (!handler) {
        console.warn(`[moogul] unknown event '${name}'. Known: ${Object.keys(EVENTS).join(", ")}`);
        return;
    }
    try {
        handler({ world, system, message, source: sourceEntity });
        console.log(`[moogul] event '${name}' fired`);
    } catch (e) {
        console.error(`[moogul] event '${name}' failed: ${e}`);
    }
});

console.log("[moogul] Moogul Core online. Event router armed.");
