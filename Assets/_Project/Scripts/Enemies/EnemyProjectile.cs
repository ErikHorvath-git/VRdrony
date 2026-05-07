// Enemies/EnemyProjectile.cs — projectile fired BY a drone AT the player.
//
// Behaviour:
//   - Flies straight along its initial velocity (Rigidbody.useGravity = false).
//   - Despawns after `lifetime` seconds even if it hits nothing.
//   - On contact with a Shield component → destroyed (blocked, no damage).
//   - On contact with anything else (player rig, tower, ground) →
//     damages the configured target health, then destroys itself.

using UnityEngine;
using DroneDefense.Combat;

namespace DroneDefense.Enemies
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float damage = 10f;
        [SerializeField] private float lifetime = 6f;

        private Health targetHealth;
        private float spawnTime;

        public void Configure(float dmg, float life, Health target)
        {
            damage = dmg;
            lifetime = life;
            targetHealth = target;
        }

        private void OnEnable()
        {
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (Time.time - spawnTime > lifetime) Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Shield blocks the round entirely — no damage dealt.
            var shield = collision.collider.GetComponentInParent<Shield>();
            if (shield != null)
            {
                shield.OnBlocked(transform.position);
                Destroy(gameObject);
                return;
            }

            // Damage the configured target on impact.
            if (targetHealth != null && targetHealth.IsAlive)
            {
                ContactPoint cp = collision.GetContact(0);
                targetHealth.TakeDamage(damage, cp.point, cp.normal);
            }

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Marker component for shield colliders. Drone projectiles destroy
    /// themselves on contact with a Shield instead of damaging the player.
    /// </summary>
    public class Shield : MonoBehaviour
    {
        public event System.Action<Vector3> OnHit;

        public void OnBlocked(Vector3 worldHitPoint)
        {
            OnHit?.Invoke(worldHitPoint);
        }
    }
}
