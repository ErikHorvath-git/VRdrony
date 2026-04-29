// MVPBootstrap — builds the entire Phase 2-7 MVP scene content
// programmatically. Run AFTER the user has finished the Phase 1 wizard
// steps (URP assigned, Oculus XR plug-in enabled, XR Origin in Main.unity).
//
// Idempotent for the most part — re-running deletes prior MVP roots
// (group "MVP_Generated") and rebuilds them. The XR Origin and any user
// setup are left alone.
//
// What this builds:
//   - Ground plane, sky-blue ambient
//   - Castle wall (cube), tower (cylinder + cube top)
//   - 3 spawn points behind the player
//   - A drone prefab (cube + small lights), saved to Assets/_Project/Prefabs/Drones/
//   - GameManager + WaveSpawner GameObjects, wired together
//   - World-space tower HP bar above the tower (3D primitives)
//   - World-space wave counter (3D TextMesh)
//   - WallBoundLocomotion on the XR Origin (if found)

using System.IO;
using DroneDefense.Combat;
using DroneDefense.Core;
using DroneDefense.Enemies;
using DroneDefense.Player;
using DroneDefense.UI;
using DroneDefense.Waves;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneDefense.Editor
{
    public static class MVPBootstrap
    {
        private const string GeneratedRootName = "MVP_Generated";
        private const string DronePrefabPath = "Assets/_Project/Prefabs/Drones/KamikazeDrone.prefab";

        // -------------------------------------------------------------
        [MenuItem("DroneDefense/MVP/Build Scene", priority = 20)]
        public static void BuildScene()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Drones");

            // Wipe any prior generated root so we don't accumulate dupes.
            var existing = GameObject.Find(GeneratedRootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Build MVP");

            // Environment: ground, wall, tower
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
            wall.transform.position = new Vector3(0f, 1f, -2f);
            wall.transform.localScale = new Vector3(10f, 2f, 0.5f);
            Tint(wall, new Color(0.55f, 0.5f, 0.45f));

            var towerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            towerBase.name = "Tower";
            towerBase.transform.SetParent(environment.transform);
            towerBase.transform.position = new Vector3(0f, 2.5f, -3.5f);
            towerBase.transform.localScale = new Vector3(1.5f, 2.5f, 1.5f);
            Tint(towerBase, new Color(0.42f, 0.42f, 0.42f));

            var towerCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            towerCap.name = "TowerCap";
            towerCap.transform.SetParent(towerBase.transform);
            towerCap.transform.localPosition = new Vector3(0f, 1f, 0f);
            towerCap.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);
            Tint(towerCap, new Color(0.55f, 0.2f, 0.2f));

            // Tower health
            var towerHealth = towerBase.AddComponent<Health>();
            towerHealth.SetMaxHealth(100f);

            // Spawn points (behind player, +Z direction, varied X/Y)
            var spawnRoot = new GameObject("DroneSpawnPoints");
            spawnRoot.transform.SetParent(root.transform);
            var sp1 = MakeSpawn(spawnRoot.transform, "Spawn_L", new Vector3(-6f, 4f, 12f));
            var sp2 = MakeSpawn(spawnRoot.transform, "Spawn_C", new Vector3( 0f, 5f, 14f));
            var sp3 = MakeSpawn(spawnRoot.transform, "Spawn_R", new Vector3( 6f, 4f, 12f));

            // Drone prefab
            Drone dronePrefab = BuildOrUpdateDronePrefab();

            // GameManager
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(root.transform);
            var gm = gmGo.AddComponent<GameManager>();
            SerializedObject so = new SerializedObject(gm);
            so.FindProperty("towerHealth").objectReferenceValue = towerHealth;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Wave spawner
            var spawnerGo = new GameObject("WaveSpawner");
            spawnerGo.transform.SetParent(root.transform);
            var spawner = spawnerGo.AddComponent<WaveSpawner>();
            SerializedObject sso = new SerializedObject(spawner);
            sso.FindProperty("gameManager").objectReferenceValue = gm;
            sso.FindProperty("dronePrefab").objectReferenceValue = dronePrefab;
            var spArr = sso.FindProperty("spawnPoints");
            spArr.arraySize = 3;
            spArr.GetArrayElementAtIndex(0).objectReferenceValue = sp1.transform;
            spArr.GetArrayElementAtIndex(1).objectReferenceValue = sp2.transform;
            spArr.GetArrayElementAtIndex(2).objectReferenceValue = sp3.transform;
            sso.FindProperty("target").objectReferenceValue = towerBase.transform;
            sso.FindProperty("targetHealth").objectReferenceValue = towerHealth;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // Tower HP bar — simple 3D bar above the tower.
            var hpBarRoot = new GameObject("TowerHpBar");
            hpBarRoot.transform.SetParent(root.transform);
            hpBarRoot.transform.position = new Vector3(0f, 5.5f, -3.5f);

            var hpFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpFrame.name = "Frame";
            hpFrame.transform.SetParent(hpBarRoot.transform, worldPositionStays: false);
            hpFrame.transform.localScale = new Vector3(2.05f, 0.22f, 0.05f);
            Tint(hpFrame, new Color(0.05f, 0.05f, 0.05f));
            Object.DestroyImmediate(hpFrame.GetComponent<BoxCollider>());

            var hpAnchor = new GameObject("FillAnchor");
            hpAnchor.transform.SetParent(hpBarRoot.transform, worldPositionStays: false);
            hpAnchor.transform.localPosition = new Vector3(-1f, 0f, -0.03f);

            var hpFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hpFill.name = "Fill";
            hpFill.transform.SetParent(hpAnchor.transform, worldPositionStays: false);
            hpFill.transform.localPosition = new Vector3(1f, 0f, 0f);  // pivot at left edge
            hpFill.transform.localScale = new Vector3(2f, 0.18f, 0.04f);
            Tint(hpFill, new Color(0.85f, 0.2f, 0.2f));
            Object.DestroyImmediate(hpFill.GetComponent<BoxCollider>());

            var hpBar = hpBarRoot.AddComponent<TowerHpBar>();
            SerializedObject hso = new SerializedObject(hpBar);
            hso.FindProperty("source").objectReferenceValue = towerHealth;
            hso.FindProperty("fill").objectReferenceValue = hpAnchor.transform;
            hso.ApplyModifiedPropertiesWithoutUndo();
            hpBarRoot.AddComponent<BillboardToPlayer>();

            // Wave counter — TextMesh in front of the tower
            var counterGo = new GameObject("WaveCounter");
            counterGo.transform.SetParent(root.transform);
            counterGo.transform.position = new Vector3(0f, 6.2f, -3.5f);
            var tm = counterGo.AddComponent<TextMesh>();
            tm.text = "Ready";
            tm.fontSize = 80;
            tm.characterSize = 0.05f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
            var wc = counterGo.AddComponent<WaveCounter>();
            SerializedObject wso = new SerializedObject(wc);
            wso.FindProperty("spawner").objectReferenceValue = spawner;
            wso.ApplyModifiedPropertiesWithoutUndo();
            counterGo.AddComponent<BillboardToPlayer>();

            // Try to attach WallBoundLocomotion to an existing XR Origin.
            var xrOrigin = GameObject.Find("XR Origin (XR Rig)");
            if (xrOrigin == null) xrOrigin = GameObject.Find("XR Origin");
            if (xrOrigin != null && xrOrigin.GetComponent<WallBoundLocomotion>() == null)
            {
                xrOrigin.AddComponent<WallBoundLocomotion>();
                Debug.Log("[MVP] Added WallBoundLocomotion to XR Origin.");
            }
            else if (xrOrigin == null)
            {
                Debug.LogWarning("[MVP] XR Origin not found in scene — locomotion not attached. Add the XR Origin (Phase 1 step) and re-run.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            EditorUtility.DisplayDialog("MVP scene built",
                "Built environment, drones, game logic, and UI under 'MVP_Generated'.\n\n" +
                "Next:\n" +
                "1. Press Play in editor (with XR Device Simulator, optional) to smoke-test.\n" +
                "2. File → Build Profiles → Android → Build And Run with Quest 3 attached.\n\n" +
                "Weapons (sword + crossbow) are NOT auto-attached to controllers — \n" +
                "wire them under XR Origin's hand controllers manually using the prefabs in Assets/_Project/Prefabs/Weapons/ (TODO: build via DroneDefense/MVP/Build Weapons).",
                "OK");
        }

        // -------------------------------------------------------------
        [MenuItem("DroneDefense/MVP/Build Drone Prefab Only", priority = 21)]
        public static void BuildDronePrefabOnlyMenu()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Drones");
            BuildOrUpdateDronePrefab();
            EditorUtility.DisplayDialog("Drone prefab",
                $"Saved at {DronePrefabPath}.", "OK");
        }

        private static Drone BuildOrUpdateDronePrefab()
        {
            // Build a transient scene object, write it as a prefab, then destroy.
            var temp = new GameObject("KamikazeDrone");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(temp.transform);
            body.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
            Tint(body, new Color(0.2f, 0.2f, 0.25f));

            // 4 propellers as small flat cubes
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

            // Glowing eye
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.transform.SetParent(temp.transform);
            eye.transform.localPosition = new Vector3(0f, 0f, 0.22f);
            eye.transform.localScale = Vector3.one * 0.12f;
            Tint(eye, new Color(1f, 0.2f, 0.1f));

            // Physics + gameplay
            var rb = temp.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 1f;

            var col = temp.AddComponent<SphereCollider>();
            col.radius = 0.35f;
            col.isTrigger = false;

            var hp = temp.AddComponent<Health>();
            hp.SetMaxHealth(40f);

            var drone = temp.AddComponent<Drone>();

            // Strip the auto-added BoxColliders on the visual children
            foreach (var c in temp.GetComponentsInChildren<BoxCollider>())
                Object.DestroyImmediate(c);
            foreach (var c in temp.GetComponentsInChildren<SphereCollider>())
                if (c.gameObject != temp) Object.DestroyImmediate(c);

            // Save as prefab
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(temp, DronePrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(temp);
            AssetDatabase.SaveAssets();
            return prefab.GetComponent<Drone>();
        }

        // -------------------------------------------------------------
        private static GameObject MakeSpawn(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.LookAt(new Vector3(0f, 2.5f, -3.5f)); // face tower
            return go;
        }

        private static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            // Use sharedMaterial.color if URP/Lit's _BaseColor is what we have;
            // editor-time, MaterialPropertyBlock via shared material is fine.
            var mat = new Material(r.sharedMaterial);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            else mat.color = c;
            r.sharedMaterial = mat;
        }

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
