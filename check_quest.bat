@echo off
cd /d "%~dp0"
echo === Checking adb for Quest connection ===
echo.

set ADB="C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"

if not exist %ADB% (
  echo Could not find adb at:
  echo %ADB%
  echo.
  echo Trying PATH adb...
  adb devices
) else (
  %ADB% devices
)

echo.
echo If Quest 2 is listed as "device" -- ready for Build And Run.
echo If listed as "unauthorized" -- accept USB debugging prompt INSIDE the headset.
echo If list is EMPTY:
echo    1. Plug Quest 2 in via USB-C cable
echo    2. Enable Developer Mode in Meta Horizon mobile app
echo    3. Put on the headset and accept "Allow USB debugging" prompt
echo.
pause
