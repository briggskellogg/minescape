@echo off
set "NODE_EXE=node"
where node >nul 2>nul
if not %errorlevel%==0 (
    if exist "%ProgramFiles%\nodejs\node.exe" ( set "NODE_EXE=%ProgramFiles%\nodejs\node.exe" ) else (
    if exist "%LocalAppData%\Programs\nodejs\node.exe" ( set "NODE_EXE=%LocalAppData%\Programs\nodejs\node.exe" ) else ( set "NODE_EXE=" ) )
)
if defined NODE_EXE (
    echo Starting Minescape Command Deck...  http://localhost:8420
    start "" http://localhost:8420
    "%NODE_EXE%" "%~dp0deck\deck.js"
) else (
    echo Node.js not found - starting plain server console instead.
    echo For the Command Deck, install Node LTS from https://nodejs.org
    cd /d "%~dp0server"
    bedrock_server.exe
)
pause
