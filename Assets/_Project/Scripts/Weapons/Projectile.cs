// Weapons/Projectile.cs — straight-line arrow with collision damage.
// Simple Rigidbody-driven flight. On contact with an IDamageable, applies
// damage and self-destructs. On contact with anything else, sticks for a
// moment then despawns.

using DroneDefense.Combat;
using UnityEngine;

namespace DroneDefense.Weapons
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float damage = 35f;
        [SerializeField] private float lifeSeconds = 5f;
        [SerializeField] private float stickSeconds = 1.5f;

        private Rigidbody rb;
        private Collider col;
        private float spawnTime;
        private bool stopped;

        public void Configure(float damageAmount)
        {
            damage = damageAmount;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            spawnTime = Time.time;
            stopped = false;
            if (col != null) col.enabled = true;
            if (rb != null) rb.isKinematic = false;
        }

        private void Update()
        {
            // Orient the arrow along its velocity vector for a clean look.
            if (!stopped && rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity);

            if (Time.time - spawnTime > lifeSeconds) Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (stopped) return;

            var dmg = collision.collider.GetComponentInParent<IDamageable>();
            ContactPoint cp = collision.GetContact(0);
            if (dmg != null && dmg.IsAlive)
            {
                dmg.TakeDamage(damage, cp.point, cp.normal);
                Destroy(gameObject);
                return;
            }

            // Stuck in geometry — disable physics, then despawn after a beat.
            stopped = true;
            if (rb != null) rb.isKinematic = true;
            if (col != null) col.enabled = false;
            transform.SetParent(collision.transform, worldPositionStays: true);
            Destroy(gameObject, stickSeconds);
        }
    }
}
