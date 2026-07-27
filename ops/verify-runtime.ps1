#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('All', 'MineScape', 'MineJammer')]
    [string] $Target = 'All',
    [switch] $RequireInert
)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
$profile = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-profiles.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$varRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'var'))
$names = if ($Target -eq 'All') { @('MineScape', 'MineJammer') } else { @($Target) }
$failures = [System.Collections.Generic.List[string]]::new()
if ([string]$profile.instances.MineScape.mode -ne 'family' -or [string]$profile.instances.MineJammer.mode -ne 'heart_qa') {
    $failures.Add('Runtime instance modes differ from the family/heart_qa safety contract')
}

function Read-Properties([string] $Path) {
    $result = @{}
    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }
        $separator = $trimmed.IndexOf('=')
        if ($separator -lt 1) { throw "Malformed property in ${Path}: $line" }
        $key = $trimmed.Substring(0, $separator)
        if ($result.ContainsKey($key)) { throw "Duplicate property in ${Path}: $key" }
        $result[$key] = $trimmed.Substring($separator + 1)
    }
    return $result
}

foreach ($name in $names) {
    $instance = $profile.instances.$name
    $root = [IO.Path]::GetFullPath((Join-Path $repoRoot ([string]$instance.root)))
    if (-not $root.StartsWith($varRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        $failures.Add("$name root escaped var: $root")
        continue
    }
    $statusPath = Join-Path $root 'assembly-status.json'
    if (-not (Test-Path -LiteralPath $statusPath -PathType Leaf)) {
        $failures.Add("$name has no assembly-status.json")
        continue
    }
    try { $status = Get-Content -LiteralPath $statusPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch {
        $failures.Add("$name assembly status is invalid JSON")
        continue
    }
    if ($status.schema -ne 'minescape.runtime-assembly.v1' -or $status.instance -ne $name) { $failures.Add("$name assembly identity is invalid") }
    $expectedOrder = @($profile.datapack_order_low_to_high)
    $actualOrder = @($status.datapack_order_contract.entries)
    if ($status.datapack_order_contract.direction -ne 'lowest-to-highest' -or
        $status.datapack_order_contract.activation_evidence_required -ne $true -or
        $expectedOrder.Count -ne $actualOrder.Count) {
        $failures.Add("$name datapack order contract differs from the committed runtime profile")
    } else {
        for ($index = 0; $index -lt $expectedOrder.Count; $index++) {
            $expected = $expectedOrder[$index]
            $actual = $actualOrder[$index]
            $expectedSource = if ($expected.kind -eq 'artifact') { [string]$expected.key } else { [string]$expected.path }
            if ($actual.ordinal -ne $index -or $actual.priority -ne ($index * 10) -or
                $actual.kind -ne $expected.kind -or $actual.source -ne $expectedSource -or
                $actual.destination -ne $expected.destination -or ([string]$actual.sha512) -notmatch '^[0-9a-f]{128}$') {
                $failures.Add("$name datapack priority entry $index is invalid")
            }
            if ($expected.kind -eq 'generated') {
                $generatedSource = [IO.Path]::GetFullPath((Join-Path $repoRoot ([string]$expected.path)))
                if (-not (Test-Path -LiteralPath $generatedSource -PathType Leaf) -or
                    (Get-FileHash -LiteralPath $generatedSource -Algorithm SHA512).Hash.ToLowerInvariant() -ne ([string]$actual.sha512).ToLowerInvariant()) {
                    $failures.Add("$name generated datapack is absent or differs from its assembled source: $($expected.path)")
                }
            }
        }
    }
    if (@($status.generated_datapacks_missing).Count -ne 0) {
        $failures.Add("$name is missing one or more generated datapacks")
    }
    foreach ($managed in @($status.files)) {
        $path = [IO.Path]::GetFullPath((Join-Path $repoRoot (($managed.path -replace '/', '\'))))
        if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            $failures.Add("$name manifest path escaped its instance: $($managed.path)")
            continue
        }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $failures.Add("$name managed file is missing: $($managed.path)")
            continue
        }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash.ToLowerInvariant()
        if ($actual -ne ([string]$managed.sha512).ToLowerInvariant()) { $failures.Add("$name managed file hash changed: $($managed.path)") }
    }

    $datapackDirectory = Join-Path $root 'world\datapacks'
    $actualDatapackNames = if (Test-Path -LiteralPath $datapackDirectory -PathType Container) {
        @(Get-ChildItem -LiteralPath $datapackDirectory -Force | ForEach-Object { $_.Name } | Sort-Object)
    } else { @() }
    $expectedDatapackNames = @($expectedOrder | ForEach-Object { [string]$_.destination } | Sort-Object)
    if (($actualDatapackNames -join "`n") -ne ($expectedDatapackNames -join "`n")) {
        $failures.Add("$name datapack directory does not exactly match the seven declared priority entries")
    }
    foreach ($entry in $actualOrder) {
        $runtimePack = Join-Path $datapackDirectory ([string]$entry.destination)
        if (Test-Path -LiteralPath $runtimePack -PathType Leaf) {
            if ((Get-FileHash -LiteralPath $runtimePack -Algorithm SHA512).Hash.ToLowerInvariant() -ne ([string]$entry.sha512).ToLowerInvariant()) {
                $failures.Add("$name ordered datapack hash changed: $($entry.destination)")
            }
        }
    }

    $serverPropertiesPath = Join-Path $root 'server.properties'
    $bridgePropertiesPath = Join-Path $root 'config\minescape\bridge.properties'
    $worldBorderConfigPath = Join-Path $root 'config\worldborder.json5'
    $bundlePath = Join-Path $root 'server-bundle.lock.json'
    if (-not (Test-Path -LiteralPath $bundlePath -PathType Leaf)) {
        $failures.Add("$name server bundle lock is missing")
    } else {
        try {
            $bundle = Get-Content -LiteralPath $bundlePath -Raw -Encoding UTF8 | ConvertFrom-Json
            $bundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA512).Hash.ToLowerInvariant()
            if ($bundle.schema -ne 'minescape.server-bundle-lock.v1' -or $bundle.instance -ne $name -or
                $bundleHash -ne ([string]$status.server_bundle_sha512).ToLowerInvariant()) {
                $failures.Add("$name server bundle lock identity or digest is invalid")
            }
            if ((@($bundle.datapacks_low_to_high) | ConvertTo-Json -Depth 6 -Compress) -ne
                (@($actualOrder) | ConvertTo-Json -Depth 6 -Compress)) {
                $failures.Add("$name server bundle lock does not bind the datapack order")
            }
            if ([string]$bundle.seed -ne [string]$profile.seed -or
                ([string]$bundle.fabric_launcher_sha512).ToLowerInvariant() -ne ([string]$profile.launcher.sha512).ToLowerInvariant()) {
                $failures.Add("$name server bundle lock disagrees with seed or launcher pins")
            }
            if ((Get-FileHash -LiteralPath $serverPropertiesPath -Algorithm SHA512).Hash.ToLowerInvariant() -ne
                ([string]$bundle.server_properties_sha512).ToLowerInvariant() -or
                (Get-FileHash -LiteralPath $bridgePropertiesPath -Algorithm SHA512).Hash.ToLowerInvariant() -ne
                ([string]$bundle.bridge_properties_sha512).ToLowerInvariant()) {
                $failures.Add("$name server bundle lock does not bind current server/Bridge properties")
            }
            if ((Get-FileHash -LiteralPath $worldBorderConfigPath -Algorithm SHA512).Hash.ToLowerInvariant() -ne
                ([string]$bundle.world_border_config_sha512).ToLowerInvariant()) {
                $failures.Add("$name server bundle lock does not bind the current World Border configuration")
            }
            $expectedBundleFiles = @($status.files | Where-Object { $_.path -notmatch '/fabric-server-launch\.jar$' } | ForEach-Object {
                [pscustomobject][ordered]@{
                    path = ([IO.Path]::GetFullPath((Join-Path $repoRoot (($_.path -replace '/', '\'))))).Substring($root.Length + 1).Replace('\', '/')
                    sha512 = [string]$_.sha512
                }
            } | Sort-Object path)
            if (($expectedBundleFiles | ConvertTo-Json -Depth 4 -Compress) -ne (@($bundle.files) | ConvertTo-Json -Depth 4 -Compress)) {
                $failures.Add("$name server bundle file set differs from assembly status")
            }
        } catch { $failures.Add("$name server bundle lock is unreadable") }
    }

    if (-not (Test-Path -LiteralPath $serverPropertiesPath -PathType Leaf)) { $failures.Add("$name server.properties is missing") }
    else {
        try {
            $properties = Read-Properties $serverPropertiesPath
            $required = @{
                'level-seed' = '6246468738900744'
                'online-mode' = 'true'
                'white-list' = 'true'
                'enforce-whitelist' = 'true'
                'server-port' = [string]$instance.port
                'enable-rcon' = 'false'
                'enable-query' = 'false'
                'spawn-protection' = '0'
            }
            foreach ($key in $required.Keys) {
                if ($properties[$key] -ne $required[$key]) { $failures.Add("$name server property is not pinned: $key") }
            }
        } catch { $failures.Add($_.Exception.Message) }
    }
    if (-not (Test-Path -LiteralPath $bridgePropertiesPath -PathType Leaf)) { $failures.Add("$name Bridge configuration is missing") }
    else {
        try {
            $bridge = Read-Properties $bridgePropertiesPath
            if ($bridge['instance.mode'] -ne [string]$instance.mode) { $failures.Add("$name Bridge instance mode is not pinned") }
            if ($bridge['bridge.bind'] -ne '127.0.0.1') { $failures.Add("$name Bridge is not loopback-only") }
            if ($bridge['bridge.port'] -ne [string]$instance.bridge_port) { $failures.Add("$name Bridge port is not isolated") }
        } catch { $failures.Add($_.Exception.Message) }
    }
    if (-not (Test-Path -LiteralPath $worldBorderConfigPath -PathType Leaf)) { $failures.Add("$name World Border configuration is missing") }
    else {
        try {
            $worldBorder = Get-Content -LiteralPath $worldBorderConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $expectedBorder = [ordered]@{
                enableCustomOverworldBorder = $true
                enableCustomNetherBorder = $true
                enableCustomEndBorder = $true
                shouldLoopToOppositeBorder = $false
                distanceTeleportedBack = 32
                nearBorderMessage = 'The edge of this finite world is close.'
                hitBorderMessage = 'You reached the world edge and were moved safely inward.'
                loopBorderMessage = 'World-edge looping is disabled.'
                overworldBorderPositiveX = 16384
                overworldBorderNegativeX = -16384
                overworldBorderPositiveZ = 16384
                overworldBorderNegativeZ = -16384
                netherBorderPositiveX = 2048
                netherBorderNegativeX = -2048
                netherBorderPositiveZ = 2048
                netherBorderNegativeZ = -2048
                endBorderPositiveX = 8192
                endBorderNegativeX = -8192
                endBorderPositiveZ = 8192
                endBorderNegativeZ = -8192
            }
            $actualNames = @($worldBorder.PSObject.Properties.Name | Sort-Object)
            $expectedNames = @($expectedBorder.Keys | Sort-Object)
            if (($actualNames -join "`n") -ne ($expectedNames -join "`n")) {
                $failures.Add("$name World Border configuration has missing or unknown keys")
            } else {
                foreach ($key in $expectedBorder.Keys) {
                    if ($worldBorder.$key -ne $expectedBorder[$key]) {
                        $failures.Add("$name World Border configuration differs at $key")
                    }
                }
            }
        } catch { $failures.Add("$name World Border configuration is invalid JSON5/JSON") }
    }
    $runtimeLauncher = Join-Path $root 'fabric-server-launch.jar'
    $eula = Join-Path $root 'eula.txt'
    if ($name -eq 'MineScape' -and $status.mode -ne 'release-assembly') {
        if (Test-Path -LiteralPath $runtimeLauncher) { $failures.Add('MineScape production launcher exists before release') }
        if (-not (Test-Path -LiteralPath $eula -PathType Leaf) -or (Get-Content -LiteralPath $eula -Raw -Encoding UTF8) -match '(?m)^eula=true\s*$') {
            $failures.Add('MineScape production EULA is not inert before release')
        }
    }
    if ($name -eq 'MineJammer' -and (Test-Path -LiteralPath $runtimeLauncher -PathType Leaf)) {
        $launcherHash = (Get-FileHash -LiteralPath $runtimeLauncher -Algorithm SHA512).Hash.ToLowerInvariant()
        if ($launcherHash -ne ([string]$profile.launcher.sha512).ToLowerInvariant() -or $status.launcher -ne 'verified-lab-only') {
            $failures.Add('MineJammer launcher is not the committed lab-only launcher')
        }
    }
    if ($RequireInert) {
        if (Test-Path -LiteralPath (Join-Path $root 'world\level.dat')) { $failures.Add("$name already contains a generated world") }
        if ($status.launch_permitted -eq $true) { $failures.Add("$name is marked launch-permitted") }
        if (-not (Test-Path -LiteralPath $eula -PathType Leaf) -or (Get-Content -LiteralPath $eula -Raw -Encoding UTF8) -match '(?m)^eula=true\s*$') {
            $failures.Add("$name is not an unaccepted-EULA skeleton")
        }
        if ($name -eq 'MineScape' -and (Test-Path -LiteralPath $runtimeLauncher)) { $failures.Add('MineScape inert skeleton contains a launcher') }
        if ($name -eq 'MineJammer' -and -not (Test-Path -LiteralPath $runtimeLauncher -PathType Leaf)) { $failures.Add('MineJammer inert lab is missing its verified launcher') }
    }
}

if ($names -contains 'MineScape' -and $names -contains 'MineJammer') {
    $production = [IO.Path]::GetFullPath((Join-Path $repoRoot ([string]$profile.instances.MineScape.root)))
    $staging = [IO.Path]::GetFullPath((Join-Path $repoRoot ([string]$profile.instances.MineJammer.root)))
    if ($production -eq $staging) { $failures.Add('MineScape and MineJammer share a writable root') }
    if ([int]$profile.instances.MineScape.port -eq [int]$profile.instances.MineJammer.port) { $failures.Add('Minecraft ports collide') }
    if ([int]$profile.instances.MineScape.bridge_port -eq [int]$profile.instances.MineJammer.bridge_port) { $failures.Add('Bridge ports collide') }
}

if ($failures.Count -gt 0) {
    Write-Host 'Runtime verification failed:' -ForegroundColor Red
    $failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}
Write-Host "Runtime verification passed for: $($names -join ', ')." -ForegroundColor Green
