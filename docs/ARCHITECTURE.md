# VRdrony — Architecture

> Single-player VR tower defense for Meta Quest 3. Player stands on a castle wall, strafes sideways, and uses two hand-held weapons to destroy waves of incoming drones before they reach the tower.

This document is the contract between the spec and the code. Every decision here has a *why*. If a future change contradicts it, update this file in the same commit.

---

## 1. Stack & target

| Item | Choice | Why |
|---|---|---|
| Engine | Unity **6** (6000.x LTS-track) | Spec requirement. Long-term Meta XR SDK alignment. |
| Render pipeline | **URP** (Universal Render Pipeline) | Quest 3 GPU budget; URP is the only sane choice for mobile XR in Unity 6. Set up in Phase 1, not Phase 10. |
| XR rig | **XR Origin (XR Interaction Toolkit)** + Meta XR SDK All-in-One v85+ | Cleaner input handling, better long-term Unity compatibility, and we still get Meta features (passthrough, hand tracking, OVRManager perf knobs) by including the All-in-One package. We don't mix `OVRCameraRig` with `XR Origin` — pick one and stick with it. |
| Input | Unity Input System + XRI action-based input | Action assets decouple controls from device. |
| Target device | Meta Quest 3 (primary). Quest 2 only kept if zero cost. | Spec. Avoids designing around Quest 2 GPU ceiling. |
| Tracking origin | Floor | Real player height drives view; no forced height. |
| Min Android API | 29 | Required by recent Meta XR SDK. |
| Architecture | ARM64 only, IL2CPP | Quest store requirement; IL2CPP gives best perf. |

### Reference videos

`VR-01.mp4` (5:12) and `VR2-controllers.mp4` (1:50) are uploaded but are **silent screen recordings** — no audio track, so I have not transcribed them. They are kept as visual references only. If they describe a base setup workflow that should override these defaults, flag a timestamp and I'll re-derive specific decisions from the relevant frames.

---

## 2. Folder layout

```
Assets/
└── _Project/                     # everything we author lives here, prefixed _ to sort top
    ├── Scripts/
    │   ├── Core/                 # GameManager, GameState, EventChannel SOs
    │   ├── Player/               # WallBoundLocomotion, PlayerRig
    │   ├── Weapons/              # Weapon, WeaponData, RangedWeapon, MeleeWeapon, Projectile
    │   ├── Combat/               # IDamageable, Health
    │   ├── Enemies/              # Drone, DroneAI, DroneData, DronePool
    │   ├── Waves/                # WaveSpawner, WaveData, WaveSequence
    │   ├── UI/                   # TowerHpBar, WaveTablet, GameOverPanel, BillboardToPlayer
    │   ├── Audio/                # AudioCue, OneShotAudioPlayer
    │   └── Utils/                # math helpers, debug overlays
    ├── Prefabs/
    │   ├── Player/               # XR Origin variant, hand controllers
    │   ├── Weapons/              # Crossbow, Arrow, Sword
    │   ├── Drones/               # KamikazeDrone (single MVP variant)
    │   ├── Environment/          # CastleWall, Tower, Ground
    │   ├── VFX/                  # ExplosionFX, HitFX, MuzzleFX
    │   └── UI/                   # TowerHpBar, WaveTablet, GameOverPanel
    ├── Scenes/
    │   └── Main.unity            # the only gameplay scene
    ├── Materials/
    ├── Audio/                    # source clips
    ├── Settings/                 # URP assets, XR settings, Input Actions
    └── ScriptableObjects/        # SO instances (data/configs/event channels)
```

**Convention.** `_Project` keeps our content visually separated from third-party packages (Meta XR, XRI, etc.) which install under `Packages/` or get imported into `Assets/Oculus`, `Assets/XR`, etc. We do not edit those vendor folders.

---

## 3. Namespaces

All gameplay code is under `DroneDefense.*`. One class per file. `[SerializeField] private` for inspector fields, never `public` fields.

| Namespace | Contents |
|---|---|
| `DroneDefense.Core` | `GameManager`, `GameState`, `EventChannel<T>` SOs |
| `DroneDefense.Player` | `WallBoundLocomotion`, `PlayerRig` |
| `DroneDefense.Weapons` | `Weapon` (abstract), `WeaponData`, `RangedWeapon`, `MeleeWeapon`, `Projectile` |
| `DroneDefense.Combat` | `IDamageable`, `Health` |
| `DroneDefense.Enemies` | `Drone`, `DroneAI`, `DroneData`, `DronePool` |
| `DroneDefense.Waves` | `WaveSpawner`, `WaveData`, `WaveSequence` |
| `DroneDefense.UI` | `TowerHpBar`, `WaveTablet`, `GameOverPanel`, `BillboardToPlayer` |
| `DroneDefense.Audio` | `AudioCue`, `OneShotAudioPlayer` |

---

## 4. Core systems

### 4.1 GameManager (singleton)

Owns the game state machine. Lightweight singleton (`GameManager.Instance`, set in `Awake`, `DontDestroyOnLoad` not needed because we have one scene).

```csharp
public enum GameState { Idle, Playing, GameOver, Victory }
```

Public surface:
- `GameState State { get; }`
- `event Action<GameState> OnStateChanged`
- `void StartGame()`, `void GameOver()`, `void Victory()`, `void Restart()`
- `Health TowerHealth { get; }` — assigned in inspector

`Restart()` reloads the active scene. Nothing fancy.

### 4.2 EventChannel ScriptableObjects

Used for decoupled cross-system signals (UI listening to gameplay, audio listening to combat, etc.). Each event channel is a `ScriptableObject` exposing `Raise(payload)` and a C# `event` listeners subscribe to in `OnEnable` / `OnDisable`.

Initial channels:
- `DroneSpawnedEvent : EventChannel<Drone>`
- `DroneKilledEvent  : EventChannel<Drone>`
- `TowerDamagedEvent : EventChannel<DamageInfo>`
- `WaveStartedEvent  : EventChannel<int>`     // wave index
- `WaveClearedEvent  : EventChannel<int>`
- `GameStateChangedEvent : EventChannel<GameState>`

**Why SO events over a global event bus or DI:** zero framework overhead, inspector-discoverable, lets us reuse listeners across prefabs without scene-specific wiring. **Why not `UnityEvent` everywhere:** SO events survive scene reloads and are easier to wire from prefabs that don't have direct refs to scene objects.

### 4.3 Health & damage

```csharp
public interface IDamageable
{
    void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal);
}
```

`Health : MonoBehaviour, IDamageable` carries:
- `[SerializeField] private float maxHealth;`
- `float CurrentHealth { get; }`
- `UnityEvent OnDeath`, `UnityEvent<float> OnDamaged` (wired in inspector)

Tower's `OnDeath` → `GameManager.GameOver()`. Drone's `OnDeath` → drone returns itself to the pool and raises `DroneKilledEvent`.

### 4.4 Player movement (constrained strafe)

`WallBoundLocomotion : MonoBehaviour` — Option A from the spec.

- Reads left thumbstick **X-axis only** via XRI `InputActionProperty`. Y is ignored.
- Translates the XR Origin along the wall's **local X axis** at ~2 m/s.
- Clamps origin position to `[wallCenter.x - wallLength * 0.5f + margin, wallCenter.x + wallLength * 0.5f - margin]`.
- Snap turn / continuous turn: **disabled**. Physical head turn only.

We don't use XRI's stock `ContinuousMoveProvider` because we need a 1-axis clamp; writing 30 lines is simpler than wrestling its constraints.

### 4.5 Weapons

```
Weapon (abstract MonoBehaviour)
├── RangedWeapon  // crossbow — projectile-based
└── MeleeWeapon   // sword — collider + tip-velocity threshold
```

- Held via `XRGrabInteractable` (XRI). Fixed to hands at scene start; no weapon swap in MVP.
- `WeaponData` SO: damage, fireRate, range, recoil, audio cues, haptic profile.
- `Weapon.Fire()` is virtual; trigger button on the holding hand calls it through an XRI `ActivateInteractable` event.
- `RangedWeapon` spawns an arrow prefab (Rigidbody, ~40 m/s forward velocity). Arrow has `Projectile` which on collision finds `IDamageable` on the hit collider's rigidbody/parent and calls `TakeDamage`. Arrows pool too — small pool, e.g., 16 arrows.
- `MeleeWeapon` samples blade-tip world position in `FixedUpdate`, computes tip velocity, only deals damage when `tipSpeed > meleeMinSpeed` (e.g., 2 m/s). On hit: haptic pulse via XRI haptic impulse player, hit FX, hit sound.

### 4.6 Drones

Single MVP type — kamikaze.

- `Drone` (MonoBehaviour): owns `Health`, audio source, `Rigidbody` (kinematic; we move via transform).
- `DroneAI`: state machine — `Approach`, `Attack`, `Dead`. `Approach` uses `Vector3.MoveTowards` toward `tower.position`, with sine-wave Y bob (small amplitude, ~0.2m). `Attack` triggers on collision with the tower: deal contact damage, raise explosion, return to pool. NavMesh is not used — flying enemies, straight lines.
- `DroneData` SO: speed, hp, damage, bobAmplitude, bobFrequency. Per-wave scaling multiplies these.

### 4.7 Wave spawner & object pool

- `DronePool : MonoBehaviour` — pre-instantiates N drones at `Awake`, deactivates them. `Spawn(position, rotation, data)` activates one and resets its state. `Return(drone)` deactivates it. Pool size ≥ peak concurrent drones across all waves (e.g., 32).
- `WaveData` SO: drone count, spawn interval, speed multiplier, hp multiplier.
- `WaveSequence` SO: ordered list of `WaveData`. MVP: 5 waves, increasing difficulty.
- `WaveSpawner`: drives the sequence. On wave start, spawns drones over time from random points on a spawn arc 30–50 m in front of the wall. 5–10 s breather between waves. After last wave clears → `GameManager.Victory()`.

### 4.8 UI

All world-space (screen-space UI doesn't exist in VR).

- **TowerHpBar** — world-space `Canvas` parented to a billboard pivot above the tower. `BillboardToPlayer` rotates Y-only toward HMD. Bound to `Health.OnDamaged`.
- **WaveTablet** — world-space canvas mounted on the wall in front of the player. Shows current wave + drones remaining. Bound to `WaveStartedEvent` / `DroneKilledEvent`.
- **GameOverPanel / VictoryPanel** — world-space panel that fades in on state change. Restart button is an XRI poke-interactable button (not a ray-only target — pokeable feels right at this distance).

### 4.9 Audio

- All combat / drone / weapon clips: 3D, spatial blend = 1.0.
- Drone rotor hum: looping `AudioSource` on each drone, attenuated.
- Ambient wind: 2D, low volume, single source on `GameSystems`.
- Use Meta XR Audio SDK if installed (better HRTF on Quest); otherwise Unity's built-in spatializer.
- `AudioCue` SO + `OneShotAudioPlayer` MonoBehaviour for fire-and-forget SFX. Avoids `AudioSource.PlayClipAtPoint` GC churn.

---

## 5. Scene hierarchy (Main.unity)

```
Main (scene)
├── ── Lighting ──
│   ├── Directional Light (sun, real-time on static geo only, baked GI for dynamic-light receivers)
│   └── Light Probes Group
├── ── Environment ──
│   ├── CastleWall            (20m × 3m × 1m, with crenellation boxes)
│   ├── Tower                 (cylinder ~10m tall, has Health, has TowerHpBar child)
│   ├── Ground                (textured plane below the wall)
│   └── Skybox volume / Reflection probe
├── ── Player ──
│   └── XR Origin (XR Rig)
│       ├── Camera Offset
│       │   ├── Main Camera   (HMD)
│       │   ├── LeftHand Controller   → holds Sword prefab
│       │   └── RightHand Controller  → holds Crossbow prefab
│       └── (WallBoundLocomotion attached at the rig root)
├── ── Game Systems ──
│   ├── GameManager
│   ├── WaveSpawner (refs WaveSequence SO, refs DronePool)
│   ├── DronePool   (refs Drone prefab)
│   └── AudioMaster
├── ── UI ──
│   ├── WaveTablet  (world canvas on the wall)
│   └── GameOverPanel (disabled at start)
└── ── Spawn Markers ──
    └── DroneSpawnArc (empty transforms defining the arc)
```

Tower's `Health.OnDeath` is wired in the inspector to `GameManager.GameOver`. WaveSpawner subscribes to `DroneKilledEvent` to know when a wave is cleared. UI listens to event channels — no direct references back from gameplay.

---

## 6. Communication pattern (one-page summary)

```
┌──────────────┐  raises   ┌────────────────────┐  listens  ┌──────────────┐
│  Gameplay    │──────────▶│  EventChannel SOs  │──────────▶│  UI / Audio  │
│  (Drone,     │           │  (DroneKilled,     │           │  (HpBar,     │
│   Weapon,    │           │   TowerDamaged,    │           │   Tablet,    │
│   Wave...)   │           │   WaveStarted...)  │           │   AudioCue)  │
└──────┬───────┘           └────────────────────┘           └──────────────┘
       │
       │  direct ref (inspector-wired UnityEvent)
       ▼
┌──────────────┐
│  GameManager │  state machine, holds tower ref
└──────────────┘
```

Rules:
1. **No `FindObjectOfType` at runtime.** Inspector-wire references or get them through SO event channels.
2. **No DI framework.** A singleton `GameManager` and SO event channels cover MVP cleanly.
3. **One direction of data flow per system.** Gameplay raises events; UI/Audio consume. UI does not call into gameplay (except the Restart button which calls `GameManager.Restart()`).

---

## 7. Performance posture (Quest 3 budget)

Targets: **72 Hz minimum**, 90 Hz preferred. CPU < 4 ms, GPU < 8 ms per frame.

Already baked into the architecture:
- **URP** from Phase 1.
- **GPU instancing** on shared materials (drone body, arrow, environment).
- **Fixed Foveated Rendering** Medium via `OVRManager`.
- **MSAA 4×** in URP asset (cheap on Quest, big visual win).
- **Object pooling** for drones and arrows — no `Instantiate`/`Destroy` per shot or per spawn.
- **No real-time shadows** on dynamic objects. Sun shadows on static geo only, baked.
- **Single Pass Instanced** stereo rendering.

We profile with **OVR Metrics Tool** on device before declaring MVP done.

---

## 8. Build order & commit checkpoints

The 10 phases from the spec are the work breakdown. Each phase ends with a commit and a "does it still compile, no console errors?" sanity check.

| Phase | Output | Commit message prefix |
|---|---|---|
| 1 | Project + XR foundation + URP, black scene boots on Quest 3 | `phase1: foundation` |
| 2 | Wall, tower, ground, lighting, player spawn | `phase2: environment` |
| 3 | `WallBoundLocomotion` working, can't fall off | `phase3: locomotion` |
| 4 | Crossbow + sword, both functional | `phase4: weapons` |
| 5 | `IDamageable`, `Health`, tower takes damage | `phase5: damage` |
| 6 | Drone + DroneAI + pool, can be killed, can damage tower | `phase6: enemies` |
| 7 | `GameManager`, `WaveSpawner`, 5 waves, victory state | `phase7: gameloop` |
| 8 | World-space UI, hit feedback, game over / victory panels | `phase8: ui` |
| 9 | Spatial audio across the board | `phase9: audio` |
| 10 | Profiling pass, FFR, MSAA, instancing, ship APK | `phase10: perf+build` |

---

## 9. Out of MVP

Tracked in `docs/POST_MVP.md` (created in Phase 10 with the final list). Already known to be deferred:

- Multiple drone types (shooter, tanky, fast)
- Weapon swapping / weapon rack on the wall
- Persistent score / leaderboard
- Settings menu (volume, comfort options)
- Multiplayer / Photon (explicitly not in scope)
- Hand tracking weapon manipulation (controllers only for MVP)
- Per-arrow physics quiver / reload mechanic

---

## 10. Open questions for the user

These don't block ARCHITECTURE.md but will shape Phase 1+:

1. **Both `/ProjektZaverecny/VRdrony/` and `/VRdrony/` are mounted and point to the same git remote.** I'm using `/ProjektZaverecny/VRdrony/` as the project root. Confirm OK or pick the other.
2. **Hand assignment:** spec leaves it open. I'll go with **sword on left**, **crossbow on right** (right-handed default — primary aiming on dominant hand). Speak up if you want it flipped or configurable.
3. **The reference videos are silent.** If they encode a setup workflow you want me to follow (specific package versions, specific OVRManager toggles, a particular import order), give me a couple of timestamps and I'll inspect frames.
