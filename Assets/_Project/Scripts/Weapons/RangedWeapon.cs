// Weapons/RangedWeapon.cs — crossbow. Fires a Projectile prefab from
// the muzzle transform when triggered.
//
// Trigger source: bound by RangedWeaponInput on the controller, or
// directly via the public Fire() method. We keep firing logic here so
// the same script works whether it's controller-triggered, AI-triggered,
// or test-triggered.

using UnityEngine;

namespace DroneDefense.Weapons
{
    public class RangedWeapon : Weapon
    {
        [Header("Ranged-specific")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float muzzleVelocity = 40f;
        [SerializeField] private float fireCooldown = 0.5f;
        [SerializeField] private bool useGravity = true;

        private float nextFireTime;

        public void Fire()
        {
            if (Time.time < nextFireTime) return;
            if (projectilePrefab == null || muzzle == null) return;

            Projectile p = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
            p.Configure(damage);

            var rb = p.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = useGravity;
                rb.linearVelocity = muzzle.forward * muzzleVelocity;
            }

            nextFireTime = Time.time + fireCooldown;
        }

        // Convenience: keyboard fallback for editor testing without VR.
        // Wrapped — only compiles if legacy Input Manager is enabled.
#if ENABLE_LEGACY_INPUT_MANAGER
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Fire();
        }
#endif
    }
}
