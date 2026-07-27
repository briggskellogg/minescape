[CmdletBinding()]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$MineJammerArgs)

$PinnedPython = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\.tools\python-3.13.14\python.exe'))
if (-not (Test-Path -LiteralPath $PinnedPython -PathType Leaf)) {
    throw "Pinned MineScape Python is missing: $PinnedPython"
}

$Entry = "import sys; sys.path.insert(0, r'$PSScriptRoot'); from minejammer.cli import main; raise SystemExit(main(sys.argv[1:]))"
& $PinnedPython -c $Entry @MineJammerArgs
exit $LASTEXITCODE
