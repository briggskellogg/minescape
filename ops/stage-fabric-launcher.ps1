#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $SourceJar,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9A-Fa-f]{128}$')]
    [string] $ExpectedSha512
)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
$profilePath = Join-Path $PSScriptRoot 'runtime-profiles.json'
$profile = Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json

if ($profile.minecraft -ne '26.2' -or $profile.fabric_loader -ne '0.19.3' -or $profile.fabric_launcher -ne '1.1.1') {
    throw 'The runtime profile no longer carries the canonical Fabric 26.2 / 0.19.3 / 1.1.1 pin.'
}

$source = (Resolve-Path -LiteralPath $SourceJar).Path
$actual = (Get-FileHash -LiteralPath $source -Algorithm SHA512).Hash.ToLowerInvariant()
$expected = $ExpectedSha512.ToLowerInvariant()
if ($expected -ne ([string]$profile.launcher.sha512).ToLowerInvariant()) {
    throw 'The supplied launcher digest disagrees with the committed canonical launcher pin.'
}
if ($actual -ne $expected) {
    throw "Fabric launcher SHA-512 mismatch. Expected $expected but read $actual. Nothing was staged."
}

$destination = [IO.Path]::GetFullPath((Join-Path $repoRoot $profile.launcher.cache_path))
$sidecar = [IO.Path]::GetFullPath((Join-Path $repoRoot $profile.launcher.sha512_path))
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\cache'))
if (-not $destination.StartsWith($cacheRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Fabric launcher destination escaped the ignored artifact cache.'
}

$destinationDirectory = Split-Path -Parent $destination
New-DirectoryIfMissing $destinationDirectory
if (Test-Path -LiteralPath $destination) {
    $existing = (Get-FileHash -LiteralPath $destination -Algorithm SHA512).Hash.ToLowerInvariant()
    if ($existing -ne $expected) {
        throw 'A different file already occupies the pinned Fabric launcher cache path. It was not overwritten.'
    }
} else {
    $temporary = Join-Path $destinationDirectory ('.fabric-launcher-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        Copy-Item -LiteralPath $source -Destination $temporary
        $copied = (Get-FileHash -LiteralPath $temporary -Algorithm SHA512).Hash.ToLowerInvariant()
        if ($copied -ne $expected) { throw 'The staged Fabric launcher changed during copying.' }
        Move-Item -LiteralPath $temporary -Destination $destination
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

[IO.File]::WriteAllText($sidecar, $expected + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Host 'Verified Fabric launcher staged in the ignored cache.' -ForegroundColor Green
Write-Host "Pin: Minecraft $($profile.minecraft), Loader $($profile.fabric_loader), Launcher $($profile.fabric_launcher)"
Write-Host "SHA-512: $expected"
Write-Host 'This script did not accept the Minecraft EULA or place the launcher in production.'
