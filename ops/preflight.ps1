[CmdletBinding()]
param(
    [switch] $Release,
    [switch] $SkipBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Require-Path([string] $Path, [string] $Label) {
    if (-not (Test-Path -LiteralPath $Path)) { $failures.Add("Missing ${Label}: $Path") }
}

Require-Path (Join-Path $repoRoot 'MineDeck') 'MineDeck source'
Require-Path (Join-Path $repoRoot 'MineScape') 'MineScape source'
Require-Path (Join-Path $repoRoot 'MineJammer') 'MineJammer source'
Require-Path (Join-Path $repoRoot 'docs\MINESCAPE_V1_MASTER_PLAN.md') 'canonical plan'

try { Get-PrivateDotnet | Out-Null } catch { $failures.Add($_.Exception.Message) }
try { Get-PrivateJavaHome | Out-Null } catch { $failures.Add($_.Exception.Message) }
try { Get-PrivatePython | Out-Null } catch { $failures.Add($_.Exception.Message) }
try { Get-PrivateGradle | Out-Null } catch { $failures.Add($_.Exception.Message) }

$production = [IO.Path]::GetFullPath((Join-Path $repoRoot 'var\MineScape'))
$staging = [IO.Path]::GetFullPath((Join-Path $repoRoot 'var\MineJammer'))
if ($production -eq $staging) { $failures.Add('MineScape and MineJammer resolve to the same path.') }

if ($Release) {
    $lock = Join-Path $repoRoot 'artifacts\artifacts.lock.json'
    $minecraftServerLock = Join-Path $repoRoot 'artifacts\minecraft-server.lock.json'
    $cache = Join-Path $repoRoot 'artifacts\cache'
    $evidence = Join-Path $repoRoot 'var\release\evidence'
    $eula = Join-Path $repoRoot 'var\MineScape\eula.txt'
    Require-Path $lock 'resolved artifact lock'
    Require-Path $minecraftServerLock 'official Minecraft server lock'
    Require-Path $cache 'downloaded artifact cache'
    Require-Path $evidence 'release evidence directory'
    Require-Path $eula 'owner-controlled production EULA file'

    $compatibilityPolicy = Join-Path $repoRoot 'MineJammer\config\compatibility.policy.json'
    if (Test-Path -LiteralPath $compatibilityPolicy) {
        $compatibility = Get-Content -LiteralPath $compatibilityPolicy -Raw | ConvertFrom-Json
        if ($compatibility.allowlist_reviewed -ne $true) {
            $failures.Add('The exact Matcha/Stardust compatibility allowlist has not been reviewed and approved.')
        }
    }
    if (Test-Path -LiteralPath $eula) {
        $accepted = Get-Content -LiteralPath $eula | Where-Object { $_.Trim().ToLowerInvariant() -eq 'eula=true' }
        if (-not $accepted) { $failures.Add('Minecraft EULA has not been accepted by the owner in var\MineScape\eula.txt.') }
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Preflight failed:' -ForegroundColor Red
    $failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}

if ($Release) {
    Write-Step 'Running MineJammer release-evidence and artifact verification'
    $python = Get-PrivatePython
    $mineJammerRoot = Join-Path $repoRoot 'MineJammer'
    $report = Join-Path $repoRoot 'var\release\preflight.json'
    $pythonCode = "import runpy,sys; sys.path.insert(0,r'''$mineJammerRoot'''); sys.argv=['minejammer']+sys.argv[1:]; runpy.run_module('minejammer',run_name='__main__')"
    & $python -c $pythonCode 'preflight' '--policy' (Join-Path $mineJammerRoot 'config\release-gates.json') '--evidence-dir' (Join-Path $repoRoot 'var\release\evidence') '--lock' (Join-Path $repoRoot 'artifacts\artifacts.lock.json') '--minecraft-server-lock' (Join-Path $repoRoot 'artifacts\minecraft-server.lock.json') '--cache' (Join-Path $repoRoot 'artifacts\cache') '--report' $report
    if ($LASTEXITCODE -ne 0) { throw "MineJammer release preflight failed. Review $report" }

    if (-not $SkipBuild) {
        Write-Step 'Rebuilding every first-party component for release'
        & (Join-Path $repoRoot 'ops\build.ps1') -Release
        if ($LASTEXITCODE -ne 0) { throw 'Release build/test verification failed.' }
    }

    Write-Step 'Verifying runtime bundle bindings and release-only launcher boundary'
    & (Join-Path $repoRoot 'ops\assemble-runtime.ps1') -Target MineScape -ValidateRelease | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Runtime release binding verification failed.' }
}

Write-Host 'Preflight passed.' -ForegroundColor Green
