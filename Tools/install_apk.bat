@echo off
setlocal

if "%~1"=="" (
  echo Usage: install_apk.bat path\to\CriticalVR.apk
  exit /b 1
)

set APK_PATH=%~1

if not exist "%APK_PATH%" (
  echo APK not found: %APK_PATH%
  exit /b 1
)

where adb >nul 2>nul
if errorlevel 1 (
  echo adb was not found on PATH.
  echo Install Android platform-tools or run from a shell where adb is available.
  exit /b 1
)

adb wait-for-device
adb install -r "%APK_PATH%"
