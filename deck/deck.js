// ============================================================
// MINESCAPE COMMAND DECK v1
// Wraps bedrock_server.exe: live log stream + web control room.
// Localhost only — never exposed to the network.
//   node deck.js          (from the deck folder, or via 2-START-SERVER.bat)
// Then open http://localhost:8420
// ============================================================
"use strict";
const http = require("http");
const path = require("path");
const fs = require("fs");
const { spawn } = require("child_process");

const PORT = 8420;
const ROOT = path.join(__dirname, "..");
const SERVER_DIR = path.join(ROOT, "server");
const EXE = path.join(SERVER_DIR, "bedrock_server.exe");
const MUSIC_DIR = path.join(ROOT, "music");
const RP_SRC = path.join(ROOT, "packs", "moogul_core_rp");
const DATA_DIR = path.join(__dirname, "data");
const TELEMETRY_RETENTION_DAYS = 90;

let child = null;
let logBuffer = [];           // last N lines
const MAX_LOG = 800;
const sseClients = new Set();

// --- sustainability state ---
let expectedExit = false;     // true when WE sent 'stop'
let crashTimes = [];          // watchdog: recent unexpected exits
let backupWaiter = null;      // resolves when BDS says files are ready

// shared by both raw log lines and telemetry pushes so a client that
// connects (or reconnects) moments after a burst still sees it via the
// backlog replay on /log — telemetry used to bypass this buffer entirely.
function broadcast(entryStr) {
    logBuffer.push(entryStr);
    if (logBuffer.length > MAX_LOG) logBuffer.shift();
    for (const res of sseClients) res.write(`data: ${entryStr}\n\n`);
}
function pushLog(line) {
    broadcast(JSON.stringify({ t: Date.now(), line }));
}

function startServer() {
    if (child) return false;
    if (!fs.existsSync(EXE)) {
        pushLog("[deck] bedrock_server.exe not found — run 1-SETUP.bat first.");
        return false;
    }
    child = spawn(EXE, [], { cwd: SERVER_DIR });
    expectedExit = false;
    pushLog("[deck] === SERVER STARTING ===");
    const onData = (buf) => buf.toString().split(/\r?\n/).filter(Boolean).forEach((line) => {
        if (!handleTelemetryLine(line)) pushLog(line);
        // backup handshake: BDS confirms world files are safe to copy
        if (backupWaiter && /ready to be copied|Data saved/i.test(line)) {
            const w = backupWaiter; backupWaiter = null; w();
        }
    });
    child.stdout.on("data", onData);
    child.stderr.on("data", onData);
    child.on("exit", (code) => {
        pushLog(`[deck] === SERVER STOPPED (code ${code}) ===`);
        child = null;
        // --- watchdog: revive after a crash (but never fight an intentional stop) ---
        if (!expectedExit) {
            const now = Date.now();
            crashTimes = crashTimes.filter((t) => now - t < 10 * 60 * 1000);
            crashTimes.push(now);
            if (crashTimes.length <= 3) {
                pushLog("[deck] Unexpected stop - restarting in 5s (watchdog)...");
                setTimeout(startServer, 5000);
            } else {
                pushLog("[deck] Crashed 4x in 10 min - watchdog standing down. Check the feed for errors.");
            }
        }
    });
    return true;
}

// --- automatic nightly backup (4:00 AM), safe-copy protocol ---
async function runBackup(reason) {
    if (!child) { pushLog("[deck] backup skipped - server offline (" + reason + ")"); return; }
    pushLog("[deck] Nightly backup starting (" + reason + ")...");
    try {
        sendCommand("save hold");
        await new Promise((resolve) => {
            backupWaiter = resolve;
            const poll = setInterval(() => { if (backupWaiter && child) child.stdin.write("save query\n"); }, 2000);
            setTimeout(() => { clearInterval(poll); if (backupWaiter) { backupWaiter = null; resolve(); } }, 30000);
        });
        const stamp = new Date().toISOString().slice(0, 16).replace(/[T:]/g, "-");
        const dest = path.join(__dirname, "..", "backups", "auto_" + stamp);
        fs.cpSync(path.join(SERVER_DIR, "worlds"), dest, { recursive: true });
        pushLog("[deck] Backup complete: backups/auto_" + stamp);
        // keep newest 14 automatic backups
        const bdir = path.join(__dirname, "..", "backups");
        const autos = fs.readdirSync(bdir).filter((n) => n.startsWith("auto_")).sort().reverse();
        for (const old of autos.slice(14)) fs.rmSync(path.join(bdir, old), { recursive: true, force: true });
    } catch (e) {
        pushLog("[deck] Backup FAILED: " + e.message);
    } finally {
        sendCommand("save resume");
    }
}
function scheduleNightlyBackup() {
    const now = new Date();
    const next = new Date(now);
    next.setHours(4, 0, 0, 0);
    if (next <= now) next.setDate(next.getDate() + 1);
    setTimeout(() => { runBackup("scheduled"); scheduleNightlyBackup(); }, next - now);
}
scheduleNightlyBackup();

function sendCommand(cmd) {
    if (!child) { pushLog("[deck] server is offline — command ignored: " + cmd); return false; }
    if (cmd === "stop") expectedExit = true;   // intentional: watchdog stands down
    if (cmd === "backup") { runBackup("manual"); return true; }  // deck-triggered safe backup
    child.stdin.write(cmd + "\n");
    pushLog("[deck] > " + cmd);
    return true;
}

// --- AMBIENCE: music library pipeline -------------------------
// Drop .ogg files into /music, hit REBUILD in the deck, restart.
// Tracks ship to players inside the resource pack; play globally
// with: playsound moogul.music.<name> @a
function listMusic() {
    try {
        const defs = JSON.parse(fs.readFileSync(path.join(RP_SRC, "sounds", "sound_definitions.json"), "utf8"));
        return Object.keys(defs.sound_definitions || {}).filter((k) => k.startsWith("moogul.music."));
    } catch (e) { return []; }
}

function rebuildMusic() {
    fs.mkdirSync(MUSIC_DIR, { recursive: true });
    const files = fs.readdirSync(MUSIC_DIR).filter((f) => f.toLowerCase().endsWith(".ogg"));
    const sndDir = path.join(RP_SRC, "sounds", "music");
    fs.mkdirSync(sndDir, { recursive: true });
    const defs = {};
    const tracks = [];
    for (const f of files) {
        const slug = f.slice(0, -4).toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_+|_+$/g, "") || "track";
        fs.copyFileSync(path.join(MUSIC_DIR, f), path.join(sndDir, slug + ".ogg"));
        defs["moogul.music." + slug] = {
            category: "music",
            sounds: [{ name: "sounds/music/" + slug, volume: 1.0, stream: true, load_on_low_memory: true }],
        };
        tracks.push("moogul.music." + slug);
    }
    fs.writeFileSync(
        path.join(RP_SRC, "sounds", "sound_definitions.json"),
        JSON.stringify({ format_version: "1.14.0", sound_definitions: defs }, null, 2)
    );
    // bump the pack version so every client re-downloads it
    const manPath = path.join(RP_SRC, "manifest.json");
    const man = JSON.parse(fs.readFileSync(manPath, "utf8"));
    man.header.version[2]++;
    man.modules[0].version = man.header.version;
    fs.writeFileSync(manPath, JSON.stringify(man, null, 2));
    // keep world pack references in step with the new version - every world folder
    // (Minescape, Spelljammer, any future copy), not just whichever is live
    const worldPackRefs = [path.join(ROOT, "world_templates", "Minescape", "world_resource_packs.json")];
    try {
        for (const w of fs.readdirSync(path.join(SERVER_DIR, "worlds"))) {
            worldPackRefs.push(path.join(SERVER_DIR, "worlds", w, "world_resource_packs.json"));
        }
    } catch (e) { /* no worlds yet */ }
    for (const wj of worldPackRefs) {
        try {
            const arr = JSON.parse(fs.readFileSync(wj, "utf8"));
            arr[0].version = man.header.version;
            fs.writeFileSync(wj, JSON.stringify(arr, null, 2));
        } catch (e) { }
    }
    // sync the rebuilt pack onto the live server
    fs.cpSync(RP_SRC, path.join(SERVER_DIR, "resource_packs", "moogul_core_rp"), { recursive: true, force: true });
    pushLog(`[deck] Music library rebuilt: ${tracks.length} track(s), pack v${man.header.version.join(".")}. Restart to ship it to players.`);
    return { tracks, version: man.header.version };
}

// --- TELEMETRY: parse [MOOGUL-T] lines from the child's stdout, store as
// daily-rotated JSONL, maintain a live per-player index, and answer the
// deck's Players/forensics queries. Never leaves the PC. ------------------
function dayStamp(d) { return d.toISOString().slice(0, 10); } // YYYY-MM-DD
function telemetryFilePath(d = new Date()) {
    return path.join(DATA_DIR, `telemetry-${dayStamp(d)}.jsonl`);
}

let playerIndex = {};   // name -> { lastSeen, lastLocation, counts:{...}, firstSeen }
let indexDirty = false;
const INDEX_PATH = path.join(DATA_DIR, "players-index.json");

function loadPlayerIndex() {
    try { playerIndex = JSON.parse(fs.readFileSync(INDEX_PATH, "utf8")); } catch (e) { playerIndex = {}; }
}
function savePlayerIndexNow() {
    try {
        fs.mkdirSync(DATA_DIR, { recursive: true });
        fs.writeFileSync(INDEX_PATH, JSON.stringify(playerIndex, null, 2));
        indexDirty = false;
    } catch (e) { pushLog("[deck] player index save failed: " + e.message); }
}
setInterval(() => { if (indexDirty) savePlayerIndexNow(); }, 5000);

function emptyCounts() { return { blocksBroken: 0, blocksPlaced: 0, deaths: 0, chats: 0 }; }

// kinds that are judge/system bookkeeping, not the player actually doing
// something — excluded from bumping "last seen" so that stays meaningful.
const ADMIN_KINDS = new Set(["economy_sync", "jail", "release", "fine", "grant"]);

function updatePlayerIndex(entry) {
    const name = entry.who;
    if (!name || typeof name !== "string") return;
    if (!playerIndex[name]) {
        playerIndex[name] = { firstSeen: entry.t, lastSeen: entry.t, lastLocation: null, counts: emptyCounts(), karma: 0, gold: null, jailed: false };
    }
    const rec = playerIndex[name];
    if (!ADMIN_KINDS.has(entry.kind)) rec.lastSeen = entry.t;
    if (entry.where) rec.lastLocation = entry.where;
    switch (entry.kind) {
        case "block_break": rec.counts.blocksBroken++; break;
        case "block_place": rec.counts.blocksPlaced++; break;
        case "death": rec.counts.deaths++; break;
        case "chat": rec.counts.chats++; break;
        case "economy_sync": rec.karma = entry.what?.karma ?? rec.karma; rec.gold = entry.what?.gold ?? rec.gold; break;
        case "karma": rec.karma = entry.what?.karma ?? rec.karma; break;
        case "fine": case "grant": rec.gold = entry.what?.balance ?? rec.gold; break;
        case "jail": rec.jailed = true; break;
        case "release": rec.jailed = false; break;
    }
    indexDirty = true;
}

function appendTelemetry(entry) {
    updatePlayerIndex(entry);
    fs.mkdir(DATA_DIR, { recursive: true }, () => {
        fs.appendFile(telemetryFilePath(new Date(entry.t)), JSON.stringify(entry) + "\n", (err) => {
            if (err) pushLog("[deck] telemetry write failed: " + err.message);
        });
    });
}

function pruneOldTelemetry() {
    try {
        const cutoff = Date.now() - TELEMETRY_RETENTION_DAYS * 24 * 60 * 60 * 1000;
        for (const f of fs.readdirSync(DATA_DIR)) {
            const m = f.match(/^telemetry-(\d{4}-\d{2}-\d{2})\.jsonl$/);
            if (!m) continue;
            if (new Date(m[1] + "T00:00:00Z").getTime() < cutoff) fs.unlinkSync(path.join(DATA_DIR, f));
        }
    } catch (e) { /* DATA_DIR may not exist yet — fine */ }
}
setInterval(pruneOldTelemetry, 6 * 60 * 60 * 1000); // every 6h is plenty for a daily-rotated file

// kinds that are pure bookkeeping (not shown in the live World Feed —
// they'd flood it, or aren't narratable moments) still get written to
// disk / update the index above.
const SILENT_KINDS = new Set(["position", "economy_sync", "dungeon_catalog"]);

// dungeons live in the game's dynamic properties, same as karma/gold —
// deck.js can't read those directly, so it mirrors them from telemetry
// too (see dungeons.js's logStatus / initDungeons catalog sync).
let dungeonCatalog = [];  // [{name, label, locked}], from dungeon_catalog
let dungeonStatus = {};   // name -> {status, label, lastUpdate, origin}

function handleDungeonTelemetry(entry) {
    if (entry.kind === "dungeon_catalog" && Array.isArray(entry.what)) {
        dungeonCatalog = entry.what;
    } else if (entry.kind === "dungeon" && entry.what?.name) {
        dungeonStatus[entry.what.name] = { status: entry.what.status, label: entry.what.label, lastUpdate: entry.t, origin: entry.where };
    }
}

function handleTelemetryLine(line) {
    const marker = "[MOOGUL-T]";
    const idx = line.indexOf(marker);
    if (idx === -1) return false;
    let entry;
    try { entry = JSON.parse(line.slice(idx + marker.length).trim()); } catch (e) { return false; }
    if (!entry || typeof entry.kind !== "string") return false;
    appendTelemetry(entry);
    handleDungeonTelemetry(entry);
    if (!SILENT_KINDS.has(entry.kind)) {
        broadcast(JSON.stringify({ t: Date.now(), telemetry: entry }));
    }
    return true;
}

function listPlayers() {
    return Object.entries(playerIndex)
        .map(([name, rec]) => ({ name, ...rec }))
        .sort((a, b) => b.lastSeen - a.lastSeen);
}

// scans newest-file-first until `limit` matching entries are found or we
// run out of retained history — fine at home-server scale (a handful of
// players, files rotate daily).
function playerTimeline(name, limit) {
    const out = [];
    let files;
    try { files = fs.readdirSync(DATA_DIR).filter((f) => /^telemetry-\d{4}-\d{2}-\d{2}\.jsonl$/.test(f)).sort().reverse(); }
    catch (e) { return out; }
    for (const f of files) {
        if (out.length >= limit) break;
        let lines;
        try { lines = fs.readFileSync(path.join(DATA_DIR, f), "utf8").split("\n").filter(Boolean); } catch (e) { continue; }
        for (let i = lines.length - 1; i >= 0 && out.length < limit; i--) {
            try {
                const entry = JSON.parse(lines[i]);
                if (SILENT_KINDS.has(entry.kind)) continue;
                if (entry.who === name || entry.what === name) out.push(entry);
            } catch (e) { }
        }
    }
    return out;
}

// "who broke blocks near X within N minutes" — settle disputes with receipts.
function forensicsQuery({ x, y, z, radius, minutes, dimension }) {
    const cutoff = Date.now() - minutes * 60 * 1000;
    const out = [];
    let files;
    try { files = fs.readdirSync(DATA_DIR).filter((f) => /^telemetry-\d{4}-\d{2}-\d{2}\.jsonl$/.test(f)).sort().reverse(); }
    catch (e) { return out; }
    for (const f of files) {
        let lines;
        try { lines = fs.readFileSync(path.join(DATA_DIR, f), "utf8").split("\n").filter(Boolean); } catch (e) { continue; }
        let stop = false;
        for (const line of lines) {
            let entry;
            try { entry = JSON.parse(line); } catch (e) { continue; }
            if (entry.t < cutoff) { stop = true; continue; } // still finish this file (unsorted-ish across ticks), but no earlier files
            if (entry.kind !== "block_break" && entry.kind !== "block_place") continue;
            const w = entry.where;
            if (!w) continue;
            if (dimension && w.dim !== dimension) continue;
            const d = Math.hypot(w.x - x, w.y - y, w.z - z);
            if (d <= radius) out.push(entry);
        }
        if (stop) break;
    }
    return out.sort((a, b) => b.t - a.t);
}

loadPlayerIndex();

// --- static assets: design-system.css + everything under deck/assets/ -----
// (fonts, icon sprite — kept generic so new assets don't need new routes)
const MIME_TYPES = {
    ".css": "text/css; charset=utf-8",
    ".svg": "image/svg+xml",
    ".woff2": "font/woff2",
    ".ttf": "font/ttf",
    ".txt": "text/plain; charset=utf-8",
    ".png": "image/png",
    ".html": "text/html; charset=utf-8",
};
function serveStatic(relPath, res) {
    const full = path.join(__dirname, relPath);
    // path-traversal guard: resolved path must stay inside deck/
    if (!full.startsWith(__dirname + path.sep)) { res.writeHead(403); res.end(); return; }
    fs.readFile(full, (err, data) => {
        if (err) { res.writeHead(404); res.end(); return; }
        const type = MIME_TYPES[path.extname(full).toLowerCase()] || "application/octet-stream";
        res.writeHead(200, { "Content-Type": type, "Cache-Control": "no-cache" });
        res.end(data);
    });
}

function readJsonBody(req, cb) {
    let body = "";
    req.on("data", (c) => (body += c));
    req.on("end", () => {
        let parsed = {};
        try { parsed = JSON.parse(body || "{}"); } catch (e) { }
        cb(parsed);
    });
}
// strips newlines (accidental multi-command injection into the console
// stdin) from free-text fields before they go into a scriptevent command
function clean(v) { return String(v ?? "").replace(/[\r\n]/g, " ").trim(); }

const server = http.createServer((req, res) => {
    // localhost guard
    const remote = req.socket.remoteAddress || "";
    if (!remote.includes("127.0.0.1") && !remote.includes("::1")) { res.writeHead(403); res.end(); return; }

    const u = new URL(req.url, "http://localhost");
    const playerTimelineMatch = req.method === "GET" && u.pathname.match(/^\/player\/([^/]+)\/timeline$/);

    if (req.method === "GET" && (req.url === "/" || req.url === "/index.html")) {
        fs.readFile(path.join(__dirname, "index.html"), (err, data) => {
            if (err) { res.writeHead(500); res.end("deck ui missing"); return; }
            res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
            res.end(data);
        });
    } else if (req.method === "GET" && req.url === "/design-system.css") {
        serveStatic("design-system.css", res);
    } else if (req.method === "GET" && req.url.startsWith("/assets/")) {
        serveStatic(req.url.slice(1), res);
    } else if (req.method === "GET" && req.url === "/log") {
        res.writeHead(200, {
            "Content-Type": "text/event-stream",
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
        });
        for (const entry of logBuffer) res.write(`data: ${entry}\n\n`);
        sseClients.add(res);
        req.on("close", () => sseClients.delete(res));
    } else if (req.method === "GET" && req.url === "/music-list") {
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ tracks: listMusic() }));
    } else if (req.method === "POST" && req.url === "/rebuild-music") {
        let out;
        try { out = rebuildMusic(); } catch (e) { out = { error: e.message }; pushLog("[deck] Music rebuild FAILED: " + e.message); }
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify(out));
    } else if (req.method === "GET" && req.url === "/status") {
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ online: !!child }));
    } else if (req.method === "GET" && u.pathname === "/players") {
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ players: listPlayers() }));
    } else if (playerTimelineMatch) {
        const name = decodeURIComponent(playerTimelineMatch[1]);
        const limit = Math.min(500, Math.max(1, parseInt(u.searchParams.get("limit"), 10) || 200));
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ name, player: playerIndex[name] || null, timeline: playerTimeline(name, limit) }));
    } else if (req.method === "GET" && u.pathname === "/forensics") {
        const q = {
            x: parseFloat(u.searchParams.get("x")),
            y: parseFloat(u.searchParams.get("y")),
            z: parseFloat(u.searchParams.get("z")),
            radius: parseFloat(u.searchParams.get("radius")) || 16,
            minutes: parseFloat(u.searchParams.get("minutes")) || 30,
            dimension: u.searchParams.get("dimension") || undefined,
        };
        if ([q.x, q.y, q.z].some(Number.isNaN)) {
            res.writeHead(400, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ error: "x, y, z are required numbers" }));
        } else {
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ query: q, results: forensicsQuery(q) }));
        }
    } else if (req.method === "GET" && u.pathname === "/dungeons") {
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ catalog: dungeonCatalog, status: dungeonStatus }));
    } else if (req.method === "POST" && u.pathname === "/dungeon/place") {
        readJsonBody(req, (b) => {
            const name = clean(b.name), mark = clean(b.mark);
            const ok = name && mark ? sendCommand(`scriptevent moogul:dungeon place ${name} ${mark}`) : false;
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok }));
        });
    } else if (req.method === "POST" && u.pathname === "/judge/jail") {
        readJsonBody(req, (b) => {
            const name = clean(b.name);
            const minutes = parseInt(b.minutes, 10) || 10;
            const ok = name ? sendCommand(`scriptevent moogul:jail ${name} ${minutes}`) : false;
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok }));
        });
    } else if (req.method === "POST" && u.pathname === "/judge/release") {
        readJsonBody(req, (b) => {
            const name = clean(b.name);
            const ok = name ? sendCommand(`scriptevent moogul:release ${name}`) : false;
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok }));
        });
    } else if (req.method === "POST" && (u.pathname === "/judge/fine" || u.pathname === "/judge/grant")) {
        const kind = u.pathname === "/judge/fine" ? "fine" : "grant";
        readJsonBody(req, (b) => {
            const name = clean(b.name);
            const amount = parseInt(b.amount, 10) || 0;
            const reason = clean(b.reason);
            const ok = name && amount > 0 ? sendCommand(`scriptevent moogul:${kind} ${name} ${amount}${reason ? " " + reason : ""}`) : false;
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok }));
        });
    } else if (req.method === "POST" && (req.url === "/cmd" || req.url === "/start")) {
        let body = "";
        req.on("data", (c) => (body += c));
        req.on("end", () => {
            let ok = false;
            if (req.url === "/start") ok = startServer();
            else {
                try { ok = sendCommand(String(JSON.parse(body).cmd || "").trim()); } catch (e) { }
            }
            res.writeHead(200, { "Content-Type": "application/json" });
            res.end(JSON.stringify({ ok }));
        });
    } else { res.writeHead(404); res.end(); }
});

server.listen(PORT, "127.0.0.1", () => {
    console.log(`[deck] Minescape Command Deck at  http://localhost:${PORT}`);
    startServer();
});

// clean shutdown: stop the world properly, flush telemetry, then exit
process.on("SIGINT", () => {
    if (indexDirty) savePlayerIndexNow();
    if (child) { sendCommand("stop"); setTimeout(() => process.exit(0), 3000); }
    else process.exit(0);
});
