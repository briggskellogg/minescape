@echo off
rem Minescape autostart target - launched at logon by Task Scheduler.
rem Starts the deck (no browser popup, no pause). Deck watchdog handles the rest.
set "NODE_EXE=node"
where node >nul 2>nul
if not %errorlevel%==0 (
    if exist "%ProgramFiles%\nodejs\node.exe" ( set "NODE_EXE=%ProgramFiles%\nodejs\node.exe" ) else (
    if exist "%LocalAppData%\Programs\nodejs\node.exe" ( set "NODE_EXE=%LocalAppData%\Programs\nodejs\node.exe" ) else ( set "NODE_EXE=" ) )
)
if defined NODE_EXE (
    "%NODE_EXE%" "%~dp0..\deck\deck.js"
) else (
    cd /d "%~dp0..\server"
    bedrock_server.exe
)
