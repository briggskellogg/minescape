[CmdletBinding()]
param([switch] $Release)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
Initialize-PrivateDotnetEnvironment
$dotnet = Get-PrivateDotnet
$javaHome = Get-PrivateJavaHome
$env:JAVA_HOME = $javaHome
$env:GRADLE_USER_HOME = Join-Path $repoRoot '.tools\gradle-home'
New-DirectoryIfMissing $env:GRADLE_USER_HOME
$configuration = if ($Release) { 'Release' } else { 'Debug' }

Write-Step "Building MineDeck ($configuration)"
$mineDeckProject = Join-Path $repoRoot 'MineDeck\src\MineDeck\MineDeck.csproj'
if (-not (Test-Path -LiteralPath $mineDeckProject)) { throw 'MineDeck project is missing.' }
& $dotnet build $mineDeckProject --configuration $configuration
if ($LASTEXITCODE -ne 0) { throw 'MineDeck build failed.' }

Write-Step 'Testing MineDeck'
$mineDeckTests = Join-Path $repoRoot 'MineDeck\tests\MineDeck.Tests\MineDeck.Tests.csproj'
if (-not (Test-Path -LiteralPath $mineDeckTests)) { throw 'MineDeck executable test project is missing.' }
& $dotnet run --project $mineDeckTests --configuration $configuration
if ($LASTEXITCODE -ne 0) { throw "MineDeck tests failed: $mineDeckTests" }

Write-Step 'Building and testing MineScape Bridge/Client'
$gradlew = Join-Path $repoRoot 'MineScape\gradlew.bat'
if (-not (Test-Path -LiteralPath $gradlew)) { throw 'MineScape Gradle wrapper is missing.' }
& $gradlew --no-daemon -p (Join-Path $repoRoot 'MineScape') clean test build
if ($LASTEXITCODE -ne 0) { throw 'MineScape Fabric build/tests failed.' }

Write-Step 'Testing MineJammer'
$python = Get-PrivatePython
$mineJammerRoot = Join-Path $repoRoot 'MineJammer'
$mineJammerTests = Join-Path $mineJammerRoot 'tests'
$pythonCode = "import sys, unittest; sys.path.insert(0, r'''$mineJammerRoot'''); suite=unittest.defaultTestLoader.discover(r'''$mineJammerTests'''); result=unittest.TextTestRunner(verbosity=2).run(suite); raise SystemExit(0 if result.wasSuccessful() else 1)"
& $python -c $pythonCode
if ($LASTEXITCODE -ne 0) { throw 'MineJammer tests failed.' }

Write-Step 'Testing repository contract'
$repositoryTests = Join-Path $repoRoot 'tests'
$pythonCode = "import unittest; suite=unittest.defaultTestLoader.discover(r'''$repositoryTests'''); result=unittest.TextTestRunner(verbosity=2).run(suite); raise SystemExit(0 if result.wasSuccessful() else 1)"
& $python -c $pythonCode
if ($LASTEXITCODE -ne 0) { throw 'Repository contract tests failed.' }

Write-Host 'Build completed.' -ForegroundColor Green
