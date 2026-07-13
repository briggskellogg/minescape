// ============================================================
// MOOGUL EVENTS — the adventure library
// Each entry is a world event you can fire live from the
// Command Deck or with /scriptevent moogul:<name>
//
// Handler signature: ({ world, system, message, source }) => {}
//   message = optional free-text payload after the event id
// ============================================================

import { carveCrater } from "./genesis.js";
import { setWeather, setTimelock } from "./ambience.js";
import { setMark, listMarks } from "./marks.js";
import { jailPlayer, releasePlayer, finePlayer, grantPlayer, adjustKarmaEvent, setKarmaWeight } from "./karma.js";
import { placeDungeon } from "./dungeons.js";

function allPlayers(world) {
    return world.getAllPlayers();
}

function announce(world, title, subtitle, sound) {
    for (const p of allPlayers(world)) {
        try {
            p.onScreenDisplay.setTitle(title, {
                fadeInDuration: 10, stayDuration: 70, fadeOutDuration: 20,
                subtitleText: subtitle ?? "",
            });
            if (sound) p.playSound(sound);
        } catch (e) { }
    }
}

export const EVENTS = {
    // -- one-time world sculpting ------------------------------
    // stand at the spot, then: /scriptevent moogul:genesis
    genesis: carveCrater,

    // -- ambience engine ---------------------------------------
    // /scriptevent moogul:weather clear|rain|thunder|natural
    // /scriptevent moogul:timelock day|night|natural
    weather: setWeather,
    timelock: setTimelock,

    // -- sanity check ----------------------------------------
    ping: ({ world }) => {
        world.sendMessage("§a[moogul] pong — the engine hears you.");
    },

    // -- narration: send story text to everyone ---------------
    // usage: /scriptevent moogul:narrate The wind shifts. Something stirs.
    narrate: ({ world, message }) => {
        if (!message) return;
        world.sendMessage(`§o§7${message}§r`);
        for (const p of allPlayers(world)) { try { p.playSound("note.bass"); } catch (e) { } }
    },

    // -- dramatic titled announcement --------------------------
    // usage: /scriptevent moogul:announce Title|Subtitle
    announce: ({ world, message }) => {
        const [title, subtitle] = (message ?? "Minescape").split("|");
        announce(world, `§d${title}`, subtitle ? `§7${subtitle}` : "", "random.orb");
    },

    // -- celebration: fireworks over every player --------------
    celebrate: ({ world, system }) => {
        announce(world, "§6✦ Celebration! ✦", "", "random.levelup");
        let burst = 0;
        const id = system.runInterval(() => {
            for (const p of allPlayers(world)) {
                try {
                    const loc = p.location;
                    p.dimension.spawnEntity("minecraft:fireworks_rocket", {
                        x: loc.x + (Math.random() * 8 - 4), y: loc.y, z: loc.z + (Math.random() * 8 - 4),
                    });
                } catch (e) { }
            }
            if (++burst >= 6) system.clearRun(id);
        }, 15);
    },

    // -- storm-in / calm: instant mood shift -------------------
    storm: ({ world }) => {
        try { world.getDimension("overworld").runCommand("weather thunder 6000"); } catch (e) { }
        announce(world, "§9The sky darkens...", "§7a storm rolls into the valley", "ambient.weather.thunder");
    },
    calm: ({ world }) => {
        const ow = world.getDimension("overworld");
        try { ow.runCommand("weather clear 24000"); } catch (e) { }
        try { ow.runCommand("time set day"); } catch (e) { }
        announce(world, "§eThe skies clear", "", "random.orb");
    },

    // -- nightfall: cue for spooky story beats ------------------
    nightfall: ({ world }) => {
        try { world.getDimension("overworld").runCommand("time set night"); } catch (e) { }
        announce(world, "§5Night falls on Minescape", "§7stay near the light", "mob.enderdragon.growl");
    },

    // -- gift drop: a small reward near each player -------------
    gift: ({ world }) => {
        for (const p of allPlayers(world)) {
            try {
                const loc = p.location;
                p.dimension.runCommand(
                    `loot spawn ${Math.round(loc.x)} ${Math.round(loc.y + 1)} ${Math.round(loc.z)} loot "chests/simple_dungeon"`
                );
                p.playSound("random.chestopen");
                p.sendMessage("§d✦ A gift appears at your feet.");
            } catch (e) { }
        }
    },

    // -- rally: bring everyone to the event host ----------------
    // fire from chat as yourself: /scriptevent moogul:rally
    rally: ({ world, source }) => {
        if (!source) { world.sendMessage("§c[moogul] rally needs a player source (fire it from chat)."); return; }
        const loc = source.location;
        for (const p of allPlayers(world)) {
            try {
                p.teleport(loc, { dimension: source.dimension });
                p.playSound("mob.endermen.portal");
            } catch (e) { }
        }
        announce(world, "§bGather round", "§7the storyteller calls", "");
    },

    // -- marks: named location bookmarks ------------------------
    // /scriptevent moogul:mark <name>   (fire from chat, standing at the spot)
    // /scriptevent moogul:marks
    mark: setMark,
    marks: listMarks,

    // -- judge: jail / release / fine / grant --------------------
    // Deck-triggered (no player source) — targets a player by name.
    // /scriptevent moogul:jail <player> [minutes]
    // /scriptevent moogul:release <player>
    // /scriptevent moogul:fine <player> <amount> [reason...]
    // /scriptevent moogul:grant <player> <amount> [reason...]
    jail: jailPlayer,
    release: releasePlayer,
    fine: finePlayer,
    grant: grantPlayer,
    // manual karma adjust (covers trades/gifts/theft/grief until dedicated
    // detection exists) + live weight tuning without a redeploy
    // /scriptevent moogul:karma <player> <+/-amount> [reason...]
    // /scriptevent moogul:karmaweight <key> <value>
    karma: adjustKarmaEvent,
    karmaweight: setKarmaWeight,

    // -- dungeons: place an authored dungeon at a mark ------------
    // /scriptevent moogul:dungeon place <name> [mark]
    // /scriptevent moogul:dungeon reset <name>
    dungeon: placeDungeon,
};
