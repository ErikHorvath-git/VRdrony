// Player/ReloadAnimation.cs — visual feedback when the charging-handle
// reload gesture fires. Two parallel tweens:
//
//   1. Gun "kick" — pitch the held weapon upward by ~12° and snap back
//      over ~0.18 s. Reads like a recoil/cycle.
//   2. Charging-handle slide — if a child Transform named ChargingHandle
//      is present, slide it locally back along -Z by chargingHandleStroke
//      then return.
//
// We use a coroutine instead of an Animator because the gun is parented
// under a script-driven transform (TwoHandedGunGrip) and the kick has
// to layer ON TOP of that frame-by-frame world rotation. The kick is
// applied as a LOCAL rotation offset on a child "AnimRoot" that holds
// the model — TwoHandedGunGrip writes the gun root's world transform,
// AnimRoot adds its own local pitch on top.

using System.Collections;
using DroneDefense.Weapons;
using UnityEngine;

namespace DroneDefense.Player
{
    public class ReloadAnimation : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ChargingHandleReload reloadSource;
        [Tooltip("Local-space child whose rotation we kick during reload. " +
                 "If null we kick the model holder we can find under us.")]
        [SerializeField] private Transform animRoot;
        [Tooltip("Optional charging-handle child that slides back-and-forward.")]
        [SerializeField] private Transform chargingHandle;

        [Header("Kick (recoil)")]
        [SerializeField] private float kickPitchDegrees = 12f;
        [SerializeField] private float kickTime = 0.06f;     // up
        [SerializeField] private float settleTime = 0.18f;   // down

        [Header("Charging-handle slide")]
        [SerializeField] private float chargingHandleStroke = 0.06f; // metres
        [SerializeField] private float slideTime = 0.10f;
        [SerializeField] private float slideReturnTime = 0.14f;

        private Coroutine running;

        private void Awake()
        {
            // If no animRoot was assigned, find the gun model holder. The
            // MVPBootstrap parents the SM_Ak47 instance directly under the
            // Crossbow root — we can rotate that whole subtree.
            if (animRoot == null)
            {
                foreach (Transform child in transform)
                {
                    // Skip helper Transforms (Muzzle, ChargingHandle markers).
                    if (child.GetComponentInChildren<MeshRenderer>() != null)
                    {
                        animRoot = child;
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (reloadSource != null) reloadSource.OnReload += Play;
        }

        private void OnDisable()
        {
            if (reloadSource != null) reloadSource.OnReload -= Play;
        }

        public void Play()
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(PlayCo());
        }

        private IEnumerator PlayCo()
        {
            Quaternion start = animRoot != null ? animRoot.localRotation : Quaternion.identity;
            Quaternion peak  = start * Quaternion.Euler(-kickPitchDegrees, 0f, 0f);

            Vector3 chStart = chargingHandle != null ? chargingHandle.localPosition : Vector3.zero;
            Vector3 chBack  = chStart + new Vector3(0f, 0f, -chargingHandleStroke);

            // Phase 1 — kick up + slide back, in parallel.
            float t = 0f;
            float dur = Mathf.Max(kickTime, slideTime);
            while (t < dur)
            {
                t += Time.deltaTime;
                float kickN = Mathf.Clamp01(t / kickTime);
                if (animRoot != null) animRoot.localRotation = Quaternion.Slerp(start, peak, EaseOutCubic(kickN));

                float slideN = Mathf.Clamp01(t / slideTime);
                if (chargingHandle != null) chargingHandle.localPosition = Vector3.Lerp(chStart, chBack, EaseOutCubic(slideN));
                yield return null;
            }

            // Phase 2 — settle down + slide forward.
            t = 0f;
            dur = Mathf.Max(settleTime, slideReturnTime);
            while (t < dur)
            {
                t += Time.deltaTime;
                float settleN = Mathf.Clamp01(t / settleTime);
                if (animRoot != null) animRoot.localRotation = Quaternion.Slerp(peak, start, EaseInOutCubic(settleN));

                float retN = Mathf.Clamp01(t / slideReturnTime);
                if (chargingHandle != null) chargingHandle.localPosition = Vector3.Lerp(chBack, chStart, EaseInOutCubic(retN));
                yield return null;
            }

            if (animRoot != null) animRoot.localRotation = start;
            if (chargingHandle != null) chargingHandle.localPosition = chStart;
            running = null;
        }

        private static float EaseOutCubic(float x) { float v = 1f - x; return 1f - v * v * v; }
        private static float EaseInOutCubic(float x)
        {
            return x < 0.5f
                ? 4f * x * x * x
                : 1f - Mathf.Pow(-2f * x + 2f, 3f) * 0.5f;
        }
    }
}
