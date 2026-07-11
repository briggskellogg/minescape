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

let child = null;
let logBuffer = [];           // last N lines
const MAX_LOG = 800;
const sseClients = new Set();

// --- sustainability state ---
let expectedExit = false;     // true when WE sent 'stop'
let crashTimes = [];          // watchdog: recent unexpected exits
let backupWaiter = null;      // resolves when BDS says files are ready

function pushLog(line) {
    const entry = JSON.stringify({ t: Date.now(), line });
    logBuffer.push(entry);
    if (logBuffer.length > MAX_LOG) logBuffer.shift();
    for (const res of sseClients) res.write(`data: ${entry}\n\n`);
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
        pushLog(line);
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
    // keep world pack references in step with the new version
    for (const wj of [
        path.join(ROOT, "world_templates", "Minescape", "world_resource_packs.json"),
        path.join(SERVER_DIR, "worlds", "Minescape", "world_resource_packs.json"),
    ]) {
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

const server = http.createServer((req, res) => {
    // localhost guard
    const remote = req.socket.remoteAddress || "";
    if (!remote.includes("127.0.0.1") && !remote.includes("::1")) { res.writeHead(403); res.end(); return; }

    if (req.method === "GET" && (req.url === "/" || req.url === "/index.html")) {
        fs.readFile(path.join(__dirname, "index.html"), (err, data) => {
            if (err) { res.writeHead(500); res.end("deck ui missing"); return; }
            res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
            res.end(data);
        });
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

// clean shutdown: stop the world properly, then exit
process.on("SIGINT", () => {
    if (child) { sendCommand("stop"); setTimeout(() => process.exit(0), 3000); }
    else process.exit(0);
});
