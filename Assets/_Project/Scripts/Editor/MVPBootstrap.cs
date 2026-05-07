// MVPBootstrap — builds the entire VR drone-defense MVP scene
// programmatically. Run from menu: DroneDefense > MVP > Build Scene.
//
// Idempotent: re-running deletes the prior MVP_Generated root and
// rebuilds. An existing XR Origin is preserved (and used as the
// player rig); otherwise we build a non-VR FPS fallback so the
// game is playable in editor without a headset.
//
// What this builds (one click):
//   - Environment (ground, wall, tower)
//   - Spawn points
//   - Drone prefab (Assets/_Project/Prefabs/Drones/KamikazeDrone.prefab)
//   - Drone explosion VFX prefab (Assets/_Project/Prefabs/VFX/...)
//   - Arrow + Crossbow + Sword prefabs (Assets/_Project/Prefabs/Weapons/...)
//   - Player rig on top of the tower (FPS fallback OR XR Origin)
//   - Crossbow attached to camera/right hand, sword on left hand
//   - GameManager + ScoreManager + WaveSpawner
//   - Dashboard: tower HP bar, wave counter, score, combo,
//     game-over message — all world-space TextMesh primitives

using System.IO;
using DroneDefense.Combat;
using DroneDefense.Core;
using DroneDefense.Effects;
using DroneDefense.Enemies;
using DroneDefense.Player;
using DroneDefense.UI;
using DroneDefense.Waves;
using DroneDefense.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneDefense.Editor
{
    public static class MVPBootstrap
    {
        private const string GeneratedRootName = "MVP_Generated";

        private const string DronePrefabPath          = "Assets/_Project/Prefabs/Drones/KamikazeDrone.prefab";
        private const string ExplosionPrefabPath      = "Assets/_Project/Prefabs/VFX/DroneExplosion.prefab";
        private const string ArrowPrefabPath          = "Assets/_Project/Prefabs/Weapons/Arrow.prefab";
        private const string CrossbowPrefabPath       = "Assets/_Project/Prefabs/Weapons/Crossbow.prefab";
        private const string SwordPrefabPath          = "Assets/_Project/Prefabs/Weapons/Sword.prefab";
        private const string EnemyProjectilePrefabPath = "Assets/_Project/Prefabs/Drones/EnemyProjectile.prefab";

        // Gun model imported by the user (PolyOne / Free Gun pack). If
        // present, the "crossbow" is built around this model instead of
        // procedural cubes — gives us a real-looking weapon and sidesteps
        // the URP/Standard material confusion in cube primitives.
        private const string GunModelPrefabPath  = "Assets/PolyOne/Free Gun/Prefabs/SM_Ak47.prefab";

        // Tower geometry constants — used to position both the tower and
        // the player rig that stands on top of it.
        private static readonly Vector3 TowerCenter   = new Vector3(0f, 2.5f, -3.5f);
        private const float TowerHeight = 5f;     // cylinder primitive is 1m * scale.y(2.5) * 2 = 5m tall
        private const float PlayerEyeHeight = 1.6f;

        // -------------------------------------------------------------
        [MenuItem("DroneDefense/MVP/Build Scene", priority = 20)]
        public static void BuildScene()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Drones");
            EnsureFolder("Assets/_Project/Prefabs/VFX");
            EnsureFolder("Assets/_Project/Prefabs/Weapons");

            // Wipe any prior generated root so we don't accumulate dupes.
            var existing = GameObject.Find(GeneratedRootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Build MVP");

            // ---- Environment ----------------------------------------
            var environment = new GameObject("Environment");
            environment.transform.SetParent(root.transform);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(environment.transform);
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            Tint(ground, new Color(0.18f, 0.42f, 0.18f));

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "CastleWall";
            wall.transform.SetParent(environment.transform);
            wall.transform.position = new Vector3(0f, 1.25f, -8f);
            wall.transform.localScale = new Vector3(20f, 2.5f, 0.6f);
            Tint(wall, new Color(0.55f, 0.5f, 0.45f));

            var towerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerBase.name = "Tower";
            towerBase.transform.SetParent(environment.transform);
            towerBase.transform.position = TowerCenter;
            towerBase.transform.localScale = new Vector3(2.0f, 2.5f, 2.0f); // wider so player has room
            Tint(towerBase, new Color(0.42f, 0.42f, 0.42f));

            var towerCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            towerCap.name = "TowerCap";
            towerCap.transform.SetParent(towerBase.transform);
            towerCap.transform.localPosition = new Vector3(0f, 1.02f, 0f);
            towerCap.transform.localScale = new Vector3(1.15f, 0.04f, 1.15f);
            Tint(towerCap, new Color(0.55f, 0.2f, 0.2f));

            var towerHealth = towerBase.AddComponent<Health>();
            towerHealth.SetMaxHealth(100f);

            // ---- Spawn points (drones come from ALL sides) ----------
            // Distributed around the tower so the player has to keep
            // turning their head to scan threats — left, right, front,
            // and from above. Heights are set so drones approach at or
            // slightly above the player's eye level on top of the tower.
            var spawnRoot = new GameObject("DroneSpawnPoints");
            spawnRoot.transform.SetParent(root.transform);
            // Front (+Z)
            var sp1 = MakeSpawn(spawnRoot.transform, "Spawn_FrontL",  new Vector3(-7f,  7f,  14f));
            var sp2 = MakeSpawn(spawnRoot.transform, "Spawn_FrontC",  new Vector3( 0f,  8f,  16f));
            var sp3 = MakeSpawn(spawnRoot.transform, "Spawn_FrontR",  new Vector3( 7f,  7f,  14f));
            // High front
            var sp4 = MakeSpawn(spawnRoot.transform, "Spawn_HighL",   new Vector3(-4f, 11f,  18f));
            var sp5 = MakeSpawn(spawnRoot.transform, "Spawn_HighR",   new Vector3( 4f, 11f,  18f));
            // Sides — drones strafe in from left and right of the tower
            var sp6 = MakeSpawn(spawnRoot.transform, "Spawn_SideL",   new Vector3(-15f, 7f,  -3f));
            var sp7 = MakeSpawn(spawnRoot.transform, "Spawn_SideR",   new Vector3( 15f, 7f,  -3f));
            var sp8 = MakeSpawn(spawnRoot.transform, "Spawn_SideHighL", new Vector3(-12f, 10f, 4f));
            var sp9 = MakeSpawn(spawnRoot.transform, "Spawn_SideHighR", new Vector3( 12f, 10f, 4f));

            // ---- Prefabs --------------------------------------------
            GameObject explosionPrefab = BuildOrUpdateExplosionPrefab();
            GameObject enemyProjectilePrefab = BuildOrUpdateEnemyProjectilePrefab();
            Drone dronePrefab = BuildOrUpdateDronePrefab(explosionPrefab, enemyProjectilePrefab);
            Projectile arrowPrefab = BuildOrUpdateArrowPrefab();
            GameObject crossbowPrefab = BuildOrUpdateCrossbowPrefab(arrowPrefab);
            GameObject swordPrefab = BuildOrUpdateSwordPrefab();

            // ---- ScoreManager ---------------------------------------
            var scoreGo = new GameObject("ScoreManager");
            scoreGo.transform.SetParent(root.transform);
            scoreGo.AddComponent<ScoreManager>();

            // ---- GameManager ----------------------------------------
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(root.transform);
            var gm = gmGo.AddComponent<GameManager>();
            SerializedObject so = new SerializedObject(gm);
            so.FindProperty("towerHealth").objectReferenceValue = towerHealth;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- Player rig FIRST so drones know where the player is ----
            Transform playerRigT = BuildPlayerRig(root.transform, crossbowPrefab, swordPrefab);
            Debug.Log("[MVP] Player rig built on top of tower. Quest: head + hands tracked, right trigger fires. Editor: mouse-look + LMB/Space.");

            // ---- WaveSpawner ----------------------------------------
            // target = PlayerRig (not the camera). The rig is anchored on
            // top of the tower and does NOT move when the player turns
            // their head — drones therefore can't be 'shaken off' by
            // tilting the headset. targetHealth = tower so impact damage
            // still flows to the thing being defended.
            var spawnerGo = new GameObject("WaveSpawner");
            spawnerGo.transform.SetParent(root.transform);
            var spawner = spawnerGo.AddComponent<WaveSpawner>();
            SerializedObject sso = new SerializedObject(spawner);
            sso.FindProperty("gameManager").objectReferenceValue = gm;
            sso.FindProperty("dronePrefab").objectReferenceValue = dronePrefab;
            var spArr = sso.FindProperty("spawnPoints");
            spArr.arraySize = 9;
            spArr.GetArrayElementAtIndex(0).objectReferenceValue = sp1.transform;
            spArr.GetArrayElementAtIndex(1).objectReferenceValue = sp2.transform;
            spArr.GetArrayElementAtIndex(2).objectReferenceValue = sp3.transform;
            spArr.GetArrayElementAtIndex(3).objectReferenceValue = sp4.transform;
            spArr.GetArrayElementAtIndex(4).objectReferenceValue = sp5.transform;
            spArr.GetArrayElementAtIndex(5).objectReferenceValue = sp6.transform;
            spArr.GetArrayElementAtIndex(6).objectReferenceValue = sp7.transform;
            spArr.GetArrayElementAtIndex(7).objectReferenceValue = sp8.transform;
            spArr.GetArrayElementAtIndex(8).objectReferenceValue = sp9.transform;
            sso.FindProperty("target").objectReferenceValue = playerRigT != null ? (Object)playerRigT : towerBase.transform;
            sso.FindProperty("targetHealth").objectReferenceValue = towerHealth;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // ---- World-space dashboard ------------------------------
            BuildDashboard(root.transform, towerHealth, spawner);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            EditorUtility.DisplayDialog("MVP scene built",
                "Built environment, drones, weapons, score, VFX, and dashboard under 'MVP_Generated'.\n\n" +
                "Press Play. With no XR Origin in the scene, you control a mouse-look FPS on top of the tower:\n" +
                "  • Left mouse / Space: fire crossbow\n" +
                "  • Mouse: aim\n" +
                "  • Escape: release cursor\n\n" +
                "With an XR Origin present, weapons are auto-attached under the controllers (verify in Hierarchy).",
                "OK");
        }

        // -------------------------------------------------------------
        // Sub-builders
        // -------------------------------------------------------------

        // -------------------------------------------------------------
        // Builds a programmatic XR rig: Camera + Left/Right hand
        // controllers, all driven by InputSystem TrackedPoseDriver. The
        // crossbow parents to the right hand so it tracks with the
        // controller. Hand visuals are simple skin-coloured cubes.
        //
        // Editor without active XR: TrackedPoseDriver's actions return
        // zero, so each child stays at its default local offset.
        // SimpleFPSController gives mouse-look + WASD; XRControllerWeaponInput
        // falls back to keyboard Space / mouse LMB.
        //
        // On Quest 3: HMD pose drives camera, controller poses drive
        // hand transforms, controller trigger fires the crossbow.
        private static Transform BuildPlayerRig(Transform parent, GameObject crossbowPrefab, GameObject swordPrefab)
        {
            // Disable any pre-existing scene "Main Camera" so we don't get
            // duplicate AudioListeners or competing cameras.
            var existingMain = GameObject.Find("Main Camera");
            if (existingMain != null) existingMain.SetActive(false);

            var rig = new GameObject("PlayerRig");
            rig.transform.SetParent(parent);
            rig.transform.position = new Vector3(TowerCenter.x, TowerCenter.y + TowerHeight * 0.5f, TowerCenter.z);
            rig.transform.rotation = Quaternion.identity;

            // CameraOffset is a tracking-origin anchor. With Floor tracking
            // mode the HMD pose ALREADY includes the user's standing height
            // (~1.6 m), so we MUST keep camOffset at the rig position
            // (localPos = 0). Adding another 1.6 m here would stack on top
            // of the headset height and put the player two metres above the
            // tower (the bug user reported as 'som moc vysoko, mám byť na
            // veži'). Editor mouse-look fallback gets eye height applied
            // separately by SimpleFPSController.
            var camOffset = new GameObject("CameraOffset");
            camOffset.transform.SetParent(rig.transform, worldPositionStays: false);
            camOffset.transform.localPosition = Vector3.zero;

            // ----- Camera (head) -----
            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(camOffset.transform, worldPositionStays: false);
            // Editor-only fallback eye height. On Quest the TrackedPoseDriver
            // overwrites this every frame with the real HMD pose (which is
            // floor-relative and already includes standing height).
            camGo.transform.localPosition = new Vector3(0f, PlayerEyeHeight, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            AddTrackedPoseDriver(camGo, "<XRHMD>/centerEyePosition", "<XRHMD>/centerEyeRotation");

            // ----- Hands (controllers) -----
            // Default offsets place RIGHT hand at the BACK (pistol grip,
            // closer to the player) and LEFT hand IN FRONT (forend, the
            // support hand that points the barrel toward the target).
            // The gun is then driven by TwoHandedGunGrip along the line
            // FROM right hand TO left hand → barrel points where the
            // left hand is steering it.
            //
            // On Quest the TrackedPoseDriver overrides these positions
            // with the real controller poses each frame.
            var rightHand = BuildHand(camOffset.transform, "RightHand",
                new Vector3( 0.18f, -0.22f, 0.18f),  // back: closer to chest
                "<XRController>{RightHand}/devicePosition",
                "<XRController>{RightHand}/deviceRotation");

            var leftHand  = BuildHand(camOffset.transform, "LeftHand",
                new Vector3( 0.05f, -0.18f, 0.65f),  // front: extended forward
                "<XRController>{LeftHand}/devicePosition",
                "<XRController>{LeftHand}/deviceRotation");

            // Round shield strapped to the left hand. Drone projectiles
            // that hit it are destroyed — protects the player.
            BuildShieldOn(leftHand.transform);

            // ----- Gun: held with TWO HANDS (right hand on grip, left
            // hand on forend). The gun lives as a sibling of the hands
            // under CameraOffset, NOT a child of the right hand — that
            // way TwoHandedGunGrip can drive its world rotation along
            // the line from right-hand → left-hand each frame.
            var crossbow = (GameObject)PrefabUtility.InstantiatePrefab(crossbowPrefab);
            crossbow.transform.SetParent(camOffset.transform, worldPositionStays: false);
            crossbow.transform.localPosition = new Vector3(0.22f, -0.30f, 0.30f);
            crossbow.transform.localRotation = Quaternion.identity;

            var rangedWeapon = crossbow.GetComponent<RangedWeapon>();

            // (HitscanWeapon was wired here for an experimental
            // raycast-from-camera mode. Removed because the user prefers
            // the original ballistic Arrow behaviour.)
            HitscanWeapon hitscan = null;

            // SINGLE-HAND grip: the crossbow follows the right controller's
            // position AND rotation. Aim is wherever the right controller
            // is pointing. Left hand is purely visual support — no aim
            // influence. Simple, predictable, hard to break.
            var grip = crossbow.AddComponent<TwoHandedGunGrip>();
            SerializedObject gso = new SerializedObject(grip);
            gso.FindProperty("rightHand").objectReferenceValue = rightHand.transform;
            gso.FindProperty("leftHand").objectReferenceValue  = leftHand.transform;
            gso.FindProperty("useSupportHandToAim").boolValue = false;
            gso.ApplyModifiedPropertiesWithoutUndo();

            // Reload by pulling the charging handle with the left hand.
            var reload = crossbow.AddComponent<ChargingHandleReload>();
            SerializedObject rso = new SerializedObject(reload);
            rso.FindProperty("leftHand").objectReferenceValue = leftHand.transform;
            rso.FindProperty("weapon").objectReferenceValue = rangedWeapon;
            rso.ApplyModifiedPropertiesWithoutUndo();

            // Reload visual feedback — kicks the gun + slides charging handle.
            var reloadAnim = crossbow.AddComponent<ReloadAnimation>();
            SerializedObject rao = new SerializedObject(reloadAnim);
            rao.FindProperty("reloadSource").objectReferenceValue = reload;
            rao.ApplyModifiedPropertiesWithoutUndo();

            // Quest right-trigger → Fire(); editor: Space / LMB.
            // Wires both the ballistic crossbow AND the hitscan; the
            // input prefers hitscan when both are set.
            var triggerInput = crossbow.AddComponent<XRControllerWeaponInput>();
            SerializedObject tio = new SerializedObject(triggerInput);
            tio.FindProperty("weapon").objectReferenceValue = rangedWeapon;
            tio.FindProperty("hitscan").objectReferenceValue = hitscan;
            tio.FindProperty("hand").enumValueIndex = (int)XRControllerWeaponInput.Hand.Right;
            tio.ApplyModifiedPropertiesWithoutUndo();

            // Left hand: empty. User asked for "iba zbran" (just the gun).
            // The melee sword remains in Assets/_Project/Prefabs/Weapons/
            // for later use; we just don't attach it here.
            _ = swordPrefab; // suppress unused-arg warning

            // ----- Editor mouse-look fallback (no-op when XR is running) -----
            var ctrl = rig.AddComponent<SimpleFPSController>();
            SerializedObject sob = new SerializedObject(ctrl);
            // Pitch the CAMERA only, so hand offsets stay parallel to ground
            // (matches how real VR works: head pitches, hands don't follow).
            sob.FindProperty("pitchPivot").objectReferenceValue = camGo.transform;
            sob.FindProperty("rangedWeapon").objectReferenceValue = rangedWeapon;
            sob.ApplyModifiedPropertiesWithoutUndo();

            // Create a stable PLAYER TARGET anchored at eye level under
            // the rig. WaveSpawner aims drones at this transform — they
            // converge on the player's chest/head altitude (y ≈ 6.6) on
            // top of the tower instead of diving for the tower base.
            // Crucially, this is NOT a child of the camera, so it doesn't
            // move when the player turns their head.
            var playerTarget = new GameObject("PlayerTarget");
            playerTarget.transform.SetParent(rig.transform, worldPositionStays: false);
            playerTarget.transform.localPosition = new Vector3(0f, PlayerEyeHeight, 0f);

            return playerTarget.transform;
        }

        // Round shield: a flat cylinder parented to the left hand, with a
        // grip handle on the back. Has a non-trigger collider so drone
        // projectiles destroy themselves on contact (see EnemyProjectile).
        private static void BuildShieldOn(Transform leftHandTransform)
        {
            var shieldRoot = new GameObject("Shield");
            shieldRoot.transform.SetParent(leftHandTransform, worldPositionStays: false);
            // Slightly forward of the palm so it intercepts incoming fire.
            shieldRoot.transform.localPosition = new Vector3(0f, 0f, 0.10f);
            shieldRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // face the player's view direction

            // Disc body (cylinder squashed flat).
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(shieldRoot.transform, worldPositionStays: false);
            disc.transform.localPosition = Vector3.zero;
            disc.transform.localScale = new Vector3(0.45f, 0.025f, 0.45f);
            Tint(disc, new Color(0.55f, 0.4f, 0.18f));   // brushed-bronze tone

            // Rim (slightly larger flat ring) — visual only.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.SetParent(shieldRoot.transform, worldPositionStays: false);
            rim.transform.localPosition = Vector3.zero;
            rim.transform.localScale = new Vector3(0.50f, 0.018f, 0.50f);
            Tint(rim, new Color(0.30f, 0.22f, 0.10f));
            // Strip the auto collider from the rim — only the disc collides.
            Object.DestroyImmediate(rim.GetComponent<CapsuleCollider>());

            // Boss / centre stud — pure decoration.
            var boss = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boss.name = "Boss";
            boss.transform.SetParent(shieldRoot.transform, worldPositionStays: false);
            boss.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            boss.transform.localScale = Vector3.one * 0.12f;
            Tint(boss, new Color(0.85f, 0.7f, 0.25f));
            Object.DestroyImmediate(boss.GetComponent<SphereCollider>());

            // Make sure the disc collider is a non-trigger so projectile
            // OnCollisionEnter fires.
            var col = disc.GetComponent<CapsuleCollider>();
            if (col != null) col.isTrigger = false;

            shieldRoot.AddComponent<DroneDefense.Enemies.Shield>();
        }

        private static GameObject BuildHand(Transform parent, string name, Vector3 fallbackOffset,
                                            string posBinding, string rotBinding)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = fallbackOffset;
            go.transform.localRotation = Quaternion.identity;

            AddTrackedPoseDriver(go, posBinding, rotBinding);

            // Visual: a single semi-transparent sphere. The player just
            // needs a hint of where their controller is — this stays out
            // of the way of the gun visual instead of clipping through it.
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "HandHint";
            sphere.transform.SetParent(go.transform, worldPositionStays: false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * 0.06f;
            Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());

            var renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = GetOrCreateTransparentMat(new Color(0.95f, 0.78f, 0.62f, 0.45f));

            return go;
        }

        // Cached transparent URP/Lit material per RGBA. URP/Lit's transparent
        // mode requires _Surface=1 (Transparent), _Blend=0 (Alpha), and the
        // queue index moved into the Transparent range (3000+).
        private static Material GetOrCreateTransparentMat(Color c)
        {
            EnsureFolder("Assets/_Project/Materials");
            EnsureFolder("Assets/_Project/Materials/Generated");
            int rk = Mathf.RoundToInt(c.r * 255f);
            int gk = Mathf.RoundToInt(c.g * 255f);
            int bk = Mathf.RoundToInt(c.b * 255f);
            int ak = Mathf.RoundToInt(c.a * 255f);
            string path = $"Assets/_Project/Materials/Generated/Trans_{rk:000}_{gk:000}_{bk:000}_{ak:000}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing.shader != null) return existing;

            var urp = UrpLit;
            if (urp == null) return null;

            var mat = new Material(urp);
            mat.SetFloat("_Surface", 1f);          // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 0f);             // 0=Alpha
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else mat.color = c;

            if (existing != null) { AssetDatabase.DeleteAsset(path); }
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void AddTrackedPoseDriver(GameObject go, string posBinding, string rotBinding)
        {
            var driver = go.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            driver.trackingType = UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType   = UnityEngine.InputSystem.XR.TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var posAction = new UnityEngine.InputSystem.InputAction(
                name: $"{go.name}_Pos",
                type: UnityEngine.InputSystem.InputActionType.Value,
                binding: posBinding);
            var rotAction = new UnityEngine.InputSystem.InputAction(
                name: $"{go.name}_Rot",
                type: UnityEngine.InputSystem.InputActionType.Value,
                binding: rotBinding);

            driver.positionInput = new UnityEngine.InputSystem.InputActionProperty(posAction);
            driver.rotationInput = new UnityEngine.InputSystem.InputActionProperty(rotAction);
        }

        // -------------------------------------------------------------
        private static void BuildDashboard(Transform parent, Health towerHealth, WaveSpawner spawner)
        {
            var dash = new GameObject("Dashboard");
            dash.transform.SetParent(parent);
            // Floats just above and in front of the tower so the player on
            // top can still see it in their peripheral vision.
            dash.transform.position = new Vector3(0f, TowerCenter.y + TowerHeight * 0.5f + 2.0f, TowerCenter.z + 1.5f);

            // ----- HP bar -----
            var hpBarRoot = new GameObject("TowerHpBar");
            hpBarRoot.transform.SetParent(dash.transform, worldPositionStays: false);
            hpBarRoot.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            var hpFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpFrame.name = "Frame";
            hpFrame.transform.SetParent(hpBarRoot.transform, worldPositionStays: false);
            hpFrame.transform.localScale = new Vector3(2.55f, 0.32f, 0.05f);
            Tint(hpFrame, new Color(0.05f, 0.05f, 0.05f));
            Object.DestroyImmediate(hpFrame.GetComponent<BoxCollider>());

            var hpAnchor = new GameObject("FillAnchor");
            hpAnchor.transform.SetParent(hpBarRoot.transform, worldPositionStays: false);
            hpAnchor.transform.localPosition = new Vector3(-1.25f, 0f, -0.03f);

            var hpFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpFill.name = "Fill";
            hpFill.transform.SetParent(hpAnchor.transform, worldPositionStays: false);
            hpFill.transform.localPosition = new Vector3(1.25f, 0f, 0f);
            hpFill.transform.localScale = new Vector3(2.5f, 0.26f, 0.04f);
            Tint(hpFill, new Color(0.85f, 0.2f, 0.2f));
            Object.DestroyImmediate(hpFill.GetComponent<BoxCollider>());

            var hpBar = hpBarRoot.AddComponent<TowerHpBar>();
            SerializedObject hso = new SerializedObject(hpBar);
            hso.FindProperty("source").objectReferenceValue = towerHealth;
            hso.FindProperty("fill").objectReferenceValue = hpAnchor.transform;
            hso.ApplyModifiedPropertiesWithoutUndo();

            // ----- HP label -----
            MakeLabel(hpBarRoot.transform, "HpLabel",
                new Vector3(-1.6f, 0f, -0.1f),
                "HP", 0.04f, new Color(0.95f, 0.95f, 0.95f));

            // ----- Wave counter -----
            var waveLabel = MakeLabel(dash.transform, "WaveCounter",
                new Vector3(0f, 1.0f, 0f),
                "WAVE 0 / 5", 0.06f, Color.white);
            var wc = waveLabel.AddComponent<WaveCounter>();
            SerializedObject wso = new SerializedObject(wc);
            wso.FindProperty("spawner").objectReferenceValue = spawner;
            wso.ApplyModifiedPropertiesWithoutUndo();

            // ----- Score panel (left) + Combo (right) -----
            var scoreLabel = MakeLabel(dash.transform, "ScoreLabel",
                new Vector3(-1.6f, 1.6f, 0f),
                "0", 0.05f, new Color(0.4f, 0.95f, 0.4f));
            scoreLabel.GetComponent<TextMesh>().anchor = TextAnchor.MiddleLeft;
            var sd = scoreLabel.AddComponent<ScoreDisplay>();
            SerializedObject sdSo = new SerializedObject(sd);
            sdSo.FindProperty("mode").enumValueIndex = (int)ScoreDisplay.Mode.Score;
            sdSo.FindProperty("prefix").stringValue = "SCORE  ";
            sdSo.ApplyModifiedPropertiesWithoutUndo();

            var comboLabel = MakeLabel(dash.transform, "ComboLabel",
                new Vector3(1.6f, 1.6f, 0f),
                "—", 0.05f, new Color(1f, 0.85f, 0.2f));
            comboLabel.GetComponent<TextMesh>().anchor = TextAnchor.MiddleRight;
            var cd = comboLabel.AddComponent<ScoreDisplay>();
            SerializedObject cdSo = new SerializedObject(cd);
            cdSo.FindProperty("mode").enumValueIndex = (int)ScoreDisplay.Mode.Combo;
            cdSo.FindProperty("prefix").stringValue = "COMBO  ";
            cdSo.ApplyModifiedPropertiesWithoutUndo();

            // ----- Game-over banner (centered, above wave counter) -----
            var gameOverLabel = MakeLabel(dash.transform, "GameOverBanner",
                new Vector3(0f, 2.3f, 0f),
                "", 0.10f, Color.white);
            gameOverLabel.AddComponent<GameOverDisplay>();

            // Billboard whole dashboard so it always faces the player.
            dash.AddComponent<BillboardToPlayer>();
        }

        private static GameObject MakeLabel(Transform parent, string name, Vector3 localPos, string text, float charSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 80;
            tm.characterSize = charSize;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = color;
            // Smooth rendering — the default texture font looks chunky at 80pt
            // but characterSize scales it down.
            return go;
        }

        // -------------------------------------------------------------
        [MenuItem("DroneDefense/MVP/Build Drone Prefab Only", priority = 21)]
        public static void BuildDronePrefabOnlyMenu()
        {
            EnsureFolder("Assets/_Project/Prefabs/Drones");
            EnsureFolder("Assets/_Project/Prefabs/VFX");
            var explosion = BuildOrUpdateExplosionPrefab();
            BuildOrUpdateDronePrefab(explosion);
            EditorUtility.DisplayDialog("Drone prefab",
                $"Saved at {DronePrefabPath}.", "OK");
        }

        // -------------------------------------------------------------
        private static Drone BuildOrUpdateDronePrefab(GameObject explosionPrefab, GameObject enemyProjectilePrefab = null)
        {
            var temp = new GameObject("KamikazeDrone");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(temp.transform);
            body.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
            Tint(body, new Color(0.2f, 0.2f, 0.25f));

            for (int i = 0; i < 4; i++)
            {
                var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prop.name = $"Prop_{i}";
                prop.transform.SetParent(temp.transform);
                float ax = ((i % 2) == 0 ? 1f : -1f) * 0.3f;
                float az = (i < 2 ? 1f : -1f) * 0.3f;
                prop.transform.localPosition = new Vector3(ax, 0.15f, az);
                prop.transform.localScale = new Vector3(0.25f, 0.04f, 0.06f);
                Tint(prop, new Color(0.85f, 0.85f, 0.2f));
            }

            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(temp.transform);
            eye.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            eye.transform.localScale = Vector3.one * 0.12f;
            Tint(eye, new Color(1f, 0.2f, 0.1f));

            var rb = temp.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 1f;

            var col = temp.AddComponent<SphereCollider>();
            col.radius = 0.35f;
            col.isTrigger = false;

            var hp = temp.AddComponent<Health>();
            hp.SetMaxHealth(40f);

            var drone = temp.AddComponent<Drone>();
            // Wire the explosion prefab.
            SerializedObject dso = new SerializedObject(drone);
            dso.FindProperty("explosionPrefab").objectReferenceValue = explosionPrefab;
            dso.ApplyModifiedPropertiesWithoutUndo();

            // Optional ranged-attack module — drone will periodically
            // spawn EnemyProjectiles aimed at the player. Player blocks
            // them with the left-hand Shield.
            if (enemyProjectilePrefab != null)
            {
                var shooter = temp.AddComponent<DroneShooter>();
                SerializedObject ssoShooter = new SerializedObject(shooter);
                ssoShooter.FindProperty("projectilePrefab").objectReferenceValue = enemyProjectilePrefab;
                ssoShooter.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var c in temp.GetComponentsInChildren<BoxCollider>())
                Object.DestroyImmediate(c);
            foreach (var c in temp.GetComponentsInChildren<SphereCollider>())
                if (c.gameObject != temp) Object.DestroyImmediate(c);

            SaveTempMaterialsAsAssets(temp);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(temp, DronePrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(temp);
            return prefab.GetComponent<Drone>();
        }

        // -------------------------------------------------------------
        private static GameObject BuildOrUpdateExplosionPrefab()
        {
            var temp = new GameObject("DroneExplosion");
            var ps = temp.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 1.0f;
            main.startSpeed = 9f;
            main.startSize = 0.32f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.7f, 0.15f), new Color(1f, 0.3f, 0.05f));
            main.gravityModifier = 0.6f;
            main.maxParticles = 200;

            var emission = ps.emission;
            // Bigger primary burst + a secondary delayed burst so the cloud
            // looks like a real two-stage detonation, not a single puff.
            emission.SetBursts(new[] {
                new ParticleSystem.Burst(0f,    100),
                new ParticleSystem.Burst(0.08f, 50),
            });
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.10f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.05f), 0.4f),
                    new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f),
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sCurve = new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(0.3f, 1.4f),  // expand outward
                new Keyframe(1f, 0.1f));   // dissipate
            size.size = new ParticleSystem.MinMaxCurve(1f, sCurve);

            var renderer = temp.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Particle material — saved as an asset so it survives prefab
            // serialization. Reuses the existing asset on rerun.
            EnsureFolder("Assets/_Project/Materials/Generated");
            const string ParticleMatPath = "Assets/_Project/Materials/Generated/Particle_Explosion.mat";
            Material pmat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMatPath);
            if (pmat == null || pmat.shader == null)
            {
                var pshader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Sprites/Default");
                if (pshader != null)
                {
                    var newMat = new Material(pshader);
                    if (newMat.HasProperty("_BaseColor"))
                        newMat.SetColor("_BaseColor", new Color(1f, 0.7f, 0.25f));
                    else newMat.color = new Color(1f, 0.7f, 0.25f);
                    if (pmat == null) AssetDatabase.CreateAsset(newMat, ParticleMatPath);
                    else EditorUtility.CopySerialized(newMat, pmat);
                    AssetDatabase.SaveAssets();
                    pmat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMatPath);
                }
            }
            renderer.sharedMaterial = pmat;

            temp.AddComponent<DestructionEffect>();

            // Particle material is already an asset (saved above) — no need to call
            // SaveTempMaterialsAsAssets here, but it's idempotent if we did.
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(temp, ExplosionPrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        // -------------------------------------------------------------
        // Glowing-orange sphere fired BY drones at the player. Lacks
        // gravity, slow enough to be dodgeable, blocked by the Shield.
        private static GameObject BuildOrUpdateEnemyProjectilePrefab()
        {
            EnsureFolder("Assets/_Project/Prefabs/Drones");

            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.name = "EnemyProjectile";
            temp.transform.localScale = Vector3.one * 0.18f;
            Tint(temp, new Color(1f, 0.55f, 0.1f));

            var rb = temp.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Sphere primitive already has a SphereCollider — keep it as
            // a non-trigger collider so OnCollisionEnter fires.
            var col = temp.GetComponent<SphereCollider>();
            col.isTrigger = false;

            temp.AddComponent<EnemyProjectile>();

            SaveTempMaterialsAsAssets(temp);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(temp, EnemyProjectilePrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        // -------------------------------------------------------------
        private static Projectile BuildOrUpdateArrowPrefab()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = "Arrow";
            temp.transform.localScale = new Vector3(0.05f, 0.05f, 0.5f);
            Tint(temp, new Color(0.25f, 0.18f, 0.1f));

            // Tip
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = "Tip";
            tip.transform.SetParent(temp.transform);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            tip.transform.localScale = new Vector3(1.6f, 1.6f, 0.4f);
            Tint(tip, new Color(0.7f, 0.7f, 0.75f));
            var tipCol = tip.GetComponent<BoxCollider>();
            if (tipCol != null) Object.DestroyImmediate(tipCol);

            // Fletching
            var fletch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fletch.name = "Fletching";
            fletch.transform.SetParent(temp.transform);
            fletch.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            fletch.transform.localScale = new Vector3(2.4f, 0.4f, 0.5f);
            Tint(fletch, new Color(0.9f, 0.9f, 0.9f));
            var fc = fletch.GetComponent<BoxCollider>();
            if (fc != null) Object.DestroyImmediate(fc);

            var rb = temp.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = 0.05f;

            var proj = temp.AddComponent<Projectile>();

            SaveTempMaterialsAsAssets(temp);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(temp, ArrowPrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(temp);
            return prefab.GetComponent<Projectile>();
        }

        // -------------------------------------------------------------
        // Builds the held-weapon prefab. If the user's PolyOne gun pack is
        // available we use SM_Ak47 as the visual; otherwise fall back to a
        // procedural cube crossbow.
        private static GameObject BuildOrUpdateCrossbowPrefab(Projectile arrowPrefab)
        {
            GameObject root;
            Transform muzzleTransform;

            // User asked to drop the PolyOne AK47 wrap and go back to the
            // procedural cube crossbow that was reliably firing arrows.
            // Set USE_AK47 = true (and rebuild) to re-enable.
            const bool USE_AK47 = false;
            var gunModel = USE_AK47 ? AssetDatabase.LoadAssetAtPath<GameObject>(GunModelPrefabPath) : null;
            if (gunModel != null)
            {
                // Wrap an instance of the AK47 prefab in a "Crossbow" root so
                // RangedWeapon + Muzzle live alongside the model without
                // modifying the imported asset.
                //
                // Coordinate convention (GUN local space):
                //   origin (0,0,0)     = pistol grip, where the right palm sits
                //   +Z                 = barrel-forward (toward target)
                //   +X                 = right side of the gun
                //   +Y                 = up (top of receiver)
                //
                // We arrange the imported model so its grip lands at the root.
                root = new GameObject("Crossbow");

                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(gunModel);
                modelInstance.transform.SetParent(root.transform, worldPositionStays: false);
                // Shift the model BACKWARD along Z so the pistol grip sits at
                // root origin. The AK47 stock is ~0.20m behind the grip, the
                // barrel tip is ~0.55m forward of it. Scale 0.85 keeps the
                // gun close to real-life proportions in VR (real AK ~0.87m).
                modelInstance.transform.localPosition = new Vector3(0f, -0.04f, -0.20f);
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one * 0.85f;

                // Muzzle = barrel tip. The AK barrel sits slightly above the
                // grip line; +0.55m forward, +0.05m up matches the visible
                // muzzle on the AK47 model after our transform.
                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(root.transform, worldPositionStays: false);
                muzzle.transform.localPosition = new Vector3(0f, 0.05f, 0.55f);
                muzzle.transform.localRotation = Quaternion.identity;
                muzzleTransform = muzzle.transform;
            }
            else
            {
                // Fallback procedural cube crossbow (used if the PolyOne pack
                // isn't installed). Same shape as before.
                root = new GameObject("Crossbow");
                var stock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stock.name = "Stock";
                stock.transform.SetParent(root.transform);
                stock.transform.localPosition = Vector3.zero;
                stock.transform.localScale = new Vector3(0.06f, 0.08f, 0.4f);
                Tint(stock, new Color(0.3f, 0.18f, 0.08f));
                Object.DestroyImmediate(stock.GetComponent<BoxCollider>());

                var limbs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                limbs.name = "Limbs";
                limbs.transform.SetParent(root.transform);
                limbs.transform.localPosition = new Vector3(0f, 0.04f, 0.2f);
                limbs.transform.localScale = new Vector3(0.4f, 0.04f, 0.04f);
                Tint(limbs, new Color(0.18f, 0.13f, 0.08f));
                Object.DestroyImmediate(limbs.GetComponent<BoxCollider>());

                var str = GameObject.CreatePrimitive(PrimitiveType.Cube);
                str.name = "String";
                str.transform.SetParent(root.transform);
                str.transform.localPosition = new Vector3(0f, 0.04f, 0.18f);
                str.transform.localScale = new Vector3(0.36f, 0.005f, 0.005f);
                Tint(str, new Color(0.95f, 0.95f, 0.95f));
                Object.DestroyImmediate(str.GetComponent<BoxCollider>());

                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(root.transform);
                muzzle.transform.localPosition = new Vector3(0f, 0.04f, 0.25f);
                muzzle.transform.localRotation = Quaternion.identity;
                muzzleTransform = muzzle.transform;
            }

            var weapon = root.AddComponent<RangedWeapon>();
            SerializedObject ro = new SerializedObject(weapon);
            ro.FindProperty("damage").floatValue = 50f;
            ro.FindProperty("projectilePrefab").objectReferenceValue = arrowPrefab;
            ro.FindProperty("muzzle").objectReferenceValue = muzzleTransform;
            ro.FindProperty("muzzleVelocity").floatValue = 60f;
            ro.FindProperty("fireCooldown").floatValue = 0.25f;
            ro.FindProperty("useGravity").boolValue = false;
            ro.ApplyModifiedPropertiesWithoutUndo();

            SaveTempMaterialsAsAssets(root);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, CrossbowPrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // -------------------------------------------------------------
        private static GameObject BuildOrUpdateSwordPrefab()
        {
            var root = new GameObject("Sword");

            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(root.transform);
            blade.transform.localPosition = new Vector3(0f, 0f, 0.35f);
            blade.transform.localScale = new Vector3(0.05f, 0.05f, 0.7f);
            Tint(blade, new Color(0.9f, 0.9f, 0.95f));
            var bladeCol = blade.GetComponent<BoxCollider>();
            bladeCol.isTrigger = true;

            var hilt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hilt.name = "Hilt";
            hilt.transform.SetParent(root.transform);
            hilt.transform.localPosition = Vector3.zero;
            hilt.transform.localScale = new Vector3(0.16f, 0.05f, 0.06f);
            Tint(hilt, new Color(0.3f, 0.18f, 0.08f));
            Object.DestroyImmediate(hilt.GetComponent<BoxCollider>());

            var tip = new GameObject("TipPoint");
            tip.transform.SetParent(root.transform);
            tip.transform.localPosition = new Vector3(0f, 0f, 0.7f);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var melee = root.AddComponent<MeleeWeapon>();
            SerializedObject mo = new SerializedObject(melee);
            mo.FindProperty("damage").floatValue = 60f;
            mo.FindProperty("tipPoint").objectReferenceValue = tip.transform;
            mo.FindProperty("bladeCollider").objectReferenceValue = bladeCol;
            mo.ApplyModifiedPropertiesWithoutUndo();

            SaveTempMaterialsAsAssets(root);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, SwordPrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // -------------------------------------------------------------
        private static GameObject MakeSpawn(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.LookAt(TowerCenter);
            return go;
        }

        // Tint: clones the renderer's primitive default material and applies
        // the requested colour. Forces the shader to URP/Lit if it isn't
        // already (Unity's primitive default sometimes resolves to the
        // built-in Standard shader, which renders magenta under URP). The
        // clone preserves URP/Lit's correct default keywords + property
        // values, which `new Material(shader)` does NOT.
        private static Shader _cachedUrpLit;
        private static Shader UrpLit
        {
            get
            {
                if (_cachedUrpLit == null) _cachedUrpLit = Shader.Find("Universal Render Pipeline/Lit");
                return _cachedUrpLit;
            }
        }

        private static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;

            var mat = new Material(r.sharedMaterial);
            // Force URP/Lit if Unity gave us the built-in Standard shader.
            var urp = UrpLit;
            if (urp != null && mat.shader != urp)
            {
                mat.shader = urp;
            }
            mat.name = $"Color_{Mathf.RoundToInt(c.r*255):000}_{Mathf.RoundToInt(c.g*255):000}_{Mathf.RoundToInt(c.b*255):000}";
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else mat.color = c;
            r.sharedMaterial = mat;
        }

        // Walk the temp prefab object's renderers and persist each in-memory
        // material as a fresh asset file under Assets/_Project/Materials/Generated/.
        // Always recreates the asset (deletes any prior one) so we never
        // pick up a stale broken material from a failed earlier build.
        private static void SaveTempMaterialsAsAssets(GameObject temp)
        {
            EnsureFolder("Assets/_Project/Materials");
            EnsureFolder("Assets/_Project/Materials/Generated");

            var byKey = new System.Collections.Generic.Dictionary<string, Material>();

            foreach (var r in temp.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))) continue;

                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                    int rk = Mathf.RoundToInt(c.r * 255f);
                    int gk = Mathf.RoundToInt(c.g * 255f);
                    int bk = Mathf.RoundToInt(c.b * 255f);
                    string key = $"{rk}_{gk}_{bk}";

                    if (byKey.TryGetValue(key, out var cached))
                    {
                        mats[i] = cached;
                        continue;
                    }

                    string path = $"Assets/_Project/Materials/Generated/Color_{rk:000}_{gk:000}_{bk:000}.mat";

                    // Force-delete any prior asset at this path so we always
                    // start from a freshly-cloned URP/Lit material with valid
                    // shader + keywords. If the prior file's shader compile
                    // got cached as broken, deleting + recreating clears it.
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                    {
                        AssetDatabase.DeleteAsset(path);
                    }

                    AssetDatabase.CreateAsset(m, path);
                    byKey[key] = m;
                }
                r.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Kept for compatibility — replaced by SaveTempMaterialsAsAssets.
        private static void EmbedRendererMaterials(GameObject prefabAsset) { }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
