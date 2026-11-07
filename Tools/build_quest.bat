@echo off
setlocal

for %%I in ("%~dp0..") do set REPO_ROOT=%%~fI
set MODE=development
set UNITY_METHOD=CriticalVR.EditorTools.CriticalVRBuildAutomation.BuildQuestDevelopmentBatch
set OUTPUT_PATH=Builds\Quest\Development\CriticalVR-Quest-Development.apk
set LOG_DIR=%REPO_ROOT%\Builds\Quest\Logs
set LOG_FILE=%LOG_DIR%\build-quest.log

:parse_args
if "%~1"=="" goto after_args
if /I "%~1"=="--release" (
  set MODE=release
  set UNITY_METHOD=CriticalVR.EditorTools.CriticalVRBuildAutomation.BuildQuestReleaseBatch
  set OUTPUT_PATH=Builds\Quest\Release\CriticalVR-Quest-Release.apk
  shift
  goto parse_args
)
if /I "%~1"=="--skip-validation" (
  set CRITICAL_VR_SKIP_VALIDATION=1
  shift
  goto parse_args
)
echo Unknown argument: %~1
exit /b 1

:after_args
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

if exist "%REPO_ROOT%\Temp\UnityLockfile" (
  tasklist /FI "IMAGENAME eq Unity.exe" | find /I "Unity.exe" >nul
  if errorlevel 1 (
    del /f /q "%REPO_ROOT%\Temp\UnityLockfile" >nul 2>nul
  ) else (
    echo Another Unity editor instance appears to have this project open.
    echo Close the editor first, then rerun the build script.
    exit /b 1
  )
)

call :resolve_unity
if errorlevel 1 exit /b 1

echo Building Quest APK (%MODE%) with Unity:
echo   %UNITY_EDITOR%
echo Unity log:
echo   %LOG_FILE%

"%UNITY_EDITOR%" -batchmode -quit -buildTarget Android -projectPath "%REPO_ROOT%" -executeMethod %UNITY_METHOD% -logFile "%LOG_FILE%"
if errorlevel 1 (
  echo Build failed. See:
  echo   %REPO_ROOT%\Builds\Quest\Logs\last-build-report.md
  echo   %LOG_FILE%
  exit /b 1
)

echo Build succeeded.
echo APK:
echo   %REPO_ROOT%\%OUTPUT_PATH%
echo Report:
echo   %REPO_ROOT%\Builds\Quest\Logs\last-build-report.md
exit /b 0

:resolve_unity
if defined UNITY_EDITOR_PATH if exist "%UNITY_EDITOR_PATH%" (
  set UNITY_EDITOR=%UNITY_EDITOR_PATH%
  exit /b 0
)

for /f "usebackq delims=" %%I in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$candidate = $null; if ($env:UNITY_EDITOR_PATH -and (Test-Path $env:UNITY_EDITOR_PATH)) { $candidate = $env:UNITY_EDITOR_PATH } else { $editors = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending; foreach ($editor in $editors) { $path = Join-Path $editor.FullName 'Editor\Unity.exe'; if (Test-Path $path) { $candidate = $path; break } } }; if ($candidate) { Write-Output $candidate }"` ) do set UNITY_EDITOR=%%I

if not defined UNITY_EDITOR (
  echo Unable to locate Unity.exe. Set UNITY_EDITOR_PATH first.
  exit /b 1
)

if not exist "%UNITY_EDITOR:\Editor\Unity.exe=\Editor\Data\PlaybackEngines\AndroidPlayer%" (
  echo Android Build Support was not found for this Unity editor.
  exit /b 1
)

exit /b 0
