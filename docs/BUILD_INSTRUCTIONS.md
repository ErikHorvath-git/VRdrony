# VRdrony — Build & Deploy

How to build the project to a Meta Quest 3 APK from a clean machine. Written for Unity **6** (6000.x) and Meta XR SDK All-in-One **v85+**.

> One-time setup is the long part. After that, "Build & Run" is one click.

---

## 0. Prerequisites

- **Unity Hub** (latest)
- **Unity 6** (any 6000.x release; LTS preferred). Install via Unity Hub.
- **Meta Quest 3** with charged battery and a USB-C cable to the dev machine.
- A **Meta** account that's also enrolled as a **developer** (Quest dev mode requires this — see §5).
- ~30 GB free disk for Unity + Android SDK/NDK.

---

## 1. Install Unity 6 with Android modules

In **Unity Hub → Installs → Install Editor → Unity 6.x**, tick these modules:

- **Android Build Support**
  - **OpenJDK** (sub-module)
  - **Android SDK & NDK Tools** (sub-module)
- *(Optional)* Documentation, Linux/Windows IL2CPP — only what your machine actually needs.

> ✅ **Check after install:** `Unity → Preferences → External Tools` should show paths populated for JDK, Android SDK, Android NDK, Gradle. If anything is empty, tick the "Use Unity-installed" checkbox or click "Browse" to locate it.

NDK version: Unity 6 expects **NDK r23b** (or the version Unity ships with that editor). Don't manually swap it — let Unity manage it.

---

## 2. Open the project & install packages

1. Clone the repo and open the project folder in Unity Hub.
2. Hub will prompt to open with Unity 6 — accept.
3. First open: Unity will resolve packages. Wait it out.

If the package manifest is empty (fresh project), open `Window → Package Manager` and add:

| Package | Version |
|---|---|
| `com.meta.xr.sdk.all` (Meta XR All-in-One SDK) | v85 or newer |
| `com.unity.xr.interaction.toolkit` | latest 2.x or 3.x compatible with Unity 6 |
| `com.unity.inputsystem` | latest |
| `com.unity.render-pipelines.universal` (URP) | bundled with Unity 6 |

Or paste into `Packages/manifest.json` directly (Unity will resolve on next focus):

```json
{
  "dependencies": {
    "com.meta.xr.sdk.all": "85.0.0",
    "com.unity.xr.interaction.toolkit": "3.0.7",
    "com.unity.inputsystem": "1.11.2",
    "com.unity.render-pipelines.universal": "17.0.4"
  }
}
```

> ⚠️ Replace versions with the actual latest at install time. Don't pin to versions older than what Unity 6 ships with.

When prompted by the **Input System** to switch to the new input system, click **Yes** (Unity will restart).

---

## 3. Project settings (one-time)

### 3.1 Switch platform

`File → Build Profiles → Android → Switch Platform`. Wait for asset reimport.

### 3.2 Player Settings (`Edit → Project Settings → Player`)

**Other Settings:**
- **Color Space:** Linear
- **Auto Graphics API:** OFF
- **Graphics APIs:** Vulkan first, OpenGLES3 as fallback (drag Vulkan above)
- **Scripting Backend:** **IL2CPP**
- **Target Architectures:** **ARM64** only (uncheck ARMv7)
- **Minimum API Level:** **Android 10 (API 29)**
- **Target API Level:** Automatic (highest installed) or 32+
- **Package Name:** e.g., `com.erikhorvath.vrdrony` (must be unique)
- **Active Input Handling:** Input System Package (New)

**Resolution and Presentation:**
- **Default Orientation:** Landscape Left
- **Use 32-bit Display Buffer:** ON
- **Disable Depth and Stencil:** OFF

**Other Settings → Configuration:**
- **Use ARM64 Native Compilation:** ON

### 3.3 Quality / Render Pipeline

`Project Settings → Graphics`: assign the URP Asset (created in Phase 1 under `Assets/_Project/Settings/URP-Quest.asset`).

`Project Settings → Quality`: only one quality level needed for Quest 3. Set it as default and assign the same URP Asset there.

URP Asset settings (Quest profile):
- **MSAA:** 4×
- **HDR:** OFF (cost on tile-based mobile GPUs)
- **Render Scale:** 1.0
- **Main Light Shadows:** Hard, low resolution, fade distance ~30m
- **Additional Lights:** Per Vertex (cheaper) or Disabled
- **Shadow Cascades:** 1

### 3.4 XR Plug-in Management

`Project Settings → XR Plug-in Management`:
- **Initialize XR on Startup:** ON
- **Android tab → Plug-in Providers:** ✅ **Oculus**

`XR Plug-in Management → Oculus` (Android):
- **Stereo Rendering Mode:** **Single Pass Instanced** (or "Multiview")
- **Target Devices:** **Quest 3** ticked. Quest 2 only if zero-cost compatibility is desired.
- **Low Overhead Mode:** ON
- **Symmetric Projection:** ON

### 3.5 Meta XR Project Setup Tool

`Meta → Tools → Project Setup Tool`. Apply **all required** fixes. Apply **recommended** fixes unless one of them turns off something you specifically configured above (e.g., it sometimes wants OpenGLES3 default — keep Vulkan).

### 3.6 OVR Manager & performance flags

These live on the `OVRManager` component (added by Meta XR rig setup, or on the XR Origin's Camera Offset depending on rig variant):

- **Tracking Origin:** Floor
- **Fixed Foveated Rendering:** Medium
- **Use Recommended MSAA Level:** ON (or set MSAA explicitly to 4)
- **Eye Buffer Sharpen Type:** Quality

---

## 4. Folder & scene check

Before first build, verify:

- `Assets/_Project/Scenes/Main.unity` exists and is in **Build Settings → Scenes In Build** as scene 0.
- No console errors. (Warnings about deprecated Meta APIs are usually fine for v85.)

---

## 5. Quest 3 — enable Developer Mode

This is on the **headset side**, one-time per device.

1. **Create a developer organization.** Visit https://developer.oculus.com/ → Sign in with your Meta account → "Create a New Organization". Any name. (Required even for personal/student dev.)
2. **Pair your Quest 3.** Open the **Meta Horizon** mobile app (formerly Oculus app) on your phone, sign in with the same Meta account, pair the headset.
3. **Enable Developer Mode.** In the mobile app: `Devices → [your Quest 3] → Headset Settings → Developer Mode → ON`.
4. **Reboot** the headset.
5. **Plug in via USB-C.** Inside the headset, a permission dialog appears: **"Allow USB debugging?"** — tick **"Always allow from this computer"** and click **Allow**. (You will see this through the headset display, not on the PC.)

### Verify with ADB

ADB ships with Unity (under the Android SDK install). On macOS / Linux, `~/Library/Android/sdk/platform-tools/adb` or wherever Unity placed it; on Windows under `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`.

```bash
adb devices
# expected:
# List of devices attached
# 1WMHHXXXXXXXXX  device
```

If it lists `unauthorized`, re-do the in-headset "Allow" dialog. If empty, replug and check the cable (must be data + power, not charging-only).

---

## 6. Build & Run

### 6.1 Quick path — Build & Run (one click)

1. Plug Quest 3 in via USB.
2. Put the headset on, accept any USB debug prompt.
3. `File → Build Profiles → Android → Build And Run`.
4. Pick an output `.apk` path (e.g., `Builds/VRdrony-dev.apk`).
5. Unity compiles, builds, installs, and auto-launches on the headset. First build: ~5–15 minutes (IL2CPP + shader compile). Subsequent builds: ~30s–2min.

### 6.2 APK only (build, no install)

`File → Build Profiles → Android → Build`. Then sideload manually:

```bash
adb install -r Builds/VRdrony-dev.apk
adb shell am start -n com.erikhorvath.vrdrony/com.unity3d.player.UnityPlayerActivity
```

### 6.3 If build fails

Common failures and fixes:

| Symptom | Fix |
|---|---|
| `Gradle build failed` with NDK error | `Preferences → External Tools` — re-tick "Use Unity-installed" for NDK and SDK. |
| `Unable to merge dex` / `Multiple dex files define ...` | Delete `Library/Bee/` and `Library/PlayerScriptAssemblies/`, reimport. |
| `XR Plugin not found at runtime` | `XR Plug-in Management → Android → Oculus` toggled OFF. Re-enable. |
| `Vulkan device init failed` on headset | Drop Vulkan, use OpenGLES3 only — this is a workaround, file against your driver. |
| `BuildFailedException: Unable to find tools.jar` | OpenJDK module wasn't installed via Hub; reinstall it. |
| App launches then crashes immediately | Check headset's logcat: `adb logcat -s Unity DEBUG ActivityManager` and look for the first `FATAL EXCEPTION`. |

---

## 7. On-device profiling

When MVP gameplay is running, profile before declaring done.

### 7.1 OVR Metrics Tool

Recommended. Sideload from Meta's developer tools page:

```bash
adb install -r OVRMetricsTool.apk
```

In headset → **Settings → Apps → Unknown Sources → OVR Metrics Tool** → enable persistent overlay. Wear the headset, run VRdrony, check FPS and GPU level under peak waves.

Targets:
- 72 Hz **minimum** sustained, 90 Hz preferred.
- App-level GPU < 8 ms.
- Stale frames (re-projection) < 1 % per minute.

### 7.2 Unity Profiler over USB

`Window → Analysis → Profiler`. Select **AndroidPlayer (your Quest)** in the Connected Player dropdown after `Build And Run` with **Development Build + Autoconnect Profiler** ticked. Don't ship the Development Build to anyone — it's slower.

### 7.3 RenderDoc / Snapdragon Profiler

Only if OVR Metrics flags a GPU bottleneck and Unity Profiler isn't enough. Out of scope for MVP unless we miss frame rate.

---

## 8. Versioning & repo hygiene

- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `obj/`, `Builds/`, `UserSettings/`, `MemoryCaptures/`. Use Unity's stock `.gitignore` template.
- **Git LFS** for any binary asset > 5 MB (FBX, large textures, audio). Set up `.gitattributes` early — moving binaries into LFS retroactively rewrites history.
- Commit `Packages/manifest.json` and `Packages/packages-lock.json`.
- **Don't** commit `Library/` — Unity rebuilds it.

---

## 9. Distribution (post-MVP)

For MVP we ship an unsigned dev APK that runs on dev-mode-enabled Quests via sideload. No store submission. App Lab / Quest Store submission is post-MVP and out of scope.
