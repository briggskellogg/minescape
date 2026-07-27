#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('All', 'MineScape', 'MineJammer')]
    [string] $Target = 'All',
    [switch] $Apply,
    [switch] $StageMineJammerLauncher,
    [switch] $ValidateRelease,
    [switch] $Release
)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
$profilePath = Join-Path $PSScriptRoot 'runtime-profiles.json'
$lockPath = Join-Path $repoRoot 'artifacts\artifacts.lock.json'
$minecraftServerLockPath = Join-Path $repoRoot 'artifacts\minecraft-server.lock.json'
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\cache'))
$varRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'var'))

function Read-Json([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required JSON is missing: $Path" }
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Get-Sha512([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA512).Hash.ToLowerInvariant()
}

function Get-Sha512Text([string] $Value) {
    $algorithm = [Security.Cryptography.SHA512]::Create()
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
        return ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    } finally {
        $algorithm.Dispose()
    }
}

function Resolve-RepositoryPath([string] $RelativePath) {
    $full = [IO.Path]::GetFullPath((Join-Path $repoRoot $RelativePath))
    if ($full -ne $repoRoot -and -not $full.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Configured path escaped the repository: $RelativePath"
    }
    return $full
}

function Assert-RuntimeRoot([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($varRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Runtime instance escaped the repository var directory: $full"
    }
    return $full
}

function Test-ContainsAny([object[]] $Haystack, [object[]] $Needles) {
    foreach ($needle in $Needles) {
        if ($Haystack -contains $needle) { return $true }
    }
    return $false
}

function Get-LockedSource([object] $Artifact) {
    $key = [string]$Artifact.key
    $filename = [string]$Artifact.filename
    if ($key -notmatch '^[a-z0-9_]+$') { throw "Unsafe artifact key in lock: $key" }
    if ([IO.Path]::GetFileName($filename) -ne $filename) { throw "Unsafe artifact filename in lock: $filename" }
    $expected = [string]$Artifact.sha512
    if ($expected -notmatch '^[0-9A-Fa-f]{128}$') { throw "Artifact $key has no usable SHA-512 pin." }
    $candidate = [IO.Path]::GetFullPath((Join-Path (Join-Path $cacheRoot $key) $filename))
    if (-not $candidate.StartsWith($cacheRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Artifact $key escaped the cache root."
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Locked artifact is absent from cache: $key ($candidate)" }
    if ((Get-Sha512 $candidate) -ne $expected.ToLowerInvariant()) { throw "Locked artifact SHA-512 mismatch: $key" }
    return $candidate
}

function Get-VerifiedLauncher([object] $Profile) {
    $jar = Resolve-RepositoryPath ([string]$Profile.launcher.cache_path)
    $sidecar = Resolve-RepositoryPath ([string]$Profile.launcher.sha512_path)
    if (-not (Test-Path -LiteralPath $jar -PathType Leaf) -or -not (Test-Path -LiteralPath $sidecar -PathType Leaf)) { return $null }
    $expected = (Get-Content -LiteralPath $sidecar -Raw -Encoding UTF8).Trim().ToLowerInvariant()
    if ($expected -notmatch '^[0-9a-f]{128}$') { throw 'The Fabric launcher SHA-512 sidecar is malformed.' }
    if ($expected -ne ([string]$Profile.launcher.sha512).ToLowerInvariant()) { throw 'The Fabric launcher sidecar disagrees with the committed runtime pin.' }
    if ((Get-Sha512 $jar) -ne $expected) { throw 'The cached Fabric launcher does not match its explicit SHA-512 pin.' }
    return [pscustomobject]@{ Path = $jar; Sha512 = $expected }
}

function Install-ManagedFile(
    [string] $Source,
    [string] $Destination,
    [string] $ExpectedSha512,
    [System.Collections.Generic.List[object]] $ManifestFiles
) {
    $expected = $ExpectedSha512.ToLowerInvariant()
    if (Test-Path -LiteralPath $Destination) {
        if (-not (Test-Path -LiteralPath $Destination -PathType Leaf)) { throw "Managed destination is not a file: $Destination" }
        if ((Get-Sha512 $Destination) -ne $expected) { throw "Refusing to overwrite a different runtime file: $Destination" }
        $action = 'verified-existing'
    } elseif ($Apply) {
        New-DirectoryIfMissing (Split-Path -Parent $Destination)
        Copy-Item -LiteralPath $Source -Destination $Destination
        if ((Get-Sha512 $Destination) -ne $expected) { throw "Runtime copy verification failed: $Destination" }
        $action = 'installed'
    } else {
        $action = 'would-install'
    }
    $ManifestFiles.Add([ordered]@{
        path = $Destination.Substring($repoRoot.Length + 1).Replace('\', '/')
        sha512 = $expected
        action = $action
    })
}

function Install-NewTextFile([string] $Path, [string] $Content, [switch] $RequireExact) {
    if (Test-Path -LiteralPath $Path) {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Runtime configuration destination is not a file: $Path" }
        $existing = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
        if ($RequireExact -and $existing.Replace("`r`n", "`n") -ne $Content.Replace("`r`n", "`n")) {
            throw "Existing pinned runtime configuration differs; it was not overwritten: $Path"
        }
        return 'preserved-existing'
    }
    if ($Apply) {
        New-DirectoryIfMissing (Split-Path -Parent $Path)
        [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
        return 'created'
    }
    return 'would-create'
}

function Assert-JsonArray([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return }
    $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    try { $null = $raw | ConvertFrom-Json } catch { throw "Invalid JSON was preserved at $Path" }
    $trimmed = $raw.Trim()
    if (-not $trimmed.StartsWith('[') -or -not $trimmed.EndsWith(']')) { throw "Expected a JSON array at $Path" }
}

function New-ServerProperties([object] $Instance, [string] $Seed) {
    $lines = @(
        '# MineScape managed security and world-identity baseline. Reconcile changes through the assembly pipeline.',
        'accept-transfers=false',
        'allow-flight=false',
        'allow-nether=true',
        'broadcast-console-to-ops=true',
        'broadcast-rcon-to-ops=false',
        'difficulty=easy',
        'enable-command-block=false',
        'enable-jmx-monitoring=false',
        'enable-query=false',
        'enable-rcon=false',
        'enable-status=true',
        'enforce-secure-profile=true',
        'enforce-whitelist=true',
        'force-gamemode=true',
        'function-permission-level=2',
        'gamemode=survival',
        'generate-structures=true',
        'generator-settings={}',
        'hardcore=false',
        'hide-online-players=false',
        'level-name=world',
        "level-seed=$Seed",
        'level-type=minecraft\:normal',
        'log-ips=false',
        'max-players=8',
        'max-tick-time=60000',
        'max-world-size=29999984',
        "motd=$($Instance.motd)",
        'network-compression-threshold=256',
        'online-mode=true',
        'op-permission-level=4',
        'pause-when-empty-seconds=60',
        'player-idle-timeout=0',
        'prevent-proxy-connections=false',
        'pvp=true',
        ('query.port=' + [string]$Instance.port),
        'rate-limit=0',
        'rcon.password=',
        'rcon.port=25575',
        'region-file-compression=deflate',
        'require-resource-pack=false',
        'resource-pack=',
        'resource-pack-id=',
        'resource-pack-prompt=',
        'resource-pack-sha1=',
        'server-ip=',
        ('server-port=' + [string]$Instance.port),
        'simulation-distance=10',
        'spawn-monsters=true',
        'spawn-protection=0',
        'sync-chunk-writes=true',
        'use-native-transport=true',
        'view-distance=12',
        'white-list=true'
    )
    return ($lines -join "`n") + "`n"
}

function New-BridgeProperties([object] $Instance) {
    return (@(
        '# MineScape Bridge is loopback-only. The first server start creates bridge.token beside this file.',
        ('instance.mode=' + [string]$Instance.mode),
        'bridge.bind=127.0.0.1',
        ('bridge.port=' + [string]$Instance.bridge_port),
        'bridge.max_request_bytes=16384'
    ) -join "`n") + "`n"
}

function New-WorldBorderConfig() {
    return (@(
        '{',
        '  "enableCustomOverworldBorder": true,',
        '  "enableCustomNetherBorder": true,',
        '  "enableCustomEndBorder": true,',
        '  "shouldLoopToOppositeBorder": false,',
        '  "distanceTeleportedBack": 32,',
        '  "nearBorderMessage": "The edge of this finite world is close.",',
        '  "hitBorderMessage": "You reached the world edge and were moved safely inward.",',
        '  "loopBorderMessage": "World-edge looping is disabled.",',
        '  "overworldBorderPositiveX": 16384,',
        '  "overworldBorderNegativeX": -16384,',
        '  "overworldBorderPositiveZ": 16384,',
        '  "overworldBorderNegativeZ": -16384,',
        '  "netherBorderPositiveX": 2048,',
        '  "netherBorderNegativeX": -2048,',
        '  "netherBorderPositiveZ": 2048,',
        '  "netherBorderNegativeZ": -2048,',
        '  "endBorderPositiveX": 8192,',
        '  "endBorderNegativeX": -8192,',
        '  "endBorderPositiveZ": 8192,',
        '  "endBorderNegativeZ": -8192',
        '}'
    ) -join "`n") + "`n"
}

function Assert-DatapackOrderProfile([object] $Profile, [object] $Lock) {
    $entries = @($Profile.datapack_order_low_to_high)
    if ($entries.Count -lt 1) { throw 'The runtime profile declares no datapack order.' }
    $destinations = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $artifactKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $generatedPaths = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $entries) {
        $destination = [string]$entry.destination
        if ([IO.Path]::GetFileName($destination) -ne $destination -or $destination -notmatch '^\d{2}-[^\\/]+\.zip$') {
            throw "Datapack order has an unsafe or unnumbered destination: $destination"
        }
        if (-not $destinations.Add($destination)) { throw "Datapack order repeats destination: $destination" }
        if ($entry.kind -eq 'artifact') {
            $key = [string]$entry.key
            if (-not $artifactKeys.Add($key)) { throw "Datapack order repeats artifact: $key" }
            $artifact = @($Lock.artifacts | Where-Object { $_.key -eq $key })
            if ($artifact.Count -ne 1 -or @($artifact[0].profiles) -notcontains 'world-datapack') {
                throw "Datapack order references a non-world artifact: $key"
            }
        } elseif ($entry.kind -eq 'generated') {
            $path = [string]$entry.path
            if ([string]::IsNullOrWhiteSpace($path) -or [string]::IsNullOrWhiteSpace([string]$entry.evidence) -or
                [string]::IsNullOrWhiteSpace([string]$entry.evidence_schema)) {
                throw "Generated datapack order entry is incomplete: $path"
            }
            $generatedPaths.Add($path)
        } else {
            throw "Unknown datapack order kind: $($entry.kind)"
        }
    }
    $declaredRelease = @($Profile.release.generated_datapacks)
    if (($generatedPaths -join "`n") -ne ($declaredRelease -join "`n")) {
        throw 'Generated datapack release list must exactly match declared low-to-high order.'
    }
}

function New-ServerBundleLock(
    [string] $InstanceName,
    [string] $InstanceRoot,
    [object] $Profile,
    [System.Collections.Generic.List[object]] $ManifestFiles,
    [System.Collections.Generic.List[object]] $Datapacks,
    [string] $ServerProperties,
    [string] $BridgeProperties,
    [string] $WorldBorderConfig
) {
    $files = @($ManifestFiles | Where-Object { $_.path -notmatch '/fabric-server-launch\.jar$' } | ForEach-Object {
        $absolute = [IO.Path]::GetFullPath((Join-Path $repoRoot (($_.path -replace '/', '\'))))
        if (-not $absolute.StartsWith($InstanceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Bundle file escaped $InstanceName runtime: $($_.path)"
        }
        [pscustomobject][ordered]@{
            path = $absolute.Substring($InstanceRoot.Length + 1).Replace('\', '/')
            sha512 = [string]$_.sha512
        }
    } | Sort-Object path)
    $document = [ordered]@{
        schema = 'minescape.server-bundle-lock.v1'
        instance = $InstanceName
        minecraft = [string]$Profile.minecraft
        fabric_loader = [string]$Profile.fabric_loader
        fabric_launcher = [string]$Profile.fabric_launcher
        fabric_launcher_sha512 = ([string]$Profile.launcher.sha512).ToLowerInvariant()
        seed = [string]$Profile.seed
        server_properties_sha512 = Get-Sha512Text $ServerProperties
        bridge_properties_sha512 = Get-Sha512Text $BridgeProperties
        world_border_config_sha512 = Get-Sha512Text $WorldBorderConfig
        datapacks_low_to_high = @($Datapacks)
        files = $files
    }
    $content = ($document | ConvertTo-Json -Depth 9 -Compress) + "`n"
    return [pscustomobject]@{ Document = $document; Content = $content; Sha512 = Get-Sha512Text $content }
}

function Get-ReleaseBlockers([object] $Profile, [object] $Lock, [object] $Launcher) {
    $blockers = [System.Collections.Generic.List[string]]::new()
    foreach ($artifact in @($Lock.artifacts)) {
        try { Get-LockedSource $artifact | Out-Null } catch { $blockers.Add($_.Exception.Message) }
    }
    if (-not $Launcher) { $blockers.Add('Fabric launcher has not been staged with an explicit SHA-512 pin.') }

    $policyPath = Resolve-RepositoryPath ([string]$Profile.release.gate_policy)
    $evidenceRoot = Resolve-RepositoryPath ([string]$Profile.release.evidence_directory)
    $policy = Read-Json $policyPath
    if ($policy.schema -ne 'minescape.release-gates.v2') {
        $blockers.Add('Release gate policy is not the hardened v2 schema.')
    }

    $python = Get-PrivatePython
    $mineJammerRoot = Join-Path $repoRoot 'MineJammer'
    $pythonCode = "import runpy,sys; sys.path.insert(0,r'''$mineJammerRoot'''); sys.argv=['minejammer']+sys.argv[1:]; runpy.run_module('minejammer',run_name='__main__')"
    $savedErrorPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $preflightOutput = @(& $python -c $pythonCode 'preflight' '--policy' $policyPath '--evidence-dir' $evidenceRoot '--lock' $lockPath '--minecraft-server-lock' $minecraftServerLockPath '--cache' $cacheRoot 2>&1)
        $preflightExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $savedErrorPreference
    }
    $preflight = $null
    try { $preflight = ($preflightOutput -join "`n") | ConvertFrom-Json } catch { }
    if ($preflightExit -ne 0 -or -not $preflight -or $preflight.schema -ne 'minescape.release-preflight.v2' -or $preflight.passed -ne $true) {
        if ($preflight -and $preflight.checks) {
            foreach ($check in @($preflight.checks | Where-Object { $_.passed -ne $true })) {
                $blockers.Add("Hardened release gate failed: $($check.gate) - $($check.detail)")
            }
        } else {
            $detail = ($preflightOutput -join ' ').Trim()
            if ($detail.Length -gt 300) { $detail = $detail.Substring(0, 300) + '...' }
            $blockers.Add("Hardened MineJammer preflight did not pass: $detail")
        }
    }

    $bundleLockPath = Join-Path (Assert-RuntimeRoot (Resolve-RepositoryPath ([string]$Profile.instances.MineScape.root))) 'server-bundle.lock.json'
    $bundleHash = $null
    if (-not (Test-Path -LiteralPath $bundleLockPath -PathType Leaf)) {
        $blockers.Add('Production server-bundle.lock.json is missing; assemble the inert production skeleton first.')
    } else {
        $bundleHash = Get-Sha512 $bundleLockPath
        try {
            $bundle = Read-Json $bundleLockPath
            if ($bundle.schema -ne 'minescape.server-bundle-lock.v1' -or $bundle.instance -ne 'MineScape') {
                $blockers.Add('Production server bundle lock has an invalid identity.')
            }
            if (($bundle.fabric_launcher_sha512).ToLowerInvariant() -ne ([string]$Profile.launcher.sha512).ToLowerInvariant() -or
                [string]$bundle.seed -ne [string]$Profile.seed) {
                $blockers.Add('Production server bundle lock disagrees with launcher or seed pins.')
            }
            $productionRoot = Split-Path -Parent $bundleLockPath
            foreach ($file in @($bundle.files)) {
                $candidate = [IO.Path]::GetFullPath((Join-Path $productionRoot (($file.path -replace '/', '\'))))
                if (-not $candidate.StartsWith($productionRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                    -not (Test-Path -LiteralPath $candidate -PathType Leaf) -or
                    (Get-Sha512 $candidate) -ne ([string]$file.sha512).ToLowerInvariant()) {
                    $blockers.Add("Production server bundle file is absent or changed: $($file.path)")
                }
            }
            $serverPropertiesPath = Join-Path $productionRoot 'server.properties'
            $bridgePropertiesPath = Join-Path $productionRoot 'config\minescape\bridge.properties'
            $worldBorderConfigPath = Join-Path $productionRoot 'config\worldborder.json5'
            if (-not (Test-Path -LiteralPath $serverPropertiesPath -PathType Leaf) -or
                (Get-Sha512 $serverPropertiesPath) -ne ([string]$bundle.server_properties_sha512).ToLowerInvariant()) {
                $blockers.Add('Production server.properties changed after bundle locking.')
            }
            if (-not (Test-Path -LiteralPath $bridgePropertiesPath -PathType Leaf) -or
                (Get-Sha512 $bridgePropertiesPath) -ne ([string]$bundle.bridge_properties_sha512).ToLowerInvariant()) {
                $blockers.Add('Production Bridge properties changed after bundle locking.')
            }
            if (-not (Test-Path -LiteralPath $worldBorderConfigPath -PathType Leaf) -or
                (Get-Sha512 $worldBorderConfigPath) -ne ([string]$bundle.world_border_config_sha512).ToLowerInvariant()) {
                $blockers.Add('Production World Border configuration changed after bundle locking.')
            }
            $expectedBundleOrder = @($Profile.datapack_order_low_to_high | ForEach-Object { [string]$_.destination })
            $actualBundleOrder = @($bundle.datapacks_low_to_high | ForEach-Object { [string]$_.destination })
            if (($expectedBundleOrder -join "`n") -ne ($actualBundleOrder -join "`n")) {
                $blockers.Add('Production bundle does not carry the committed datapack priority order.')
            }
            $assemblyStatusPath = Join-Path $productionRoot 'assembly-status.json'
            if (-not (Test-Path -LiteralPath $assemblyStatusPath -PathType Leaf) -or
                ([string](Read-Json $assemblyStatusPath).server_bundle_sha512).ToLowerInvariant() -ne $bundleHash) {
                $blockers.Add('Production assembly status is not bound to the current server bundle lock.')
            }
        } catch { $blockers.Add('Production server bundle lock is unreadable.') }
    }

    foreach ($entry in @($Profile.datapack_order_low_to_high | Where-Object { $_.kind -eq 'generated' })) {
        $generated = Resolve-RepositoryPath ([string]$entry.path)
        if (-not (Test-Path -LiteralPath $generated -PathType Leaf)) {
            $blockers.Add("Generated release datapack missing: $($entry.path)")
            continue
        }
        $evidencePath = Join-Path $evidenceRoot ([string]$entry.evidence)
        if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) { continue }
        try {
            $evidence = Read-Json $evidencePath
            if ($evidence.schema -ne $entry.evidence_schema -or
                ([string]$evidence.output_sha512).ToLowerInvariant() -ne (Get-Sha512 $generated)) {
                $blockers.Add("Generated datapack does not match hardened evidence: $($entry.path)")
            }
        } catch { $blockers.Add("Generated datapack evidence is unreadable: $($entry.evidence)") }
    }

    if ($bundleHash) {
        foreach ($gate in @($policy.gates | Where-Object { $_.evaluator -in @('manual', 'seed-audit') })) {
            $evidencePath = Join-Path $evidenceRoot ([string]$gate.evidence)
            if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf)) { continue }
            try {
                $evidence = Read-Json $evidencePath
                if (-not $evidence.input_hashes -or
                    ([string]$evidence.input_hashes.server_bundle_sha512).ToLowerInvariant() -ne $bundleHash) {
                    $blockers.Add("Release evidence was not recorded against this server bundle: $($gate.id)")
                }
            } catch { $blockers.Add("Release bundle binding is unreadable: $($gate.id)") }
        }
    }

    $backupMarker = Resolve-RepositoryPath ([string]$Profile.release.backup_marker)
    $recoveryEvidence = Join-Path $evidenceRoot 'recovery.json'
    $expectedBackupMarker = if (Test-Path -LiteralPath $recoveryEvidence -PathType Leaf) { 'recovery-evidence-sha512=' + (Get-Sha512 $recoveryEvidence) } else { $null }
    if (-not $expectedBackupMarker -or -not (Test-Path -LiteralPath $backupMarker -PathType Leaf) -or
        (Get-Content -LiteralPath $backupMarker -Raw -Encoding UTF8).Trim().ToLowerInvariant() -ne $expectedBackupMarker) {
        $blockers.Add('Independent backup marker is missing or is not bound to the current recovery evidence SHA-512.')
    }
    $eulaMarker = Resolve-RepositoryPath ([string]$Profile.release.eula_marker)
    if (-not (Test-Path -LiteralPath $eulaMarker -PathType Leaf) -or
        (Get-Content -LiteralPath $eulaMarker -Raw -Encoding UTF8).Trim().ToLowerInvariant() -ne 'minecraft-eula-owner-accepted=true') {
        $blockers.Add('Owner EULA marker is missing or does not contain the explicit acceptance statement.')
    }
    $productionRoot = Assert-RuntimeRoot (Resolve-RepositoryPath ([string]$Profile.instances.MineScape.root))
    $productionEula = Join-Path $productionRoot 'eula.txt'
    if (-not (Test-Path -LiteralPath $productionEula -PathType Leaf) -or
        (Get-Content -LiteralPath $productionEula -Raw -Encoding UTF8) -notmatch '(?m)^eula=true\s*$') {
        $blockers.Add('Production eula.txt does not contain the owner-written eula=true choice.')
    }
    return $blockers
}

$profile = Read-Json $profilePath
$lock = Read-Json $lockPath
if ($profile.schema -ne 'minescape.runtime-profiles.v1') { throw 'Unsupported runtime profile schema.' }
if ($lock.schema -ne 'minescape.artifacts.lock.v1') { throw 'Unsupported artifact lock schema.' }
foreach ($pin in @('minecraft', 'fabric_loader', 'fabric_launcher')) {
    if ([string]$profile.$pin -ne [string]$lock.$pin) { throw "Runtime profile and artifact lock disagree on $pin." }
}
if ([string]$profile.seed -ne '6246468738900744') { throw 'The canonical seed pin changed unexpectedly.' }
if ([string]$profile.launcher.sha512 -notmatch '^[0-9a-f]{128}$') { throw 'The committed Fabric launcher SHA-512 pin is invalid.' }
if ([string]$profile.instances.MineScape.mode -ne 'family' -or [string]$profile.instances.MineJammer.mode -ne 'heart_qa') {
    throw 'Runtime instance modes must remain family for MineScape and heart_qa for MineJammer.'
}
Assert-DatapackOrderProfile $profile $lock

$firstParty = Resolve-RepositoryPath ([string]$profile.first_party_jar)
if (-not (Test-Path -LiteralPath $firstParty -PathType Leaf)) { throw "Built MineScape jar is missing: $firstParty. Run ops\build.ps1 first." }
$firstPartyHash = Get-Sha512 $firstParty
$launcher = Get-VerifiedLauncher $profile
$javaHome = Get-PrivateJavaHome
$javaExecutable = Join-Path $javaHome 'bin\javaw.exe'
if (-not (Test-Path -LiteralPath $javaExecutable -PathType Leaf)) { throw "Private Java GUI executable is missing: $javaExecutable" }

if ($Release -and $ValidateRelease) { throw '-Release and -ValidateRelease are mutually exclusive.' }
if ($Release -and -not $Apply) { throw '-Release requires -Apply. Use -ValidateRelease for a read-only gate check.' }
if ($ValidateRelease -and $Apply) { throw '-ValidateRelease is read-only and cannot be combined with -Apply.' }
$releaseGateRequested = $Release -or $ValidateRelease
if ($releaseGateRequested) {
    if ($Target -eq 'MineJammer') { throw 'Release validation/assembly requires MineScape as a selected target.' }
    $releaseBlockers = @(Get-ReleaseBlockers $profile $lock $launcher)
    if ($releaseBlockers.Count -gt 0) {
        Write-Host 'Release assembly refused:' -ForegroundColor Red
        $releaseBlockers | ForEach-Object { Write-Host " - $_" }
        throw 'Release gates are closed. No release mutation was attempted.'
    }
}
if ($StageMineJammerLauncher -and -not $launcher) {
    throw "MineJammer launcher staging requested, but no verified launcher is available. Download only from $($profile.launcher.official_url), establish its SHA-512 independently, then run ops\stage-fabric-launcher.ps1."
}
if ($StageMineJammerLauncher -and $Target -eq 'MineScape') {
    throw '-StageMineJammerLauncher requires MineJammer or All as the target.'
}

$names = if ($Target -eq 'All') { @('MineScape', 'MineJammer') } else { @($Target) }
$summary = [System.Collections.Generic.List[object]]::new()
foreach ($name in $names) {
    $instance = $profile.instances.$name
    if (-not $instance) { throw "Runtime profile is missing instance: $name" }
    $instanceRoot = Assert-RuntimeRoot (Resolve-RepositoryPath ([string]$instance.root))
    $otherName = if ($name -eq 'MineScape') { 'MineJammer' } else { 'MineScape' }
    $otherRoot = Assert-RuntimeRoot (Resolve-RepositoryPath ([string]$profile.instances.$otherName.root))
    if ($instanceRoot -eq $otherRoot) { throw 'MineScape and MineJammer cannot share a writable root.' }
    if (Test-Path -LiteralPath (Join-Path $instanceRoot 'world\level.dat')) { throw "Refusing to assemble over an initialized world: $instanceRoot" }
    if ($name -eq 'MineScape' -and -not $releaseGateRequested) {
        if (Test-Path -LiteralPath (Join-Path $instanceRoot 'fabric-server-launch.jar')) {
            throw 'Production launcher is present while release gates are closed. MineScape must remain inert.'
        }
        $existingEula = Join-Path $instanceRoot 'eula.txt'
        if ((Test-Path -LiteralPath $existingEula -PathType Leaf) -and
            (Get-Content -LiteralPath $existingEula -Raw -Encoding UTF8) -match '(?m)^eula=true\s*$') {
            throw 'Production EULA is already accepted while release gates are closed. MineScape must remain inert.'
        }
    }

    $lockedSelections = [System.Collections.Generic.List[object]]::new()
    foreach ($artifact in @($lock.artifacts)) {
        $artifactProfiles = @($artifact.profiles)
        $isMod = (Test-ContainsAny $artifactProfiles @($instance.mod_profiles)) -and (@($instance.mod_statuses) -contains $artifact.status)
        $isDatapack = (Test-ContainsAny $artifactProfiles @($instance.datapack_profiles)) -and (@($instance.datapack_statuses) -contains $artifact.status)
        if ($isMod -and $isDatapack) { throw "Artifact is ambiguously both a server mod and world datapack: $($artifact.key)" }
        if ($isMod -or $isDatapack) {
            $source = Get-LockedSource $artifact
            $lockedSelections.Add([pscustomobject]@{ Artifact = $artifact; Source = $source; Kind = $(if ($isMod) { 'mod' } else { 'datapack' }) })
        }
    }

    $manifestFiles = [System.Collections.Generic.List[object]]::new()
    Install-ManagedFile $firstParty (Join-Path $instanceRoot 'mods\minescape-0.1.0.jar') $firstPartyHash $manifestFiles
    foreach ($selection in @($lockedSelections | Where-Object { $_.Kind -eq 'mod' })) {
        $destination = Join-Path (Join-Path $instanceRoot 'mods') ([string]$selection.Artifact.filename)
        Install-ManagedFile ([string]$selection.Source) $destination ([string]$selection.Artifact.sha512) $manifestFiles
    }

    $selectedDatapacks = @($lockedSelections | Where-Object { $_.Kind -eq 'datapack' })
    $declaredArtifactKeys = @($profile.datapack_order_low_to_high | Where-Object { $_.kind -eq 'artifact' } | ForEach-Object { [string]$_.key })
    $selectedArtifactKeys = @($selectedDatapacks | ForEach-Object { [string]$_.Artifact.key })
    if ((@($declaredArtifactKeys | Sort-Object) -join "`n") -ne (@($selectedArtifactKeys | Sort-Object) -join "`n")) {
        throw "$name locked world-datapack profile differs from the declared priority contract."
    }

    $datapackDirectory = Join-Path $instanceRoot 'world\datapacks'
    $allowedDatapackNames = @($profile.datapack_order_low_to_high | ForEach-Object { [string]$_.destination })
    if (Test-Path -LiteralPath $datapackDirectory -PathType Container) {
        foreach ($existing in Get-ChildItem -LiteralPath $datapackDirectory -Force) {
            if ($allowedDatapackNames -notcontains $existing.Name) {
                throw "Refusing an undeclared or stale datapack in $name runtime: $($existing.FullName)"
            }
        }
    }

    $generatedMissing = [System.Collections.Generic.List[string]]::new()
    $datapackManifest = [System.Collections.Generic.List[object]]::new()
    $ordinal = 0
    foreach ($entry in @($profile.datapack_order_low_to_high)) {
        $destination = Join-Path $datapackDirectory ([string]$entry.destination)
        if ($entry.kind -eq 'artifact') {
            $selection = @($selectedDatapacks | Where-Object { $_.Artifact.key -eq $entry.key })
            if ($selection.Count -ne 1) { throw "Ordered datapack artifact did not resolve exactly once: $($entry.key)" }
            $source = [string]$selection[0].Source
            $sha512 = ([string]$selection[0].Artifact.sha512).ToLowerInvariant()
            $sourceId = [string]$entry.key
        } else {
            $source = Resolve-RepositoryPath ([string]$entry.path)
            if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
                $generatedMissing.Add([string]$entry.path)
                $ordinal++
                continue
            }
            $sha512 = Get-Sha512 $source
            $sourceId = [string]$entry.path
        }
        Install-ManagedFile $source $destination $sha512 $manifestFiles
        $datapackManifest.Add([ordered]@{
            ordinal = $ordinal
            priority = $ordinal * 10
            kind = [string]$entry.kind
            source = $sourceId
            destination = [string]$entry.destination
            sha512 = $sha512
        })
        $ordinal++
    }

    $serverProperties = New-ServerProperties $instance ([string]$profile.seed)
    $bridgeProperties = New-BridgeProperties $instance
    $worldBorderConfig = New-WorldBorderConfig
    $propertiesAction = Install-NewTextFile (Join-Path $instanceRoot 'server.properties') $serverProperties -RequireExact
    $bridgeAction = Install-NewTextFile (Join-Path $instanceRoot 'config\minescape\bridge.properties') $bridgeProperties -RequireExact
    $worldBorderPath = Join-Path $instanceRoot 'config\worldborder.json5'
    $worldBorderAction = Install-NewTextFile $worldBorderPath $worldBorderConfig -RequireExact
    $manifestFiles.Add([ordered]@{
        path = $worldBorderPath.Substring($repoRoot.Length + 1).Replace('\', '/')
        sha512 = Get-Sha512Text $worldBorderConfig
        action = $worldBorderAction
    })
    $eulaAction = Install-NewTextFile (Join-Path $instanceRoot 'eula.txt') "# The owner must read Mojang's EULA and change this manually. MineScape never accepts it.`neula=false`n"
    $whitelistPath = Join-Path $instanceRoot 'whitelist.json'
    $whitelistAction = Install-NewTextFile $whitelistPath "[]`n"
    $opsPath = Join-Path $instanceRoot 'ops.json'
    $opsAction = Install-NewTextFile $opsPath "[]`n"
    if ($Apply) { Assert-JsonArray $whitelistPath; Assert-JsonArray $opsPath }
    $markerAction = Install-NewTextFile (Join-Path $instanceRoot '.minescape-runtime-root') "minescape.runtime-root.v1`ninstance=$name`n"

    $launcherAction = 'withheld'
    $runtimeLauncher = Join-Path $instanceRoot 'fabric-server-launch.jar'
    if ($name -eq 'MineScape' -and $Release) {
        Install-ManagedFile ([string]$launcher.Path) (Join-Path $instanceRoot 'fabric-server-launch.jar') ([string]$launcher.Sha512) $manifestFiles
        $launcherAction = 'verified-or-installed'
    } elseif ($name -eq 'MineJammer' -and ((Test-Path -LiteralPath $runtimeLauncher -PathType Leaf) -or $StageMineJammerLauncher)) {
        if (-not $launcher) { throw 'MineJammer launcher exists or was requested without a verified canonical cache source.' }
        Install-ManagedFile ([string]$launcher.Path) $runtimeLauncher ([string]$launcher.Sha512) $manifestFiles
        $launcherAction = 'verified-lab-only'
    }

    $bundle = New-ServerBundleLock $name $instanceRoot $profile $manifestFiles $datapackManifest $serverProperties $bridgeProperties $worldBorderConfig
    if ($Apply) {
        [IO.File]::WriteAllText((Join-Path $instanceRoot 'server-bundle.lock.json'), $bundle.Content, [Text.UTF8Encoding]::new($false))
    }

    $status = [ordered]@{
        schema = 'minescape.runtime-assembly.v1'
        instance = $name
        mode = if ($Release -and $name -eq 'MineScape') { 'release-assembly' } else { 'laboratory-skeleton' }
        launch_permitted = [bool]($Release -and $name -eq 'MineScape' -and $launcherAction -ne 'withheld')
        minecraft = [string]$profile.minecraft
        fabric_loader = [string]$profile.fabric_loader
        fabric_launcher = [string]$profile.fabric_launcher
        seed = [string]$profile.seed
        online_mode = $true
        whitelist = $true
        eula_accepted_by_automation = $false
        launcher = $launcherAction
        server_bundle_sha512 = $bundle.Sha512
        datapack_order_contract = [ordered]@{
            direction = 'lowest-to-highest'
            entries = @($datapackManifest)
            activation_evidence_required = $true
        }
        generated_datapacks_missing = @($generatedMissing)
        operator_gates = @(
            'Owner must review and manually accept Mojang EULA before any server start.',
            'MineDeck must use the private Java 25 executable and these exact var instance roots.',
            'After first start, MineDeck must read the local Bridge token; the token never enters Git or a shortcut.',
            'MineDeck currently routes only the production Bridge; clean MineJammer stop remains gated until its separate 8766 Bridge channel is implemented.',
            'After first lab boot, observed datapack output must prove the committed lowest-to-highest order with MineScape Compatibility highest.',
            'An operator-observed server ping, backup restore, seed, controller, and gameplay audit remain mandatory.'
        )
        files = @($manifestFiles)
    }
    if ($Apply) {
        New-DirectoryIfMissing $instanceRoot
        [IO.File]::WriteAllText((Join-Path $instanceRoot 'assembly-status.json'), ($status | ConvertTo-Json -Depth 8) + "`n", [Text.UTF8Encoding]::new($false))
        $notice = if ($status.launch_permitted) {
            "Release assembly gates passed. Perform an operator-observed startup and health check before family use.`n"
        } else {
            "NOT RELEASED. This is an inert V0/laboratory skeleton. Do not admit family players.`n"
        }
        [IO.File]::WriteAllText((Join-Path $instanceRoot 'RUNTIME-NOTICE.txt'), $notice, [Text.UTF8Encoding]::new($false))
    }
    $summary.Add([ordered]@{
        instance = $name
        root = $instanceRoot
        port = [int]$instance.port
        bridge_port = [int]$instance.bridge_port
        mods = @($lockedSelections | Where-Object { $_.Kind -eq 'mod' }).Count + 1
        datapacks = $datapackManifest.Count
        launcher = $launcherAction
        server_bundle_sha512 = $bundle.Sha512
        server_properties = $propertiesAction
        bridge = $bridgeAction
        world_border = $worldBorderAction
        eula = $eulaAction
        whitelist = $whitelistAction
        ops = $opsAction
        root_marker = $markerAction
        generated_datapacks_missing = @($generatedMissing)
    })
}

$layout = [ordered]@{
    schema = 'minescape.runtime-layout.v1'
    java_executable = $javaExecutable
    production_root = Assert-RuntimeRoot (Resolve-RepositoryPath ([string]$profile.instances.MineScape.root))
    production_port = [int]$profile.instances.MineScape.port
    production_bridge = "http://127.0.0.1:$($profile.instances.MineScape.bridge_port)"
    production_bridge_token = Join-Path (Assert-RuntimeRoot (Resolve-RepositoryPath ([string]$profile.instances.MineScape.root))) 'config\minescape\bridge.token'
    minejammer_root = Assert-RuntimeRoot (Resolve-RepositoryPath ([string]$profile.instances.MineJammer.root))
    minejammer_port = [int]$profile.instances.MineJammer.port
    minejammer_bridge = "http://127.0.0.1:$($profile.instances.MineJammer.bridge_port)"
    minejammer_bridge_token = Join-Path (Assert-RuntimeRoot (Resolve-RepositoryPath ([string]$profile.instances.MineJammer.root))) 'config\minescape\bridge.token'
    server_arguments = '-jar fabric-server-launch.jar nogui'
    note = 'This is an input for MineDeck configuration, not a credential file. Bridge tokens are generated locally at first start.'
}
if ($Apply) {
    New-DirectoryIfMissing $varRoot
    [IO.File]::WriteAllText((Join-Path $varRoot 'runtime-layout.json'), ($layout | ConvertTo-Json -Depth 5) + "`n", [Text.UTF8Encoding]::new($false))
}

$result = [ordered]@{
    schema = 'minescape.runtime-assembly-plan.v1'
    applied = [bool]$Apply
    release_requested = [bool]$Release
    release_validation_requested = [bool]$ValidateRelease
    launcher_cache_verified = [bool]$launcher
    fabric_launcher_official_url = [string]$profile.launcher.official_url
    layout = $layout
    instances = @($summary)
}
$result | ConvertTo-Json -Depth 8
if (-not $Apply) { Write-Host 'Dry run only; no runtime files were changed.' -ForegroundColor Yellow }
