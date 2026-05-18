@echo off
setlocal
set CONFIG=%TEMP%\pagewalker-rectangle-dryrun.json
powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content '%~dp0appsettings.sample.json' -Raw).Replace('\"targetMode\": \"ActiveWindow\"','\"targetMode\": \"Rectangle\"') | Set-Content -Encoding UTF8 '%CONFIG%'"
set APP=%~dp0..\artifacts\PageWalkerLocal-win-x64\PageWalkerLocal.exe
if not exist "%APP%" set APP=%~dp0..\src\PageWalkerLocal\bin\Release\net8.0-windows\win-x64\publish\PageWalkerLocal.exe
"%APP%" --config "%CONFIG%"
