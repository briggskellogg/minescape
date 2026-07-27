# ============================================================
# MINESCAPE SETUP — downloads BDS, installs config + packs.
# Safe to re-run: it refreshes packs/config without touching
# your world or your live allowlist.
# ============================================================
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$serverDir = Join-Path $root "server"
$BDS_URL = "https://www.minecraft.net/bedrockdedicatedserver/bin-win/bedrock-server-1.26.33.2.zip"

Write-Host ""
Write-Host "  === MINESCAPE SETUP ===" -ForegroundColor Magenta
Write-Host ""

# --- 1. Download + extract BDS (only if not already installed) ---
if (-not (Test-Path (Join-Path $serverDir "bedrock_server.exe"))) {
    New-Item -ItemType Directory -Force -Path $serverDir | Out-Null
    $zip = Join-Path $root "bds.zip"
    Write-Host "  [1/4] Downloading Bedrock Dedicated Server 1.26.33.2 (~150 MB)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $BDS_URL -OutFile $zip -UserAgent "Mozilla/5.0"
    Write-Host "        Extracting..." -ForegroundColor Cyan
    Expand-Archive -Path $zip -DestinationPath $serverDir -Force
    Remove-Item $zip
    Write-Host "        BDS installed." -ForegroundColor Green
} else {
    Write-Host "  [1/4] BDS already installed - skipping download." -ForegroundColor DarkGray
}

# --- 2. Config: server.properties always applied; allowlist/permissions only if missing ---
Write-Host "  [2/4] Applying config..." -ForegroundColor Cyan
Copy-Item (Join-Path $root "config\server.properties") $serverDir -Force
foreach ($f in @("allowlist.json", "permissions.json")) {
    $dest = Join-Path $serverDir $f
    # BDS ships blank versions of these - treat tiny/empty files as missing
    if (-not (Test-Path $dest) -or ((Get-Item $dest).Length -lt 10)) {
        Copy-Item (Join-Path $root "config\$f") $dest -Force
    }
}

# --- 3. Install packs + world pack wiring ---
Write-Host "  [3/4] Installing Moogul Core packs..." -ForegroundColor Cyan
$bpDest = Join-Path $serverDir "behavior_packs\moogul_core_bp"
$rpDest = Join-Path $serverDir "resource_packs\moogul_core_rp"
foreach ($d in @($bpDest, $rpDest)) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
Copy-Item (Join-Path $root "packs\moogul_core_bp") $bpDest -Recurse
Copy-Item (Join-Path $root "packs\moogul_core_rp") $rpDest -Recurse

$worldDir = Join-Path $serverDir "worlds\Minescape"
New-Item -ItemType Directory -Force -Path $worldDir | Out-Null
Copy-Item (Join-Path $root "world_templates\Minescape\*.json") $worldDir -Force

# --- 4. Check for Node (powers the Command Deck) ---
Write-Host "  [4/4] Checking for Node.js (Command Deck)..." -ForegroundColor Cyan
$node = Get-Command node -ErrorAction SilentlyContinue
if ($node) {
    Write-Host "        Node found: $($node.Source)" -ForegroundColor Green
} else {
    Write-Host "        Node.js not found. The server still runs, but the Command Deck needs it." -ForegroundColor Yellow
    Write-Host "        Install from https://nodejs.org (LTS), then re-run 2-START-SERVER.bat." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  SETUP COMPLETE." -ForegroundColor Magenta
Write-Host "  Next: run 2-START-SERVER.bat  ->  deck at http://localhost:8420" -ForegroundColor White
Write-Host "  (Windows Firewall will ask on first launch - click Allow.)" -ForegroundColor DarkGray
Write-Host ""
