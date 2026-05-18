@echo off
setlocal
set APP=%~dp0..\artifacts\PageWalkerLocal-win-x64\PageWalkerLocal.exe
if not exist "%APP%" set APP=%~dp0..\src\PageWalkerLocal\bin\Release\net8.0-windows\win-x64\publish\PageWalkerLocal.exe
"%APP%" --config "%~dp0appsettings.sample.json"
