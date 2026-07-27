[CmdletBinding()]
param([switch] $MetadataOnly)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
$python = Get-PrivatePython

$cache = Join-Path $repoRoot 'artifacts\cache'
$lock = Join-Path $repoRoot 'artifacts\artifacts.lock.json'
$report = Join-Path $repoRoot 'artifacts\acquisition-report.json'
New-DirectoryIfMissing $cache
$arguments = @(
    'acquire',
    (Join-Path $repoRoot 'MineJammer\manifests\artifacts.json'),
    '--cache', $cache,
    '--lock', $lock,
    '--report', $report
)
if ($MetadataOnly) { $arguments += '--metadata-only' }
$mineJammerRoot = Join-Path $repoRoot 'MineJammer'
$pythonCode = "import runpy, sys; sys.path.insert(0, r'''$mineJammerRoot'''); sys.argv=['minejammer']+sys.argv[1:]; runpy.run_module('minejammer', run_name='__main__')"
& $python -c $pythonCode @arguments
if ($LASTEXITCODE -ne 0) { throw 'Artifact acquisition failed.' }

if ($MetadataOnly) {
    Write-Host 'Pinned artifact metadata was verified; no third-party binaries were downloaded.' -ForegroundColor Green
} else {
    Write-Host 'Third-party artifacts were downloaded locally and remain excluded from Git.' -ForegroundColor Green
}
