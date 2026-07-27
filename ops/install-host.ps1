[CmdletBinding()]
param([switch] $PublishOnly)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
Initialize-PrivateDotnetEnvironment
$dotnet = Get-PrivateDotnet
$publishRoot = Join-Path $repoRoot 'var\host\MineDeck'
New-DirectoryIfMissing $publishRoot

$project = Join-Path $repoRoot 'MineDeck\src\MineDeck\MineDeck.csproj'
if (-not (Test-Path -LiteralPath $project)) { throw 'MineDeck application project is missing.' }

Write-Step 'Publishing MineDeck'
# The repository clears ambient NuGet feeds for ordinary builds. A self-contained
# Windows publish additionally needs Microsoft's runtime packs, so name the one
# official feed explicitly; subsequent publishes use the verified local cache.
& $dotnet publish $project --configuration Release --runtime win-x64 --self-contained true `
    --source 'https://api.nuget.org/v3/index.json' --output $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'MineDeck publish failed.' }

$executable = Get-Item -LiteralPath (Join-Path $publishRoot 'MineDeck.exe') -ErrorAction SilentlyContinue
if (-not $executable) { throw 'Published MineDeck executable was not found.' }

if (-not $PublishOnly) {
    & (Join-Path $PSScriptRoot 'configure-minedeck.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'MineDeck first-run configuration failed.' }
    & (Join-Path $PSScriptRoot 'create-shortcuts.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Shortcut creation failed.' }
}

Write-Host "MineDeck published to $publishRoot." -ForegroundColor Green
