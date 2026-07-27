[CmdletBinding()]
param([string] $DesktopDirectory)

. (Join-Path $PSScriptRoot 'common.ps1')
$repoRoot = Get-MineScapeRoot
$publishRoot = Join-Path $repoRoot 'var\host\MineDeck'
$executable = Get-Item -LiteralPath (Join-Path $publishRoot 'MineDeck.exe') -ErrorAction SilentlyContinue
if (-not $executable) { throw "Published MineDeck executable was not found under $publishRoot. Run ops\install-host.ps1 -PublishOnly first." }

$desktop = if ([string]::IsNullOrWhiteSpace($DesktopDirectory)) {
    [Environment]::GetFolderPath('Desktop')
} else {
    [IO.Path]::GetFullPath($DesktopDirectory)
}
if (-not (Test-Path -LiteralPath $desktop)) { throw "Desktop directory does not exist: $desktop" }

$shell = New-Object -ComObject WScript.Shell
$definitions = @(
    @{ Name = 'Launch MineScape & MineDeck.lnk'; Arguments = '--dashboard'; Description = 'Start or attach to MineScape and open MineDeck' },
    @{ Name = 'Play MineScape.lnk'; Arguments = '--play'; Description = 'Start MineScape and open the official Minecraft Launcher' }
)

foreach ($definition in $definitions) {
    $shortcutPath = Join-Path $desktop $definition.Name
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executable.FullName
    $shortcut.Arguments = $definition.Arguments
    $shortcut.WorkingDirectory = $repoRoot
    $shortcut.IconLocation = "$($executable.FullName),0"
    $shortcut.Description = $definition.Description
    $shortcut.Save()
}

Write-Host "Created both MineScape shortcuts on $desktop." -ForegroundColor Green
