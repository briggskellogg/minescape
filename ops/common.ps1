Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-MineScapeRoot {
    $resolved = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
    if (-not (Test-Path -LiteralPath (Join-Path $resolved 'docs\MINESCAPE_V1_MASTER_PLAN.md'))) {
        throw "MineScape repository root could not be verified: $resolved"
    }
    return $resolved
}

function Get-PrivateDotnet {
    $root = Get-MineScapeRoot
    $candidate = Join-Path $root '.tools\dotnet-10.0.302\dotnet.exe'
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "Private .NET SDK is missing. Run ops\bootstrap-toolchains.ps1 first."
    }
    return $candidate
}

function Initialize-PrivateDotnetEnvironment {
    $root = Get-MineScapeRoot
    $env:DOTNET_CLI_HOME = Join-Path $root '.tools\dotnet-home'
    $env:NUGET_PACKAGES = Join-Path $root '.tools\nuget-packages'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    New-DirectoryIfMissing $env:DOTNET_CLI_HOME
    New-DirectoryIfMissing $env:NUGET_PACKAGES
}

function Get-PrivateJavaHome {
    $root = Get-MineScapeRoot
    $candidate = Join-Path $root '.tools\temurin-25.0.3+9'
    $java = Get-ChildItem -LiteralPath $candidate -Filter java.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '[\\/]bin[\\/]java\.exe$' } |
        Select-Object -First 1
    if (-not $java) {
        throw "Private Java 25 runtime is missing. Run ops\bootstrap-toolchains.ps1 first."
    }
    return (Split-Path -Parent (Split-Path -Parent $java.FullName))
}

function Get-PrivatePython {
    $root = Get-MineScapeRoot
    $candidate = Join-Path $root '.tools\python-3.13.14\python.exe'
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "Private Python is missing. Run ops\bootstrap-toolchains.ps1 first."
    }
    return $candidate
}

function Get-PrivateGradle {
    $root = Get-MineScapeRoot
    $candidate = Join-Path $root '.tools\gradle-9.5.1\gradle-9.5.1\bin\gradle.bat'
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "Private Gradle is missing. Run ops\bootstrap-toolchains.ps1 first."
    }
    return $candidate
}

function New-DirectoryIfMissing([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Write-Step([string] $Message) {
    Write-Host "`n== $Message" -ForegroundColor Cyan
}
