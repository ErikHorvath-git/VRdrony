// Weapons/HitscanWeapon.cs — instant-hit raycast weapon.
//
// Per spec section 3.3 (Production-Grade Shooting Spec):
//   * Raycast origin = CAMERA position + forward, NOT the muzzle. This
//     is the #1 cause of bugs in junior FPS code — if the gun is
//     parented to the right hand and the muzzle pokes through a wall,
//     a muzzle-based raycast hits the wall. Camera-based raycast
//     follows where the player is *looking*, which is the experience
//     the player expects.
//   * Muzzle is used ONLY for VFX (flash + tracer start point).
//   * QueryTriggerInteraction.Ignore — trigger colliders (e.g. damage
//     volumes, hit-zone helpers) shouldn't register hits.
//   * LayerMask defaults to "Everything except Player" so the gun
//     can't shoot itself.
//   * Time.time >= nextFireTime gate; never accumulate cooldown via
//     `cd -= deltaTime` (drift over long sessions).

using DroneDefense.Combat;
using UnityEngine;

namespace DroneDefense.Weapons
{
    public class HitscanWeapon : Weapon
    {
        [Header("Hitscan-specific")]
        [SerializeField] private float range = 80f;
        [SerializeField] private float fireRate = 8f;          // rounds per second
        [SerializeField] private float spreadDegrees = 0.5f;   // pellet spread; 0 = perfect
        [SerializeField] private int pelletsPerShot = 1;
        [SerializeField] private float impactForce = 15f;
        [SerializeField] private LayerMask hitMaskOverride = ~0; // ~0 = Everything; we filter Player below

        [Header("Refs")]
        [Tooltip("Camera transform. If left null, Camera.main is used at runtime. " +
                 "MUST be the player's eye camera — raycast originates here, " +
                 "not from the gun's muzzle.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Muzzle transform — used ONLY for VFX (tracer start, flash). " +
                 "Hits are determined by the camera ray.")]
        [SerializeField] private Transform muzzle;

        [Header("VFX (optional)")]
        [SerializeField] private LineRenderer tracerPrefab;
        [SerializeField] private float tracerSeconds = 0.06f;
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject impactPrefab;

        private float nextFireTime;
        private int playerLayerMask;

        private void Awake()
        {
            // Cache the inverse of the Player layer once. We always exclude
            // it so the gun can't damage its owner.
            int playerLayer = LayerMask.NameToLayer("Player");
            playerLayerMask = (playerLayer >= 0) ? ~(1 << playerLayer) : ~0;
        }

        public void Fire()
        {
            if (Time.time < nextFireTime) return;
            nextFireTime = Time.time + 1f / Mathf.Max(0.0001f, fireRate);

            Transform cam = ResolveCamera();
            if (cam == null) return;

            Vector3 origin = cam.position;
            Vector3 baseDir = cam.forward;

            for (int i = 0; i < Mathf.Max(1, pelletsPerShot); i++)
            {
                Vector3 dir = ApplySpread(baseDir, spreadDegrees);
                LayerMask combinedMask = (LayerMask)((int)hitMaskOverride & playerLayerMask);

                bool hitSomething = Physics.Raycast(
                    origin, dir, out RaycastHit hit, range,
                    combinedMask, QueryTriggerInteraction.Ignore);

                Vector3 tracerEnd = hitSomething ? hit.point : origin + dir * range;

                // VFX origin is the visible muzzle, not the camera, so
                // tracers come out of the gun barrel even though the
                // damage ray came from the eye.
                Vector3 vfxStart = muzzle != null ? muzzle.position : origin;
                SpawnTracer(vfxStart, tracerEnd);
                if (muzzleFlashPrefab != null && muzzle != null)
                    Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation);

                if (!hitSomething) continue;

                if (impactPrefab != null)
                    Instantiate(impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));

                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                {
                    dmg.TakeDamage(damage, hit.point, hit.normal);
                }
                if (hit.rigidbody != null)
                {
                    hit.rigidbody.AddForceAtPosition(dir * impactForce, hit.point, ForceMode.Impulse);
                }
            }
        }

        private Transform ResolveCamera()
        {
            if (cameraTransform != null) return cameraTransform;
            var main = Camera.main;
            if (main != null)
            {
                cameraTransform = main.transform;
                return cameraTransform;
            }
            return null;
        }

        private static Vector3 ApplySpread(Vector3 forward, float deg)
        {
            if (deg <= 0f) return forward;
            // Cone spread — uniform inside a small angular cap.
            float maxRad = deg * Mathf.Deg2Rad;
            float u = Random.value;
            float cosTheta = 1f - u * (1f - Mathf.Cos(maxRad));
            float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
            float phi = Random.value * Mathf.PI * 2f;
            Vector3 local = new Vector3(sinTheta * Mathf.Cos(phi),
                                        sinTheta * Mathf.Sin(phi),
                                        cosTheta);
            return Quaternion.FromToRotation(Vector3.forward, forward) * local;
        }

        private void SpawnTracer(Vector3 start, Vector3 end)
        {
            if (tracerPrefab == null) return;
            var line = Instantiate(tracerPrefab);
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            Destroy(line.gameObject, tracerSeconds);
        }
    }
}
