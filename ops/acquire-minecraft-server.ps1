#Requires -Version 5.1
[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
$version = '26.2'
$manifestUrl = 'https://piston-meta.mojang.com/mc/game/version_manifest_v2.json'
$manifest = Invoke-RestMethod -Uri $manifestUrl
$entry = $manifest.versions | Where-Object { $_.id -eq $version } | Select-Object -First 1
if (-not $entry -or -not $entry.url) { throw "Official Minecraft manifest does not contain version $version." }
$metadata = Invoke-RestMethod -Uri $entry.url
$server = $metadata.downloads.server
if (-not $server.url -or $server.sha1 -notmatch '^[0-9a-f]{40}$' -or [int64]$server.size -le 0) {
    throw 'Official Minecraft version metadata has no valid pinned server download.'
}

$cache = Join-Path $repoRoot "artifacts\cache\minecraft_server\server-$version.jar"
New-DirectoryIfMissing (Split-Path -Parent $cache)
$needsDownload = -not (Test-Path -LiteralPath $cache -PathType Leaf)
if (-not $needsDownload) {
    $needsDownload = (Get-FileHash -LiteralPath $cache -Algorithm SHA1).Hash.ToLowerInvariant() -ne $server.sha1
}
if ($needsDownload) {
    $partial = "$cache.download"
    try {
        Invoke-WebRequest -Uri $server.url -OutFile $partial
        $actual = (Get-FileHash -LiteralPath $partial -Algorithm SHA1).Hash.ToLowerInvariant()
        if ($actual -ne $server.sha1) { throw "Minecraft server SHA-1 mismatch: expected $($server.sha1), got $actual." }
        Move-Item -LiteralPath $partial -Destination $cache -Force
    } finally {
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
    }
}

$info = Get-Item -LiteralPath $cache
if ($info.Length -ne [int64]$server.size) { throw 'Minecraft server size differs from official metadata.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$innerCache = Join-Path (Split-Path -Parent $cache) "server-$version-inner.jar"
$zip = [IO.Compression.ZipFile]::OpenRead($cache)
try {
    $versionsEntry = $zip.GetEntry('META-INF/versions.list')
    if (-not $versionsEntry) { throw 'Minecraft server bundle has no META-INF/versions.list.' }
    $reader = [IO.StreamReader]::new($versionsEntry.Open(), [Text.Encoding]::UTF8)
    try { $versionLines = $reader.ReadToEnd() -split "`r?`n" } finally { $reader.Dispose() }
    $versionFields = $versionLines | ForEach-Object { ,($_ -split "`t") } | Where-Object { $_.Count -eq 3 -and $_[1] -eq $version } | Select-Object -First 1
    if (-not $versionFields -or $versionFields[0] -notmatch '^[0-9a-f]{64}$') { throw 'Minecraft server bundle has no exact, SHA-256-pinned inner server entry.' }
    $innerPath = 'META-INF/versions/' + $versionFields[2]
    $innerEntry = $zip.GetEntry($innerPath)
    if (-not $innerEntry) { throw "Minecraft server bundle is missing $innerPath." }
    $extract = -not (Test-Path -LiteralPath $innerCache -PathType Leaf)
    if (-not $extract) {
        $extract = (Get-FileHash -LiteralPath $innerCache -Algorithm SHA256).Hash.ToLowerInvariant() -ne $versionFields[0]
    }
    if ($extract) {
        $partialInner = "$innerCache.download"
        try {
            $input = $innerEntry.Open()
            $output = [IO.File]::Create($partialInner)
            try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
            $innerActual = (Get-FileHash -LiteralPath $partialInner -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($innerActual -ne $versionFields[0]) { throw 'Bundled Minecraft server SHA-256 mismatch.' }
            Move-Item -LiteralPath $partialInner -Destination $innerCache -Force
        } finally {
            if (Test-Path -LiteralPath $partialInner) { Remove-Item -LiteralPath $partialInner -Force }
        }
    }
} finally {
    $zip.Dispose()
}
$lock = [ordered]@{
    schema = 'minescape.minecraft-server-lock.v1'
    version = $version
    type = [string]$metadata.type
    version_manifest = $manifestUrl
    version_metadata = [string]$entry.url
    download_url = [string]$server.url
    size = [int64]$server.size
    sha1 = [string]$server.sha1
    sha512 = (Get-FileHash -LiteralPath $cache -Algorithm SHA512).Hash.ToLowerInvariant()
    cache_relative = "artifacts/cache/minecraft_server/server-$version.jar"
    bundled_server_relative = "artifacts/cache/minecraft_server/server-$version-inner.jar"
    bundled_server_sha256 = (Get-FileHash -LiteralPath $innerCache -Algorithm SHA256).Hash.ToLowerInvariant()
    bundled_server_sha512 = (Get-FileHash -LiteralPath $innerCache -Algorithm SHA512).Hash.ToLowerInvariant()
}
$lockPath = Join-Path $repoRoot 'artifacts\minecraft-server.lock.json'
[IO.File]::WriteAllText($lockPath, ($lock | ConvertTo-Json -Depth 5) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Host "Official Minecraft $version server archive acquired and verified." -ForegroundColor Green
Write-Host "Lock: $lockPath"
Write-Host 'This operation did not start Minecraft or accept its EULA.'
