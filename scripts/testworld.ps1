# ============================================================
# MINESCAPE / SPELLJAMMER - same-seed test world manager.
#
# Spelljammer is a replica of Minescape (same seed, copied world) you can
# safely build and experiment in. It never changes Minescape on its own.
# When something you built in Spelljammer is ready for the kids, export it
# with a Structure Block and use 'commit' to bring that one piece into
# Minescape - see dungeons\README.md ("Path B: hand-built") for the rest
# of that workflow.
#
# Actions:
#   refresh          - copy Minescape -> Spelljammer (replaces old Spelljammer)
#   build            - set the live world to Spelljammer (for building/testing)
#   play             - set the live world back to Minescape (for the kids)
#   commit <name>    - copy Spelljammer\structures\<name>.mcstructure into
#                       packs\moogul_core_bp\structures\ (the shared source of truth)
#   status           - show which world is currently live
# ============================================================
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("refresh", "build", "play", "commit", "status")]
    [string]$Action,
    [string]$Name
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$worldsDir = Join-Path $root "server\worlds"
$minescapeDir = Join-Path $worldsDir "Minescape"
$spelljammerDir = Join-Path $worldsDir "Spelljammer"
$propsPath = Join-Path $root "server\server.properties"

function Get-LevelName {
    if (-not (Test-Path $propsPath)) { return $null }
    $line = Get-Content $propsPath | Where-Object { $_ -match "^level-name=" } | Select-Object -First 1
    if ($line) { return ($line -split "=", 2)[1].Trim() }
    return $null
}

function Set-LevelName([string]$name) {
    $found = $false
    $content = Get-Content $propsPath | ForEach-Object {
        if ($_ -match "^level-name=") { $found = $true; "level-name=$name" } else { $_ }
    }
    if (-not $found) { $content += "level-name=$name" }
    Set-Content -Path $propsPath -Value $content
}

function Assert-ServerStopped {
    $proc = Get-Process -Name "bedrock_server" -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "The server is running - stop it first (deck 'Stop' button, or close its console)." -ForegroundColor Red
        Write-Host "Copying or switching worlds while BDS has one open can corrupt the save." -ForegroundColor Red
        exit 1
    }
}

switch ($Action) {
    "refresh" {
        Assert-ServerStopped
        if (-not (Test-Path $minescapeDir)) { Write-Host "No Minescape world yet - nothing to copy from." -ForegroundColor Yellow; exit 1 }
        if (Test-Path $spelljammerDir) { Remove-Item $spelljammerDir -Recurse -Force }
        Copy-Item $minescapeDir $spelljammerDir -Recurse
        Write-Host "Spelljammer refreshed from Minescape - exact replica, same seed (6246468738900744)." -ForegroundColor Green
        Write-Host "Run '.\4-TEST-WORLD.bat build' to boot into it." -ForegroundColor Green
    }
    "build" {
        if (-not (Test-Path $propsPath)) { Write-Host "No server\server.properties yet - run '.\1-SETUP.bat' first." -ForegroundColor Yellow; exit 1 }
        if (-not (Test-Path $spelljammerDir)) { Write-Host "No Spelljammer world yet - run '.\4-TEST-WORLD.bat refresh' first." -ForegroundColor Yellow; exit 1 }
        Assert-ServerStopped
        Set-LevelName "Spelljammer"
        Write-Host "Live world set to Spelljammer. Start the server - Minescape is untouched." -ForegroundColor Green
        Write-Host "Note: re-running 1-SETUP.bat re-applies config\server.properties and will flip this back to Minescape - just run 'build' again if that happens." -ForegroundColor DarkGray
    }
    "play" {
        if (-not (Test-Path $propsPath)) { Write-Host "No server\server.properties yet - run '.\1-SETUP.bat' first." -ForegroundColor Yellow; exit 1 }
        Assert-ServerStopped
        Set-LevelName "Minescape"
        Write-Host "Live world set back to Minescape. Start the server - safe for the kids." -ForegroundColor Green
    }
    "commit" {
        if (-not $Name -or $Name -notmatch "^[A-Za-z0-9_\-]+$") {
            Write-Host "Usage: .\4-TEST-WORLD.bat commit <structure-name>   (letters/numbers/_/- only)" -ForegroundColor Yellow
            exit 1
        }
        $src = Join-Path $spelljammerDir "structures\$Name.mcstructure"
        if (-not (Test-Path $src)) {
            Write-Host "Can't find $src" -ForegroundColor Red
            Write-Host "Export it first: place a Structure Block in Spelljammer, Save mode, name it '$Name', hit Export." -ForegroundColor Yellow
            exit 1
        }
        $destDir = Join-Path $root "packs\moogul_core_bp\structures"
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
        Copy-Item $src (Join-Path $destDir "$Name.mcstructure") -Force
        Write-Host "Committed: packs\moogul_core_bp\structures\$Name.mcstructure (the shared source of truth)." -ForegroundColor Green
        Write-Host "Next: run '.\1-SETUP.bat' to deploy it, then in Minescape:" -ForegroundColor Green
        Write-Host "  /structure load moogul:$Name <x> <y> <z>" -ForegroundColor Cyan
        Write-Host "or register it as a dungeon piece in dungeons.js - see dungeons\README.md (Path B)." -ForegroundColor Green
    }
    "status" {
        $live = Get-LevelName
        Write-Host "Live world (server\server.properties level-name): $live"
        if (Test-Path $spelljammerDir) {
            $stamp = (Get-Item $spelljammerDir).LastWriteTime
            Write-Host "Spelljammer replica exists - last refreshed $stamp"
        } else {
            Write-Host "Spelljammer replica does not exist yet - run 'refresh' to create it."
        }
    }
}
