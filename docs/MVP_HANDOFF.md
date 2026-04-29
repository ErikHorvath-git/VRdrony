# MVP Handoff — exact click sequence

The runtime gameplay code is fully authored under `Assets/_Project/Scripts/`.
The editor automation script `Assets/_Project/Scripts/Editor/MVPBootstrap.cs`
builds the entire game scene (environment + drones + game logic + UI) from
primitives when you click **DroneDefense → MVP → Build Scene**.

This doc is the click-by-click path from where the project currently is
(Unity open, platform Android, Quest 3 player settings applied,
`Main.unity` in Build Settings, `URP-Quest.asset` exists in
`Assets/_Project/Settings/`) to a playable APK on Quest 3.

---

## 1. Finish Phase 1 wizard steps (~5 min)

These are the manual steps from `docs/PHASE1_CHECKLIST.md` that the
bootstrap script can't do for you because they're wizard-driven.

### 1a. Configure the URP-Quest asset

1. **Project window** → `Assets/_Project/Settings/`.
2. Click **`URP-Quest`** (the one with the blue ball icon — not the `_Renderer` companion).
3. **Inspector** → set:
   - **Quality → Anti-Aliasing (MSAA):** 4×
   - **Quality → HDR:** OFF
   - **Quality → Render Scale:** 1.0
   - **Lighting → Main Light Cast Shadows:** ON, Hard, Low
   - **Lighting → Main Light Shadow Distance:** 30
   - **Shadows → Cascade Count:** 1
   - **Post-processing → Enabled:** OFF (turn on later if you want vignette)

### 1b. Assign URP-Quest in Project Settings

1. **Edit → Project Settings → Graphics** → drag `URP-Quest` into **Default Render Pipeline**.
2. **Edit → Project Settings → Quality** → for the Android level, drag `URP-Quest` into **Render Pipeline Asset**.

### 1c. Enable Oculus XR Plug-in (Android)

1. **Edit → Project Settings → XR Plug-in Management**.
2. If asked to install, click **Install XR Plug-in Management**.
3. Switch to the **Android** tab → tick **Oculus**.
4. Click into the **Oculus** subsection → set:
   - **Stereo Rendering Mode:** Single Pass Instanced
   - **Target Devices:** ✅ Quest 3 (uncheck others)
   - **Low Overhead Mode:** ON
   - **Symmetric Projection:** ON

### 1d. Run Meta XR Project Setup Tool

1. **Meta → Tools → Project Setup Tool**.
2. Click **Fix All** under Required.
3. Click **Apply All** under Recommended **but skip** any item that:
   - Reverts Graphics APIs to OpenGLES3 only (we keep Vulkan first).
   - Disables IL2CPP.
4. Re-run until zero outstanding required issues.

### 1e. Add the XR Origin to Main.unity

1. **Project window** → double-click `Assets/_Project/Scenes/Main.unity`.
2. **Hierarchy** → delete the default `Main Camera` (XR Origin brings its own).
3. **Window → Package Manager → XR Interaction Toolkit → Samples** →
   import **Starter Assets** (gives you the InputActions presets).
4. **Hierarchy** → right-click empty area → **XR → XR Origin (XR Rig)**.
5. Select the new **XR Origin (XR Rig)** in Hierarchy → in Inspector:
   - **Tracking Origin Mode:** Floor
6. Position the rig so it's roughly on top of where the wall will be:
   `Position = (0, 1, -1.5)` works well with the generated environment.
7. Save the scene (`Ctrl+S`).

### 1f. Verify

**DroneDefense → Phase 1 → Verify Setup** — every line in the Console should say `[OK]`.

---

## 2. Build the MVP scene (~5 sec)

1. **DroneDefense → MVP → Build Scene**.
2. A dialog confirms what was built. Click OK.
3. Hierarchy now contains an `MVP_Generated` root with the environment,
   drone spawn points, GameManager, WaveSpawner, tower HP bar, and wave counter.
4. The drone prefab was saved to `Assets/_Project/Prefabs/Drones/KamikazeDrone.prefab`.
5. Save the scene (`Ctrl+S`).

The script is **idempotent** — if you re-run it, it deletes the prior
`MVP_Generated` root and rebuilds. Your XR Origin is left alone.

---

## 3. (Optional, recommended) Smoke-test in editor

1. **Window → Package Manager → XR Interaction Toolkit → Samples** →
   import **XR Device Simulator** if you haven't.
2. Drag `XR Device Simulator` prefab into the scene.
3. Press **Play**. Use WASD + mouse to fake head movement; you'll see the
   tower, drones spawning, the wave counter ticking.
4. Stop play before building.

If a drone reaches the tower, the tower HP bar drains. If HP hits zero,
the GameManager triggers GameOver (auto-restart in 4s — see
`GameManager.restartDelaySeconds`).

---

## 4. Build & deploy to Quest 3 (~5–15 min for first IL2CPP build)

1. Plug Quest 3 in via USB-C, put the headset on, accept **Allow USB debugging**.
2. Verify with `adb devices` (Git Bash works fine) — your headset's serial
   should appear as `device`, not `unauthorized`.
3. **File → Build Profiles → Android → Build And Run**.
4. Save the APK as `Builds/VRdrony-mvp.apk`.
5. The app auto-launches on the headset. You should see:
   - The castle wall, ground, and tower in front of you.
   - The HP bar floating above the tower.
   - "Wave 1 / 5" text appearing.
   - Drones approaching from behind / above the tower.
6. Drones currently fly toward the tower and explode on contact —
   damaging it. Until you wire weapons (see §5) you can't shoot back.

---

## 5. Wiring the weapons (manual, ~5 min)

Weapons aren't auto-attached because they need to live under the XR
Origin's hand controllers, which the bootstrap can't reliably find by
name across XRI versions.

### 5a. Sword (right hand)

1. In the Hierarchy, expand **XR Origin → Camera Offset → Right Controller** (or `Right Hand`/`RightHand Controller` depending on XRI version).
2. Right-click the controller → **3D Object → Cube**. Name it `Sword`.
3. Set Transform: `Position (0, 0, 0.4)`, `Rotation (0, 0, 0)`, `Scale (0.05, 0.05, 0.6)` — long thin blade.
4. Add Component → `Rigidbody` (set Is Kinematic ✅, Use Gravity ❌).
5. Add Component → `MeleeWeapon` (script).
6. Create a child empty `TipPoint` at local `(0, 0, 0.3)`. Drag it into the sword's `Tip Point` field.
7. The cube's `Box Collider` is already there — set **Is Trigger** ✅. Drag the collider into the sword's `Blade Collider` field.

### 5b. Crossbow (left hand)

1. Under **Left Controller**, create a new empty `Crossbow`.
2. Add an empty child `Muzzle` at local `(0, 0, 0.2)`.
3. Add Component → `RangedWeapon`.
4. Drag `Assets/_Project/Prefabs/Drones/KamikazeDrone.prefab` into… wait,
   that's the drone. You need an Arrow projectile prefab. Quick build:
   - Create a Cube primitive in the scene.
   - Scale it to `(0.05, 0.05, 0.4)`. Tint dark grey.
   - Add `Rigidbody` (Use Gravity ✅, Is Kinematic ❌).
   - Add `Box Collider`.
   - Add the `Projectile` script.
   - Drag it into `Assets/_Project/Prefabs/Weapons/Arrow.prefab`. Delete from scene.
5. Drag `Arrow.prefab` into the crossbow's `Projectile Prefab` field.
6. Drag `Muzzle` into `Muzzle` field.
7. To fire, bind the controller's trigger to call `RangedWeapon.Fire()` —
   simplest: use **XR Direct Interactor → Activate** event on the controller,
   wire to `RangedWeapon.Fire`. Or in editor, press Space to fire (legacy Input).

(Phase 4 of the original plan was supposed to do this with Input
Actions and proper weapon prefabs. The current MVP gets you there with
manual wiring — extracting it to an editor automation script is the
natural next iteration.)

---

## 6. Commit

```bash
cd C:/Users/erikh/Documents/VRskolskeVeci/ProjektZaverecny/VRdrony
git add Assets/_Project/Scripts Assets/_Project/Settings Assets/_Project/Scenes Assets/_Project/Prefabs ProjectSettings docs Packages
git status            # sanity-check that Library/ is NOT staged
git commit -m "phase1+mvp: foundation, environment, weapons stubs, drones, wave loop"
git push
```

If `git status` shows `.git/index.lock` blocking, run `rm -f .git/index.lock` first (sandbox left a stale lock; see earlier in this thread).

---

## 7. What's NOT in the MVP (deferred)

- **Audio** (drone whirr, sword whoosh, crossbow twang). Phase 9.
- **VFX** (explosion particles, hit sparks, muzzle flash). Phase 9.
- **Polished art** — everything is primitives. Phase 2 expansion.
- **Wave config asset** — waves are hardcoded in `WaveSpawner` fields. Move
  to `ScriptableObject` config for designer iteration. Phase 7 expansion.
- **GameOverPanel UI** — currently the scene auto-restarts after 4s; no
  visible "Game Over" splash.
- **Hand pose presets, IK, hand models** — controller models from XRI
  Starter Assets are used as-is.
- **Performance profiling pass** (OVR Metrics overlay, Burst, GPU
  Instancer). Phase 10.
- **Object pooling for drones** — current code Instantiates/Destroys.
  Adequate for MVP wave sizes (3-13 drones at peak); pool when Phase 10
  hits the 72fps target on Quest 3.

The architecture in `docs/ARCHITECTURE.md` and the original 10-phase
plan describe how each of these slots in.
