@echo off
setlocal

cd /d "%~dp0"

where python >nul 2>nul
if %errorlevel%==0 (
  python "%~dp0run_local.py"
  exit /b %errorlevel%
)

echo Python was not found, falling back to npm.cmd.
if not exist ".env.local" (
  if exist ".env.example" (
    copy /Y ".env.example" ".env.local" >nul
    echo Created .env.local from .env.example
  )
)

if not exist "node_modules" (
  call npm.cmd install
  if errorlevel 1 exit /b %errorlevel%
)

call npm.cmd run dev
exit /b %errorlevel%
