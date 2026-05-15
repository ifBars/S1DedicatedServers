@echo off
cd /d "%~dp0"
if not exist "steam_appid.txt" (
    >"steam_appid.txt" echo 3164500
)
start "" "Schedule I.exe" --batchmode --nographics --dedicated-server
