#Requires -Version 5.1
[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
$python = Get-PrivatePython
$mineJammerRoot = Join-Path $repoRoot 'MineJammer'
$lock = Get-Content -LiteralPath (Join-Path $repoRoot 'artifacts\artifacts.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json

function Get-ArtifactPath([string] $Key) {
    $artifact = $lock.artifacts | Where-Object { $_.key -eq $Key } | Select-Object -First 1
    if (-not $artifact) { throw "Artifact lock is missing $Key." }
    $path = Join-Path (Join-Path $repoRoot "artifacts\cache\$Key") ([string]$artifact.filename)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Artifact cache is missing $Key." }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash.ToLowerInvariant() -ne ([string]$artifact.sha512).ToLowerInvariant()) {
        throw "Artifact cache hash mismatch: $Key"
    }
    return $path
}

$minecraftLockPath = Join-Path $repoRoot 'artifacts\minecraft-server.lock.json'
if (-not (Test-Path -LiteralPath $minecraftLockPath -PathType Leaf)) { throw 'Run ops\acquire-minecraft-server.ps1 first.' }
$minecraftLock = Get-Content -LiteralPath $minecraftLockPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($minecraftLock.schema -ne 'minescape.minecraft-server-lock.v1' -or $minecraftLock.version -ne '26.2' -or
    [string]$minecraftLock.bundled_server_sha512 -notmatch '^[0-9a-f]{128}$') {
    throw 'Official Minecraft server lock is malformed or targets a different version.'
}
$vanilla = [IO.Path]::GetFullPath((Join-Path $repoRoot ([string]$minecraftLock.bundled_server_relative -replace '/', '\')))
$minecraftCacheRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\cache\minecraft_server'))
if (-not $vanilla.StartsWith($minecraftCacheRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Pinned vanilla server archive path escaped the Minecraft cache.'
}
if (-not (Test-Path -LiteralPath $vanilla -PathType Leaf) -or
    (Get-FileHash -LiteralPath $vanilla -Algorithm SHA512).Hash.ToLowerInvariant() -ne ([string]$minecraftLock.bundled_server_sha512).ToLowerInvariant()) {
    throw 'Pinned vanilla server archive is missing or changed.'
}
$matcha = Get-ArtifactPath 'matcha'
$terralith = Get-ArtifactPath 'terralith'
$nullscape = Get-ArtifactPath 'nullscape'
$build = Join-Path $mineJammerRoot 'build'
$reports = Join-Path $mineJammerRoot 'reports\local'
New-DirectoryIfMissing $build
New-DirectoryIfMissing $reports

$pythonCode = "import runpy,sys; sys.path.insert(0,r'''$mineJammerRoot'''); sys.argv=['minejammer']+sys.argv[1:]; runpy.run_module('minejammer',run_name='__main__')"
function Invoke-MineJammer([string[]] $Arguments) {
    $null = & $python -c $pythonCode @Arguments
    if ($LASTEXITCODE -ne 0) { throw "MineJammer generator failed: $($Arguments[0])" }
    Write-Host "Verified generator: $($Arguments[0])"
}

Invoke-MineJammer @('compat-candidates', '--matcha', $matcha, '--terralith', $terralith, '--nullscape', $nullscape, '--report', (Join-Path $reports 'compatibility-candidates.json'))
Invoke-MineJammer @('build-village-adapter', '--vanilla', $vanilla, '--matcha', $matcha, '--output', (Join-Path $build 'MineScape-Villages.zip'), '--report', (Join-Path $reports 'villages.json'))
Invoke-MineJammer @('build-fishing-adapter', '--matcha', $matcha, '--terralith', $terralith, '--policy', (Join-Path $mineJammerRoot 'config\fishing-climates.json'), '--output', (Join-Path $build 'MineScape-Fishing.zip'), '--report', (Join-Path $reports 'fishing.json'))
Invoke-MineJammer @('build-loot-spec', '--matcha', $matcha, '--terralith', $terralith, '--policy', (Join-Path $mineJammerRoot 'config\loot-adapter.policy.json'), '--output', (Join-Path $build 'loot-adapter.lock.json'))
Invoke-MineJammer @('build-food-spec', '--matcha', $matcha, '--terralith', $terralith, '--output', (Join-Path $build 'food-normalizer.lock.json'))
Invoke-MineJammer @('build-curio-spec', '--matcha', $matcha, '--output', (Join-Path $build 'curio-cache.lock.json'))

$compatibilityPolicy = Get-Content -LiteralPath (Join-Path $mineJammerRoot 'config\compatibility.policy.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($compatibilityPolicy.allowlist_reviewed -eq $true) {
    Invoke-MineJammer @('build-compat', '--matcha', $matcha, '--terralith', $terralith, '--nullscape', $nullscape, '--policy', (Join-Path $mineJammerRoot 'config\compatibility.policy.json'), '--output', (Join-Path $build 'MineScape-Compatibility.zip'), '--report', (Join-Path $reports 'compatibility.json'))
} else {
    Write-Host 'Compatibility candidates were generated, but the overlay remains blocked pending exact JSON-pointer review.' -ForegroundColor Yellow
}

Write-Host 'Generated every currently approved adapter/spec; no world was started.' -ForegroundColor Green
