// ============================================================
// KARMA + ECONOMY + JUDGE
//
// Two related-but-distinct systems, per Briggs's call on scope:
//   1. Automatic karma (flavor-only, per DIRECTIONS-FOR-CLAUDE-CODE.md
//      §5) — nothing here hard-blocks play by itself.
//   2. A judge-only layer (jail, fines, grants) that YOU trigger from
//      the deck after reviewing forensics — never automatic. This is
//      the "I will decide who was right" role from the conversation
//      with Owen.
//
// SCOPE NOTE: automatic karma only reacts to what telemetry.js
// already reliably captures today (villager_hurt, pvp). Villager
// trades/gifts/theft-from-tracked-chests/grief-on-tagged-builds need
// dedicated detection work that doesn't exist yet — use
// /scriptevent moogul:karma <player> <+/-amount> <reason> by hand
// for those until that lands.
//
// PLATFORM NOTE: Bedrock scripts can't read config/karma.json at
// runtime (same reason telemetry writes to stdout, not a file) — the
// weights below are a hardcoded mirror of that file. Keep them in
// sync, or override live with:
//   /scriptevent moogul:karmaweight <key> <value>
//
// KARMA MARKER: one kid can't read yet, so karma needs to be seen,
// not read. There's no "player opened their inventory" event in the
// stable scripting API at all — the personal inventory screen is
// purely client-side, the dedicated server has no way to know it was
// opened (unlike a chest, which IS a world interaction). So instead
// of an overlay triggered by opening it, a real colored block sits in
// a fixed inventory slot at all times — it's just *there* every time
// they look, hotbar included. No text needed: color is the signal.
// ============================================================
import { ItemStack, ItemLockMode } from "@minecraft/server";

const ECONOMY_KEY = "moogul:economy";
const WEIGHT_OVERRIDE_KEY = "moogul:karmaWeights";

const DEFAULT_WEIGHTS = {
    villager_hurt: -8,
    excessive_pvp: -3,
};
const DEFAULT_JAIL = { default_minutes: 10, cage_radius: 3, cage_height: 12 };
const STARTING_GOLD = 10;

// mirrors config/karma.json's thresholds (high: 20, low: -20), split
// into 5 visual steps — a color gradient reads at a glance, a
// good/bad binary doesn't give a kid anything to aim for.
const MARKER_SLOT = 9; // first slot of the main inventory grid — the
// first thing your eye hits the instant the inventory screen opens.
function karmaTier(karma) {
    if (karma >= 20) return { block: "minecraft:lime_concrete", label: "§aKarma: Great!" };
    if (karma >= 5) return { block: "minecraft:green_concrete", label: "§2Karma: Good" };
    if (karma > -5) return { block: "minecraft:white_concrete", label: "§fKarma: Neutral" };
    if (karma > -20) return { block: "minecraft:orange_concrete", label: "§6Karma: Watch out" };
    return { block: "minecraft:red_concrete", label: "§cKarma: In trouble" };
}

function getEconomy(world) {
    try {
        const raw = world.getDynamicProperty(ECONOMY_KEY);
        return typeof raw === "string" && raw ? JSON.parse(raw) : {};
    } catch (e) { return {}; }
}
function saveEconomy(world, econ) {
    world.setDynamicProperty(ECONOMY_KEY, JSON.stringify(econ));
}
function record(world, econ, name) {
    if (!econ[name]) {
        econ[name] = { karma: 0, gold: STARTING_GOLD, jailedUntil: 0, preJail: null };
        // deck.js mirrors karma/gold from telemetry rather than reading the
        // game's dynamic property directly (it can't) — sync the starting
        // values immediately so the deck doesn't show "unknown" until this
        // player's first fine/grant/karma event.
        logTelemetry("economy_sync", name, { karma: 0, gold: STARTING_GOLD });
    }
    return econ[name];
}

function getWeight(world, key) {
    try {
        const raw = world.getDynamicProperty(WEIGHT_OVERRIDE_KEY);
        const overrides = typeof raw === "string" && raw ? JSON.parse(raw) : {};
        if (typeof overrides[key] === "number") return overrides[key];
    } catch (e) { }
    return DEFAULT_WEIGHTS[key] ?? 0;
}

function logTelemetry(kind, who, what) {
    try { console.log("[MOOGUL-T] " + JSON.stringify({ t: Date.now(), kind, who, what, where: null })); } catch (e) { }
}

function adjustKarma(world, name, delta, reason) {
    if (!name || !delta) return;
    const econ = getEconomy(world);
    const rec = record(world, econ, name);
    rec.karma += delta;
    saveEconomy(world, econ);
    logTelemetry("karma", name, { delta, karma: rec.karma, reason: reason || "" });
}

// -- reacted to automatically from telemetry (see SCOPE NOTE above) --
export function onTelemetry(world, entry) {
    try {
        if (entry.kind === "villager_hurt" && entry.who) adjustKarma(world, entry.who, getWeight(world, "villager_hurt"), "hurt a villager");
        else if (entry.kind === "pvp" && entry.who) adjustKarma(world, entry.who, getWeight(world, "excessive_pvp"), "player-vs-player");
    } catch (e) { }
}

// -- manual karma adjust: covers trades/gifts/theft/grief until
// dedicated detection exists ----------------------------------------
export function adjustKarmaEvent({ world, message }) {
    const [name, deltaStr, ...rest] = (message ?? "").trim().split(/\s+/);
    const delta = parseInt(deltaStr, 10);
    if (!name || Number.isNaN(delta)) { world.sendMessage("§c[moogul] usage: /scriptevent moogul:karma <player> <+/-amount> [reason...]"); return; }
    adjustKarma(world, name, delta, rest.join(" "));
    world.sendMessage(`§b[moogul] ${name}'s karma ${delta >= 0 ? "+" : ""}${delta}`);
}

export function setKarmaWeight({ world, message }) {
    const [key, valStr] = (message ?? "").trim().split(/\s+/);
    const val = parseFloat(valStr);
    if (!key || Number.isNaN(val)) { world.sendMessage("§c[moogul] usage: /scriptevent moogul:karmaweight <key> <value>"); return; }
    try {
        const raw = world.getDynamicProperty(WEIGHT_OVERRIDE_KEY);
        const overrides = typeof raw === "string" && raw ? JSON.parse(raw) : {};
        overrides[key] = val;
        world.setDynamicProperty(WEIGHT_OVERRIDE_KEY, JSON.stringify(overrides));
        world.sendMessage(`§b[moogul] karma weight '${key}' set to ${val}`);
    } catch (e) { world.sendMessage("§c[moogul] failed to set weight: " + e); }
}

// -- gold bars: judge-triggered only ----------------------------------
export function finePlayer({ world, message }) { adjustGold(world, message, -1, "fine"); }
export function grantPlayer({ world, message }) { adjustGold(world, message, 1, "grant"); }

function adjustGold(world, message, sign, kindLabel) {
    const [name, amtStr, ...rest] = (message ?? "").trim().split(/\s+/);
    const amt = parseInt(amtStr, 10);
    if (!name || Number.isNaN(amt) || amt < 0) { world.sendMessage(`§c[moogul] usage: /scriptevent moogul:${kindLabel} <player> <amount> [reason...]`); return; }
    const econ = getEconomy(world);
    const rec = record(world, econ, name);
    rec.gold = Math.max(0, rec.gold + sign * amt);
    saveEconomy(world, econ);
    const reason = rest.join(" ");
    const target = world.getAllPlayers().find((p) => p.name === name);
    const msg = `§6[judge] ${name} ${sign > 0 ? "granted" : "fined"} ${amt} gold bar(s)${reason ? " — " + reason : ""}. Balance: ${rec.gold}.`;
    world.sendMessage(msg);
    try { target?.sendMessage(msg); } catch (e) { }
    logTelemetry(sign > 0 ? "grant" : "fine", name, { amount: amt, balance: rec.gold, reason });
}

// -- jail: builds a cage AROUND wherever the player currently is (no
// fixed jail location needed), cancels their block-breaking
// everywhere while jailed ("can't break any block, can only build
// up"), restores them to where they were on release.
// KNOWN LIMITATION: the cage isn't cleaned up after release — it's
// cheap iron-bars/stone/bedrock, but it does leave a small structure
// behind wherever someone was jailed. Fine for a first pass; flagged
// in dungeons/README.md as a improvement if it bothers you in practice. --
function isJailed(world, name) {
    const rec = getEconomy(world)[name];
    return !!(rec && rec.jailedUntil && rec.jailedUntil > Date.now());
}

function buildCage(dimension, center) {
    const r = DEFAULT_JAIL.cage_radius;
    const h = DEFAULT_JAIL.cage_height;
    const x = Math.floor(center.x), y = Math.floor(center.y), z = Math.floor(center.z);
    try {
        dimension.runCommand(`fill ${x - r} ${y - 1} ${z - r} ${x + r} ${y + h} ${z + r} iron_bars hollow`);
        dimension.runCommand(`fill ${x - r} ${y - 1} ${z - r} ${x + r} ${y - 1} ${z + r} stone`);
        dimension.runCommand(`fill ${x - r} ${y + h} ${z - r} ${x + r} ${y + h} ${z + r} bedrock`);
        dimension.runCommand(`fill ${x - r + 1} ${y} ${z - r + 1} ${x + r - 1} ${y + h - 1} ${z + r - 1} air`);
    } catch (e) { }
}

export function jailPlayer({ world, message }) {
    const parts = (message ?? "").trim().split(/\s+/);
    const name = parts[0];
    const minutes = parseInt(parts[1], 10) || DEFAULT_JAIL.default_minutes;
    const target = world.getAllPlayers().find((p) => p.name === name);
    if (!target) { world.sendMessage(`§c[moogul] ${name || "(no name given)"} isn't online — jail needs them online to teleport.`); return; }
    const econ = getEconomy(world);
    const rec = record(world, econ, name);
    const loc = target.location;
    rec.preJail = { x: loc.x, y: loc.y, z: loc.z, dim: (target.dimension.id || "minecraft:overworld").replace("minecraft:", "") };
    rec.jailedUntil = Date.now() + minutes * 60 * 1000;
    saveEconomy(world, econ);
    buildCage(target.dimension, loc);
    try { target.teleport({ x: Math.floor(loc.x) + 0.5, y: Math.floor(loc.y) + 1, z: Math.floor(loc.z) + 0.5 }, { dimension: target.dimension }); } catch (e) { }
    const msg = `§c[judge] ${name} is in jail for ${minutes} minute(s). No block-breaking until release. Building up is still allowed.`;
    world.sendMessage(msg);
    try { target.sendMessage(msg); } catch (e) { }
    logTelemetry("jail", name, { minutes });
}

export function releasePlayer({ world, message }) {
    const name = (message ?? "").trim();
    const econ = getEconomy(world);
    const rec = econ[name];
    if (!rec || !rec.jailedUntil) { world.sendMessage(`§7[moogul] ${name || "(no name given)"} isn't in jail.`); return; }
    rec.jailedUntil = 0;
    const pre = rec.preJail;
    rec.preJail = null;
    saveEconomy(world, econ);
    const target = world.getAllPlayers().find((p) => p.name === name);
    if (target && pre) {
        try { target.teleport({ x: pre.x, y: pre.y, z: pre.z }, { dimension: world.getDimension(pre.dim) }); } catch (e) { }
    }
    const msg = `§a[judge] ${name} is released from jail.`;
    world.sendMessage(msg);
    try { target?.sendMessage(msg); } catch (e) { }
    logTelemetry("release", name, {});
}

// -- karma marker: a colored block that lives in every online player's
// inventory slot 9, always kept in sync with their current karma tier.
// KNOWN TRADE-OFF: that slot is reserved for this — anything a player
// puts there gets replaced within ~2s. One dedicated slot out of 36,
// and it's not a hotbar slot, so it doesn't cost them a tool/weapon
// slot during play. --------------------------------------------------
function refreshKarmaMarkers(world) {
    try {
        const econ = getEconomy(world);
        for (const player of world.getAllPlayers()) {
            try {
                const karma = econ[player.name]?.karma ?? 0;
                const tier = karmaTier(karma);
                const inv = player.getComponent("minecraft:inventory")?.container;
                if (!inv) continue;
                const current = inv.getItem(MARKER_SLOT);
                if (current?.typeId === tier.block) continue; // already correct — don't spam/flicker
                const stack = new ItemStack(tier.block, 1);
                stack.nameTag = tier.label;
                try { stack.setLore(["§7Your karma mood ring —", "§7the color says how you're doing."]); } catch (e) { }
                try { stack.lockMode = ItemLockMode.slot; } catch (e) { /* fine without it — the refresh loop still keeps it correct */ }
                inv.setItem(MARKER_SLOT, stack);
            } catch (e) { }
        }
    } catch (e) { }
}

export function initKarma(world, system) {
    // auto-release anyone whose timer has expired, checked every ~10s
    try {
        system.runInterval(() => {
            try {
                const econ = getEconomy(world);
                const now = Date.now();
                for (const name of Object.keys(econ)) {
                    if (econ[name].jailedUntil && econ[name].jailedUntil <= now) releasePlayer({ world, message: name });
                }
            } catch (e) { }
        }, 200);
    } catch (e) { console.warn("[moogul] karma: jail-timer interval failed to register (" + e + ")"); }

    // every ~2s so a karma change is reflected almost immediately, not
    // just on next inventory open
    try {
        system.runInterval(() => refreshKarmaMarkers(world), 40);
    } catch (e) { console.warn("[moogul] karma: marker refresh did not register (" + e + ") — karma won't show in inventory."); }

    // global block-break guard for jailed players — the actual
    // enforcement mechanism for "can't break any block." If this
    // specific event doesn't exist in this API version, jail becomes
    // cosmetic (teleport + cage) without real enforcement — watch for
    // the registration warning below on first restart.
    try {
        world.beforeEvents.playerBreakBlock.subscribe((ev) => {
            try {
                if (isJailed(world, ev.player?.name)) ev.cancel = true;
            } catch (e) { }
        });
    } catch (e) {
        console.warn("[moogul] karma: block-break guard did not register (" + e + ") — jail will teleport/cage but NOT prevent breaking blocks. Needs attention.");
    }

    console.log("[moogul] Karma + judge online.");
}
