#Requires -Version 5.1
[CmdletBinding()]
param([string] $ConfigurationRoot)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
Initialize-PrivateDotnetEnvironment
$dotnet = Get-PrivateDotnet
$javaHome = Get-PrivateJavaHome

$localRoot = if ([string]::IsNullOrWhiteSpace($ConfigurationRoot)) {
    Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'MineScape\MineDeck'
} else {
    [IO.Path]::GetFullPath($ConfigurationRoot)
}
$configPath = Join-Path $localRoot 'appsettings.json'
$passwordPath = Join-Path $localRoot 'INITIAL_ADMIN_PASSWORD.txt'
New-DirectoryIfMissing $localRoot

if (Test-Path -LiteralPath $configPath -PathType Leaf) {
    # The supported configuration contains absolute runtime paths by design. Never
    # silently preserve a generated configuration after its repository was moved:
    # doing so would make the shortcuts target the new executable while supervising
    # the abandoned old runtime. Refusal is byte-preserving, including the password
    # hash and any unrelated owner customizations or secrets.
    try {
        $existing = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $configuredProduction = [string]$existing.MineDeck.Minecraft.WorkingDirectory
        if (-not [string]::IsNullOrWhiteSpace($configuredProduction)) {
            $configuredProduction = [IO.Path]::GetFullPath(
                [Environment]::ExpandEnvironmentVariables($configuredProduction))
            $expectedProduction = [IO.Path]::GetFullPath((Join-Path $repoRoot 'var\MineScape'))
            if (-not $configuredProduction.Equals($expectedProduction, [StringComparison]::OrdinalIgnoreCase)) {
                $knownSuffix = [IO.Path]::DirectorySeparatorChar + 'var' +
                    [IO.Path]::DirectorySeparatorChar + 'MineScape'
                if ($configuredProduction.EndsWith($knownSuffix, [StringComparison]::OrdinalIgnoreCase)) {
                    throw "Existing MineDeck configuration points to a different repository runtime: $configuredProduction. The file was preserved byte-for-byte. Rebase its repository-derived paths deliberately, or archive it and rerun this script from the permanent repository location: $repoRoot"
                }
            }
        }
    } catch [System.Management.Automation.RuntimeException] {
        throw
    } catch {
        throw "Existing MineDeck configuration could not be validated and was preserved byte-for-byte: $($_.Exception.Message)"
    }
    Write-Host "Existing MineDeck configuration was preserved: $configPath" -ForegroundColor Yellow
    if (Test-Path -LiteralPath $passwordPath -PathType Leaf) {
        Write-Host "The initial local administrator-password note still exists: $passwordPath" -ForegroundColor Yellow
    }
    exit 0
}

$passwordTool = Join-Path $repoRoot 'MineDeck\tools\MineDeck.PasswordTool\MineDeck.PasswordTool.csproj'
if (-not (Test-Path -LiteralPath $passwordTool -PathType Leaf)) { throw 'MineDeck password tool is missing.' }
$generatedLines = @(& $dotnet run --project $passwordTool --configuration Release -- --generate-json)
$generatedText = ($generatedLines | Where-Object { $_ -and $_.Trim() } | Select-Object -Last 1).Trim()
if ($LASTEXITCODE -ne 0 -or -not $generatedText) { throw 'MineDeck password generation failed.' }
$generated = $generatedText | ConvertFrom-Json
if (-not $generated.password -or -not $generated.hash) { throw 'MineDeck password tool returned an invalid bootstrap record.' }

$productionRoot = Join-Path $repoRoot 'var\MineScape'
$mineJammerRoot = Join-Path $repoRoot 'var\MineJammer'
$java = Join-Path $javaHome 'bin\javaw.exe'
$userProfileRoot = [Environment]::GetFolderPath('UserProfile')
if ([string]::IsNullOrWhiteSpace($userProfileRoot)) { $userProfileRoot = $env:USERPROFILE }
if ([string]::IsNullOrWhiteSpace($userProfileRoot)) { $userProfileRoot = $localRoot }
$configuration = [ordered]@{
    MineDeck = [ordered]@{
        InstanceName = 'family-production'
        DataDirectory = $localRoot
        Dashboard = [ordered]@{
            ListenUrl = 'http://127.0.0.1:43117'
            PublicBaseUrl = 'http://127.0.0.1:43117'
        }
        Admin = [ordered]@{
            PasswordHash = [string]$generated.hash
            CookieHours = 8
        }
        Minecraft = [ordered]@{
            Host = '127.0.0.1'
            Port = 25565
            ProtocolVersion = -1
            ExecutablePath = $java
            Arguments = '-Xms4G -Xmx12G -jar fabric-server-launch.jar nogui'
            WorkingDirectory = $productionRoot
            StartupTimeoutSeconds = 180
        }
        MineJammer = [ordered]@{
            Host = '127.0.0.1'
            Port = 25566
            ProtocolVersion = -1
            ExecutablePath = $java
            Arguments = '-Xms2G -Xmx8G -jar fabric-server-launch.jar nogui'
            WorkingDirectory = $mineJammerRoot
            StartupTimeoutSeconds = 180
        }
        Bridge = [ordered]@{
            Mode = 'Http'
            BaseUrl = 'http://127.0.0.1:8765'
            TokenFile = (Join-Path $productionRoot 'config\minescape\bridge.token')
            TokenEnvironmentVariable = 'MINESCAPE_BRIDGE_TOKEN'
            TimeoutSeconds = 12
        }
        Launcher = [ordered]@{
            LaunchTarget = 'minecraft-launcher:'
            QuickPlayLabel = 'MineScape'
        }
        Backups = [ordered]@{
            DestinationDirectory = (Join-Path $userProfileRoot 'MineScape-Backups-CONSTRUCTION-ONLY')
            SourceDirectories = @(
                (Join-Path $productionRoot 'world'),
                $localRoot
            )
        }
    }
}

$json = $configuration | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($configPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$note = @"
MineDeck initial administrator password
=======================================

$($generated.password)

This file is local to this Windows account and is not in Git. Store the password in your password manager, verify MineDeck login, then delete this note.
The construction-only backup path in appsettings.json is not an independent backup and cannot satisfy the V1 release gate.
"@
[IO.File]::WriteAllText($passwordPath, $note, [Text.UTF8Encoding]::new($false))

Write-Host "MineDeck local configuration created: $configPath" -ForegroundColor Green
Write-Host "Initial administrator password written locally: $passwordPath" -ForegroundColor Yellow
Write-Host 'No EULA was accepted and no Minecraft world was started.'
