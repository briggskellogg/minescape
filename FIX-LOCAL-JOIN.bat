@echo off
echo This lets YOUR Minecraft (on this PC) join the server running on this same PC.
echo (One-time Windows loopback exemption - requires admin approval.)
powershell -NoProfile -Command "Start-Process cmd -Verb RunAs -ArgumentList '/c CheckNetIsolation LoopbackExempt -a -n=Microsoft.MinecraftUWP_8wekyb3d8bbwe && echo Done - you can now join your own server. && pause'"
