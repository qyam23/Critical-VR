@echo off
setlocal

set MODE=development
set DO_PULL=0
set SKIP_BUILD=0
set SKIP_VALIDATION=0

:parse_args
if "%~1"=="" goto after_args
if /I "%~1"=="--pull" (
  set DO_PULL=1
  shift
  goto parse_args
)
if /I "%~1"=="--release" (
  set MODE=release
  shift
  goto parse_args
)
if /I "%~1"=="--skip-build" (
  set SKIP_BUILD=1
  shift
  goto parse_args
)
if /I "%~1"=="--skip-validation" (
  set SKIP_VALIDATION=1
  shift
  goto parse_args
)
echo Unknown argument: %~1
exit /b 1

:after_args
pushd "%~dp0.."

if "%DO_PULL%"=="1" (
  echo Running git pull --ff-only...
  git pull --ff-only
  if errorlevel 1 (
    echo git pull failed.
    popd
    exit /b 1
  )
)

if "%SKIP_VALIDATION%"=="0" (
  call Tools\validate_quest_project.bat
  if errorlevel 1 (
    echo Validation step failed.
    popd
    exit /b 1
  )
)

if "%SKIP_BUILD%"=="0" (
  if /I "%MODE%"=="release" (
    if "%SKIP_VALIDATION%"=="1" (
      call Tools\build_quest.bat --release --skip-validation
    ) else (
      call Tools\build_quest.bat --release
    )
  ) else (
    if "%SKIP_VALIDATION%"=="1" (
      call Tools\build_quest.bat --skip-validation
    ) else (
      call Tools\build_quest.bat
    )
  )
  if errorlevel 1 (
    echo Build step failed.
    popd
    exit /b 1
  )
)

where adb >nul 2>nul
if errorlevel 1 (
  echo adb was not found on PATH.
  popd
  exit /b 1
)

adb start-server >nul
for /f "tokens=1" %%D in ('adb devices ^| findstr /R "[A-Za-z0-9].*device$"') do set DEVICE_FOUND=1
if not defined DEVICE_FOUND (
  echo No Quest headset detected through adb.
  popd
  exit /b 1
)

if /I "%MODE%"=="release" (
  set APK_PATH=Builds\Quest\Release\CriticalVR-Quest-Release.apk
) else (
  set APK_PATH=Builds\Quest\Development\CriticalVR-Quest-Development.apk
)

if not exist "%APK_PATH%" (
  echo APK not found:
  echo   %CD%\%APK_PATH%
  popd
  exit /b 1
)

echo Installing APK:
echo   %CD%\%APK_PATH%
adb install -r "%APK_PATH%"
if errorlevel 1 (
  echo adb install failed.
  popd
  exit /b 1
)

adb shell monkey -p com.qyam23.criticalvr 1 >nul 2>nul

echo Quest update completed successfully.
echo APK:
echo   %CD%\%APK_PATH%
echo Validation report:
echo   %CD%\Docs\Quest\ValidationReport.latest.md
echo Build report:
echo   %CD%\Builds\Quest\Logs\last-build-report.md

popd
exit /b 0
