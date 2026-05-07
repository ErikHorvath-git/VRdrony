@echo off
cd /d "%~dp0"
echo Pushing to GitHub...
echo.
git push origin main
set RC=%ERRORLEVEL%
echo.
if "%RC%"=="0" (
  echo === PUSH SUCCESS ===
) else (
  echo === PUSH FAILED ^(exit %RC%^) ===
)
pause
