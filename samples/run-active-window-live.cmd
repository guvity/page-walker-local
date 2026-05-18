@echo off
setlocal
set CONFIG=%LOCALAPPDATA%\PageWalkerLocal\appsettings.live.json
echo Copy samples\appsettings.sample.json to "%CONFIG%" and set "dryRun": false after reviewing safety limits.
echo Then run:
echo PageWalkerLocal.exe --config "%CONFIG%"
