// ============================================================
// MOOGUL TELEMETRY — the parent dashboard's eyes and ears
//
// Scripts can't write files directly, so every event becomes one
// line of JSON on stdout, prefixed [MOOGUL-T]. deck.js tails the
// server process, picks those lines out, and turns them into the
// human-readable World Feed + per-player pages + the "who broke
// blocks near X" forensics query. Nothing here ever leaves the PC.
//
// SAFETY: this file wires up to ~9 separate engine events, several
// of which have moved/renamed across Bedrock script API versions
// in the past. A subscription that throws at REGISTRATION time
// (e.g. an event that doesn't exist in this exact API version)
// must not take the other eight down with it — CLAUDE.md is
// explicit that a broken script here kills the whole engine. So:
// every single subscribe() call below is individually try/caught,
// and initTelemetry() itself never throws outward. Verify each of
// these fires as expected in-game before trusting the feed fully;
// if one silently no-ops, its console.warn will say which.
// ============================================================

import { onTelemetry } from "./karma.js";

const CONTAINER_TYPES = [
    "chest", "trapped_chest", "barrel", "ender_chest",
    "furnace", "blast_furnace", "smoker",
    "hopper", "dispenser", "dropper",
];

function isContainer(typeId) {
    const short = (typeId || "").replace("minecraft:", "");
    if (short.includes("shulker_box")) return true;
    return CONTAINER_TYPES.includes(short);
}

function locOf(location, dimensionId) {
    if (!location) return null;
    return {
        x: Math.round(location.x),
        y: Math.round(location.y),
        z: Math.round(location.z),
        dim: (dimensionId || "").replace("minecraft:", "") || undefined,
    };
}

// wraps one event registration so a missing/renamed event can't
// take the rest of telemetry down with it.
function safeSubscribe(label, subscribeFn) {
    try {
        subscribeFn();
    } catch (e) {
        console.warn(`[moogul] telemetry: '${label}' did not register (${e}) — that signal will be missing from the feed, everything else still runs.`);
    }
}

export function initTelemetry(world, system) {
    // closes over `world` so karma.js's in-process reaction (villager_hurt,
    // pvp -> karma adjust) doesn't need a stdout round-trip.
    function emit(kind, who, what, where) {
        const entry = { t: Date.now(), kind, who, what, where };
        try { console.log("[MOOGUL-T] " + JSON.stringify(entry)); } catch (e) { /* never let a logging failure ripple outward */ }
        try { onTelemetry(world, entry); } catch (e) { }
    }
    // -- chat: log, never block --------------------------------
    safeSubscribe("chat", () => {
        world.beforeEvents.chatSend.subscribe((ev) => {
            try {
                emit("chat", ev.sender?.name, ev.message);
            } catch (e) { }
            // no ev.cancel — chat always proceeds normally
        });
    });

    // -- join / leave --------------------------------------------
    safeSubscribe("join", () => {
        world.afterEvents.playerSpawn.subscribe((ev) => {
            try {
                if (!ev.initialSpawn) return; // main.js's welcome handler covers respawns
                const p = ev.player;
                emit(ev.player.hasTag("moogul:known") ? "join" : "join_new", p.name, null, locOf(p.location, p.dimension?.id));
            } catch (e) { }
        });
    });
    safeSubscribe("leave", () => {
        world.afterEvents.playerLeave.subscribe((ev) => {
            try {
                emit("leave", ev.playerName);
            } catch (e) { }
        });
    });

    // -- death, with cause ------------------------------------------
    safeSubscribe("death", () => {
        world.afterEvents.entityDie.subscribe((ev) => {
            try {
                const dead = ev.deadEntity;
                if (!dead || dead.typeId !== "minecraft:player") return;
                const src = ev.damageSource || {};
                emit("death", dead.name, {
                    cause: src.cause || "unknown",
                    by: src.damagingEntity?.typeId?.replace("minecraft:", ""),
                }, locOf(dead.location, dead.dimension?.id));
            } catch (e) { }
        });
    });

    // -- blocks: build/break, the forensics backbone -----------------
    safeSubscribe("blockBreak", () => {
        world.afterEvents.playerBreakBlock.subscribe((ev) => {
            try {
                const block = ev.brokenBlockPermutation || ev.block;
                emit("block_break", ev.player?.name, block?.type?.id, locOf(ev.block?.location, ev.dimension?.id));
            } catch (e) { }
        });
    });
    safeSubscribe("blockPlace", () => {
        world.afterEvents.playerPlaceBlock.subscribe((ev) => {
            try {
                emit("block_place", ev.player?.name, ev.block?.typeId, locOf(ev.block?.location, ev.dimension?.id));
            } catch (e) { }
        });
    });

    // -- container open (approximated: any block-interact on a
    // container-shaped block; there is no single dedicated
    // "container opened" event in the stable API) ------------------
    safeSubscribe("containerOpen", () => {
        world.afterEvents.playerInteractWithBlock.subscribe((ev) => {
            try {
                const typeId = ev.block?.typeId;
                if (!isContainer(typeId)) return;
                emit("container_open", ev.player?.name, typeId, locOf(ev.block?.location, ev.dimension?.id));
            } catch (e) { }
        });
    });

    // -- entity hurt: villagers + player-vs-player only (not every mob scuffle) --
    safeSubscribe("entityHurt", () => {
        world.afterEvents.entityHurt.subscribe((ev) => {
            try {
                const hurt = ev.hurtEntity;
                const src = ev.damageSource || {};
                const attacker = src.damagingEntity;
                if (hurt?.typeId === "minecraft:villager") {
                    emit("villager_hurt", attacker?.typeId === "minecraft:player" ? attacker.name : (attacker?.typeId?.replace("minecraft:", "") || "something"), null, locOf(hurt.location, hurt.dimension?.id));
                } else if (hurt?.typeId === "minecraft:player" && attacker?.typeId === "minecraft:player") {
                    emit("pvp", attacker.name, hurt.name, locOf(hurt.location, hurt.dimension?.id));
                }
            } catch (e) { }
        });
    });

    // -- lightweight position heartbeat: gives the deck a "last known
    // location" for each online player without a dedicated movement
    // API. Every 600 ticks (~30s); deck.js keeps this out of the
    // visible feed, it's bookkeeping only. -------------------------
    safeSubscribe("positionHeartbeat", () => {
        system.runInterval(() => {
            try {
                for (const p of world.getAllPlayers()) {
                    try {
                        emit("position", p.name, null, locOf(p.location, p.dimension?.id));
                    } catch (e) { }
                }
            } catch (e) { }
        }, 600);
    });

    console.log("[moogul] Telemetry online.");
}
