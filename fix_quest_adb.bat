@echo off
cd /d "%~dp0"

set ADB="C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"

echo === Step 1: Kill all running adb servers ===
echo (Meta Horizon Link, Unity, and Android Studio all spawn their own
echo  adb daemons; only one can hold the USB device at a time.)
echo.
%ADB% kill-server 2>nul
taskkill /F /IM adb.exe /T 2>nul
timeout /t 2 /nobreak >nul

echo === Step 2: Start fresh Unity adb server ===
%ADB% start-server
timeout /t 2 /nobreak >nul
echo.

echo === Step 3: Check device list ===
%ADB% devices
echo.

echo If Quest 2 still missing or unauthorized:
echo   1. Inside the headset look for "Allow USB debugging from this computer?"
echo      (Open the menu, check Notifications, or unplug + replug the cable)
echo      Tap Allow and check "Always allow from this computer"
echo   2. Try a DIFFERENT USB-C cable (some are charge-only, no data)
echo   3. Try a DIFFERENT USB port on the PC (USB 3.x preferred)
echo   4. Make sure the headset is powered ON, not asleep
echo.
echo Once it shows as "device" (not "unauthorized"), close this window
echo and click Build And Run in Unity.
echo.
pause
