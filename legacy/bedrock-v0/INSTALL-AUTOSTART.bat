@echo off
echo Installing Minescape always-on setup (needs admin approval):
echo   1. Server auto-starts when you log in
echo   2. PC never sleeps (display can still turn off)
powershell -NoProfile -Command "Start-Process cmd -Verb RunAs -ArgumentList '/c schtasks /Create /F /TN \"Minescape Server\" /TR \"\"\"%~dp0scripts\autostart.bat\"\"\" /SC ONLOGON /RL LIMITED && powercfg /change standby-timeout-ac 0 && powercfg /change hibernate-timeout-ac 0 && echo. && echo Done - Minescape now survives reboots. && pause'"
