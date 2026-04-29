# Phase 1 — Foundation Checklist

End state: black scene with the XR Origin rig boots on Quest 3, head tracking works, no console errors. Commit message prefix: `phase1: foundation`.

> The bootstrap script does ~70% of the click-through. The remaining 30% are wizard-driven steps that benefit from a human eye.

---

## Step 1 — Open the project

1. Make sure Unity Hub has **Unity 6 (6000.0.32f1 or newer 6000.x)** installed with Android Build Support, OpenJDK, and Android SDK & NDK Tools modules. If not, install via Hub before continuing.
2. Open Unity Hub → **Add → Add project from disk** → pick `ProjektZaverecny/VRdrony/`.
3. Click the project to open. Unity will:
   - Resolve packages (Meta XR SDK, XRI, URP, etc.) — this takes a few minutes on first open.
   - Prompt to switch to the new **Input System** — click **Yes**, Unity restarts.
   - Possibly prompt to upgrade asmdefs / re-import — accept.
4. Wait until the console is idle. **Errors must be resolved before continuing.** Yellow warnings about deprecated Meta APIs are expected and OK.

> If package resolution fails (no internet to package registry, version not found), open `Packages/manifest.json` and update the version pins. Most likely cause: Meta XR SDK released a newer version than `85.0.0` and removed the old one — replace with the latest stable.

---

## Step 2 — Run the bootstrap script

1. In the menu bar, click **DroneDefense → Phase 1 → Run All**.
2. Wait for the popup confirming "Phase 1 — Bootstrap complete".
3. Check the **Console** for `[Phase1]` log lines — every step should report success.

What this did automatically:
- Switched active build target to Android.
- Applied Quest 3 player settings (Linear color space, IL2CPP, ARM64, API 29, Vulkan + OpenGLES3, package name `com.erikhorvath.vrdrony`).
- Created `Assets/_Project/Scenes/Main.unity` and added it to Build Settings as scene 0.

What's left for you to do manually (the popup lists these too):

---

## Step 3 — Create the URP asset (manual, ~30 sec)

1. In the **Project** window, navigate to `Assets/_Project/Settings/`.
2. Right-click → **Create → Rendering → URP Asset (with Universal Renderer)**.
3. Name it **`URP-Quest`**. Unity creates two assets: `URP-Quest.asset` and a paired `URP-Quest_Renderer.asset`.

### Configure URP for Quest 3

Select **`URP-Quest.asset`**. In the inspector:

| Section | Field | Value |
|---|---|---|
| Quality | Anti-aliasing (MSAA) | **4×** |
| Quality | HDR | **OFF** |
| Quality | Render Scale | **1.0** |
| Lighting → Main Light | Cast Shadows | ON, Hard, Low resolution |
| Lighting → Main Light | Shadow Distance | 30 |
| Lighting → Additional Lights | Per Object | Disabled (or Per Vertex) |
| Shadows | Cascade Count | 1 |
| Post-processing | Enabled | OFF for now (turn on in Phase 8 only if you need vignette) |

### Assign URP asset

1. **Edit → Project Settings → Graphics**: drag `URP-Quest` into **Default Render Pipeline**.
2. **Edit → Project Settings → Quality**: select your single quality level → assign `URP-Quest` to **Render Pipeline Asset**.

---

## Step 4 — Enable Oculus XR plug-in (manual, ~20 sec)

1. **Edit → Project Settings → XR Plug-in Management**.
2. If it asks to install the package, click **Install**.
3. Switch to the **Android** tab → tick **Oculus**.
4. Click into **Oculus** subsection → set:
   - **Stereo Rendering Mode:** Single Pass Instanced (a.k.a. Multiview)
   - **Target Devices:** ✅ Quest 3 (uncheck others if you don't need them)
   - **Low Overhead Mode:** ON
   - **Symmetric Projection:** ON

---

## Step 5 — Run Meta XR Project Setup Tool (manual, ~1 min)

1. **Meta → Tools → Project Setup Tool**.
2. Click **Fix All** under **Required**.
3. Click **Apply All** under **Recommended** — *but* if any recommended fix wants to:
   - Change graphics APIs back to OpenGLES3 only → **skip it**, we keep Vulkan first.
   - Disable IL2CPP → **skip it**.
4. Re-run the tool until it shows zero outstanding required issues.

---

## Step 6 — Add the XR Origin to Main.unity (manual, ~1 min)

1. Open **`Assets/_Project/Scenes/Main.unity`** (double-click).
2. In the Hierarchy, **delete the default `Main Camera`** (XR Origin includes its own camera).
3. **Window → Package Manager → XR Interaction Toolkit → Samples** → import **Starter Assets** (gives you InputActions + presets) and **XR Device Simulator** (optional, for editor testing without headset).
4. Hierarchy → right-click → **XR → XR Origin (XR Rig)**. Confirm the rig appears with `Camera Offset → Main Camera + LeftHand Controller + RightHand Controller`.
5. On the **XR Origin** GameObject:
   - **Tracking Origin Mode:** Floor.
6. Save the scene (`Cmd/Ctrl+S`).

> If the **XR → XR Origin (XR Rig)** menu doesn't exist, XRI didn't import correctly. Re-check Package Manager and that XRI samples are imported.

---

## Step 7 — Verify

1. **DroneDefense → Phase 1 → Verify Setup**.
2. Check the Console — every line should say `[OK]`. `[DRIFT]` means something doesn't match expected; re-run the relevant step.

---

## Step 8 — First device build (smoke test)

1. Plug in Quest 3 via USB. Put on the headset, accept the **Allow USB debugging** prompt.
2. Verify with `adb devices` → your headset's serial appears as `device` (not `unauthorized`).
3. **File → Build Profiles → Android → Build And Run**.
4. Save the APK as `Builds/VRdrony-phase1.apk`. First IL2CPP build takes 5–15 min.
5. The app auto-launches on the headset. You should see a black scene.
6. **Acceptance criteria for Phase 1:**
   - The app launches without crashing.
   - Head tracking works — looking around moves the view.
   - Controllers are visible (or at least the rig boots without errors).
   - On-device console (via `adb logcat -s Unity`) shows no `FATAL EXCEPTION`.

---

## Step 9 — Commit

Once the smoke test passes:

```bash
cd ProjektZaverecny/VRdrony
git add .
git commit -m "phase1: foundation — Unity 6 + URP + XRI + Meta XR + Quest 3 settings"
git push
```

> **Don't push the APK** — it's gitignored. Don't worry if `Library/` shows up as a huge diff: it's also gitignored, just confirm `git status` is clean of `Library/` paths before committing.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `Package com.meta.xr.sdk.all@85.0.0 not found` | Meta released newer, removed old version | Bump pin in `Packages/manifest.json` to latest stable |
| `Build target group Android is not installed` | Android module missing from Unity install | Unity Hub → Installs → ⚙️ on Unity 6 → Add Modules → Android Build Support |
| Black screen on headset, no head tracking | Oculus XR plug-in not enabled OR scene has no XR Origin | Repeat Steps 4 and 6 |
| `IL2CPP build failed` with NDK error | Wrong NDK version | Preferences → External Tools → re-tick "Use Unity-installed" for NDK |
| Verify reports `Active build target: StandaloneOSX/Windows` | Bootstrap was run before Android module was installed | Install Android module → re-run **DroneDefense → Phase 1 → 1. Switch Platform to Android** |

---

## What's intentionally deferred to later phases

- Castle wall, tower, ground geometry — Phase 2.
- Player movement clamp — Phase 3.
- Weapons — Phase 4.
- Health & damage — Phase 5.
- Drones — Phase 6.
- Wave loop & GameManager — Phase 7.
- World-space UI — Phase 8.
- Spatial audio — Phase 9.
- Profiling pass + final APK — Phase 10.
