// Enemies/DroneShooter.cs — periodic ranged attack mounted on a drone.
//
// Every `fireInterval` seconds (with random jitter so the volley feels
// organic), the drone spawns an EnemyProjectile aimed at the configured
// target Transform (set via Drone.Initialize → forwarded here). The
// projectile flies straight; player can dodge with their head or block
// with the left-hand Shield.

using UnityEngine;
using DroneDefense.Combat;

namespace DroneDefense.Enemies
{
    public class DroneShooter : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float fireInterval = 4.0f;
        [SerializeField] private float firstShotDelay = 2.5f;
        [SerializeField] private float jitter = 1.2f;
        [SerializeField] private float minRangeToFire = 4f;
        [SerializeField] private float maxRangeToFire = 25f;

        [Header("Projectile")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 8f;
        [SerializeField] private float projectileDamage = 8f;
        [SerializeField] private float projectileLifetime = 6f;

        private Transform target;
        private Health targetHealth;
        private float nextFireTime;

        public void Initialize(Transform t, Health th)
        {
            target = t;
            targetHealth = th;
            nextFireTime = Time.time + firstShotDelay + Random.Range(-jitter, jitter);
        }

        private void Update()
        {
            if (target == null || projectilePrefab == null) return;
            if (Time.time < nextFireTime) return;

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist < minRangeToFire || dist > maxRangeToFire)
            {
                // Try again shortly — drone might be in or out of range.
                nextFireTime = Time.time + 0.5f;
                return;
            }

            FireOne();
            nextFireTime = Time.time + fireInterval + Random.Range(-jitter, jitter);
        }

        private void FireOne()
        {
            // Target transform is at the player's eye level → aim directly,
            // no extra Y bias (otherwise we'd shoot over the player's head).
            Vector3 dir = (target.position - transform.position).normalized;
            Quaternion rot = Quaternion.LookRotation(dir);
            var go = Instantiate(projectilePrefab, transform.position + dir * 0.5f, rot);

            var proj = go.GetComponent<EnemyProjectile>();
            if (proj != null) proj.Configure(projectileDamage, projectileLifetime, targetHealth);

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.linearVelocity = dir * projectileSpeed;
            }
        }
    }
}
