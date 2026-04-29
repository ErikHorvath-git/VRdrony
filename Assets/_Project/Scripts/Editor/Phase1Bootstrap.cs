// Phase1Bootstrap — automates the mechanical click-through of Phase 1.
//
// Why this exists: Unity's project setup involves a lot of inspector clicks
// that are easy to misclick and tedious to redo on a clean checkout.
// This script does the parts that are scriptable and stable across Unity 6.x.
// The wizard-driven parts (URP asset creation, XR plug-in management, Meta
// XR Project Setup Tool) stay manual on purpose — they benefit from a
// human eye and have a one-click flow inside the editor anyway.
//
// Idempotent: safe to run twice.

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DroneDefense.Editor
{
    public static class Phase1Bootstrap
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string PackageName = "com.erikhorvath.vrdrony";
        private const string ProductName = "VRdrony";
        private const string CompanyName = "ErikHorvath";

        // ---------------------------------------------------------------------
        // Aggregated menu — runs the whole Phase 1 sequence.
        // ---------------------------------------------------------------------
        [MenuItem("DroneDefense/Phase 1/Run All", priority = 1)]
        public static void RunAll()
        {
            SwitchPlatformToAndroid();
            ApplyPlayerSettings();
            CreateMainScene();
            VerifySetup();
            EditorUtility.DisplayDialog(
                "Phase 1 — Bootstrap complete",
                "Mechanical setup done. Manual steps remaining:\n\n" +
                "1. Create URP Asset:\n" +
                "   Assets > Create > Rendering > URP Asset (with Universal Renderer)\n" +
                "   Save as Assets/_Project/Settings/URP-Quest.asset\n\n" +
                "2. Assign URP Asset:\n" +
                "   Edit > Project Settings > Graphics > Default Render Pipeline\n" +
                "   Edit > Project Settings > Quality > Render Pipeline Asset\n\n" +
                "3. Enable Oculus XR Plug-in:\n" +
                "   Project Settings > XR Plug-in Management > Android > Oculus\n\n" +
                "4. Run Meta XR Project Setup Tool:\n" +
                "   Meta > Tools > Project Setup Tool — fix all required + recommended\n\n" +
                "5. Add XR Origin to Main scene:\n" +
                "   Hierarchy > XR > XR Origin (XR Rig) — after importing XRI samples\n\n" +
                "See docs/PHASE1_CHECKLIST.md for full walkthrough.",
                "OK");
        }

        // ---------------------------------------------------------------------
        // Step 1: switch to Android build target.
        // ---------------------------------------------------------------------
        [MenuItem("DroneDefense/Phase 1/1. Switch Platform to Android")]
        public static void SwitchPlatformToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("[Phase1] Already on Android. Skipping platform switch.");
                return;
            }

            Debug.Log("[Phase1] Switching active build target to Android...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android,
                BuildTarget.Android);
        }

        // ---------------------------------------------------------------------
        // Step 2: apply Quest 3 player settings.
        // ---------------------------------------------------------------------
        [MenuItem("DroneDefense/Phase 1/2. Apply Quest 3 Player Settings")]
        public static void ApplyPlayerSettings()
        {
            Debug.Log("[Phase1] Applying Quest 3 player settings...");

            // Identity
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);

            // Color space — Linear is required for proper PBR + URP on Quest.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Scripting backend + architecture — Quest store requires ARM64 + IL2CPP.
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // SDK levels — Meta XR SDK v85 requires API 29 minimum.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Graphics — Vulkan first, OpenGLES3 fallback. Disable auto so our order sticks.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[]
                {
                    UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                    UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                });

            // Multithreaded rendering — big perf win on Quest's mobile GPU.
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);

            // Orientation — Quest is always landscape.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Use 32-bit display buffer — Quest 3 panels are 8-bit per channel; keep 32.
            PlayerSettings.use32BitDisplayBuffer = true;

            // Active input handling — new Input System only.
            // (Cannot be set via PlayerSettings API in Unity 6; user gets a prompt
            //  when XRI is first imported. We just log the expectation.)
            Debug.Log("[Phase1] If prompted, accept switching to the new Input System.");

            AssetDatabase.SaveAssets();
            Debug.Log("[Phase1] Player settings applied.");
        }

        // ---------------------------------------------------------------------
        // Step 3: create Main.unity if missing, register in Build Settings.
        // ---------------------------------------------------------------------
        [MenuItem("DroneDefense/Phase 1/3. Create Main Scene")]
        public static void CreateMainScene()
        {
            EnsureFolder("Assets/_Project/Scenes");

            if (File.Exists(ScenePath))
            {
                Debug.Log($"[Phase1] {ScenePath} already exists. Skipping creation.");
            }
            else
            {
                Debug.Log($"[Phase1] Creating {ScenePath}...");
                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.DefaultGameObjects,
                    NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }

            // Add to Build Settings as scene 0.
            var newScene = new EditorBuildSettingsScene(ScenePath, true);
            var existing = EditorBuildSettings.scenes;

            bool alreadyListed = false;
            foreach (var s in existing)
            {
                if (s.path == ScenePath)
                {
                    alreadyListed = true;
                    break;
                }
            }

            if (!alreadyListed)
            {
                var combined = new EditorBuildSettingsScene[existing.Length + 1];
                combined[0] = newScene;
                for (int i = 0; i < existing.Length; i++)
                {
                    combined[i + 1] = existing[i];
                }
                EditorBuildSettings.scenes = combined;
                Debug.Log("[Phase1] Added Main.unity to Build Settings as scene 0.");
            }
            else
            {
                Debug.Log("[Phase1] Main.unity already in Build Settings.");
            }
        }

        // ---------------------------------------------------------------------
        // Verification — prints current vs expected so the user can spot drift.
        // ---------------------------------------------------------------------
        [MenuItem("DroneDefense/Phase 1/Verify Setup")]
        public static void VerifySetup()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== VRdrony Phase 1 Verification ===");

            Check(report, "Active build target",
                EditorUserBuildSettings.activeBuildTarget.ToString(),
                "Android");

            Check(report, "Color space",
                PlayerSettings.colorSpace.ToString(),
                "Linear");

            Check(report, "Scripting backend (Android)",
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString(),
                "IL2CPP");

            Check(report, "Android architecture",
                PlayerSettings.Android.targetArchitectures.ToString(),
                "ARM64");

            Check(report, "Min SDK",
                PlayerSettings.Android.minSdkVersion.ToString(),
                "AndroidApiLevel29");

            Check(report, "Use default graphics APIs (Android)",
                PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android).ToString(),
                "False");

            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            string apisJoined = string.Join(",", apis);
            Check(report, "Graphics APIs (Android)",
                apisJoined,
                "Vulkan,OpenGLES3");

            Check(report, "Package name",
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                PackageName);

            Check(report, "Main scene exists",
                File.Exists(ScenePath).ToString(),
                "True");

            // URP — informational; can't easily check without coupling to URP package symbols.
            var rp = GraphicsSettings.defaultRenderPipeline;
            report.AppendLine(rp != null
                ? $"  [info] Default render pipeline asset: {rp.name}"
                : "  [warn] No default render pipeline asset assigned (manual step pending)");

            Debug.Log(report.ToString());
        }

        private static void Check(System.Text.StringBuilder sb, string label,
            string actual, string expected)
        {
            string mark = string.Equals(actual, expected, System.StringComparison.OrdinalIgnoreCase)
                ? "OK"
                : "DRIFT";
            sb.AppendLine($"  [{mark}] {label}: {actual} (expected: {expected})");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
