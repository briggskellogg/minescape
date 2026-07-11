# MINESCAPE BACKUP - timestamped copy of every world.
# Best run while the server is stopped (or right after 'save hold' from the deck).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$worlds = Join-Path $root "server\worlds"
if (-not (Test-Path $worlds)) { Write-Host "No worlds yet - nothing to back up."; exit }
$stamp = Get-Date -Format "yyyy-MM-dd_HH-mm"
$dest = Join-Path $root "backups\$stamp"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $worlds $dest -Recurse
Write-Host "Backed up worlds to backups\$stamp" -ForegroundColor Green
# keep the newest 30 backups
Get-ChildItem (Join-Path $root "backups") | Sort-Object Name -Descending | Select-Object -Skip 30 | Remove-Item -Recurse -Force
