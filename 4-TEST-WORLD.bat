@echo off
setlocal
set "ACTION=%~1"
set "NAME=%~2"
if "%ACTION%"=="" (
    echo Spelljammer - the same-seed test copy of Minescape.
    echo.
    echo   1. refresh  - copy Minescape into Spelljammer ^(replaces old Spelljammer^)
    echo   2. build    - switch the live world to Spelljammer, to build/test
    echo   3. play     - switch the live world back to Minescape, for the kids
    echo   4. commit   - copy a finished .mcstructure from Spelljammer into packs\
    echo   5. status   - show which world is currently live
    echo.
    set /p "CHOICE=Pick 1-5: "
    if "%CHOICE%"=="1" set "ACTION=refresh"
    if "%CHOICE%"=="2" set "ACTION=build"
    if "%CHOICE%"=="3" set "ACTION=play"
    if "%CHOICE%"=="4" (
        set "ACTION=commit"
        set /p "NAME=Structure name ^(as named in the Structure Block^): "
    )
    if "%CHOICE%"=="5" set "ACTION=status"
)
if "%ACTION%"=="" (
    echo No action chosen - exiting.
    pause
    exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\testworld.ps1" -Action %ACTION% -Name "%NAME%"
pause
