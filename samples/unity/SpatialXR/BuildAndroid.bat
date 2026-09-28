@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildAndroid.ps1" %*
set "result=%ERRORLEVEL%"
echo.
if "%result%"=="0" (echo APK build completed.) else (echo APK build failed. Exit code: %result%)
if /I not "%~1"=="-CheckOnly" pause
exit /b %result%
