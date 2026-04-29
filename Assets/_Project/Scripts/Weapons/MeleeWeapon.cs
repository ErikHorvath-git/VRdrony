// Weapons/MeleeWeapon.cs — sword. Damages anything that contacts the
// "blade" trigger collider IF the tip is moving fast enough.
//
// Why tip-velocity gating: in VR, players brush against enemies all the
// time without "swinging". Requiring real swing speed makes hits feel
// earned and prevents accidental damage when a controller drifts.
//
// Implementation: attach this script to the sword root. Assign:
//   - tipPoint: a child Transform near the blade tip.
//   - bladeCollider: the trigger collider that hits enemies.
// Track tipPoint's velocity in FixedUpdate, and on OnTriggerEnter only
// apply damage if speed > minSwingSpeed.

using DroneDefense.Combat;
using UnityEngine;

namespace DroneDefense.Weapons
{
    [RequireComponent(typeof(Rigidbody))]
    public class MeleeWeapon : Weapon
    {
        [Header("Melee-specific")]
        [SerializeField] private Transform tipPoint;
        [SerializeField] private Collider bladeCollider;
        [SerializeField] private float minSwingSpeed = 1.5f;
        [SerializeField] private float hitCooldown = 0.25f;

        private Vector3 lastTipPos;
        private float currentTipSpeed;
        private float nextHitTime;

        private void Start()
        {
            if (tipPoint == null) tipPoint = transform;
            lastTipPos = tipPoint.position;

            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (bladeCollider != null) bladeCollider.isTrigger = true;
        }

        private void FixedUpdate()
        {
            if (tipPoint == null) return;
            Vector3 p = tipPoint.position;
            currentTipSpeed = (p - lastTipPos).magnitude / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            lastTipPos = p;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < nextHitTime) return;
            if (currentTipSpeed < minSwingSpeed) return;
            if (((1 << other.gameObject.layer) & hitMask) == 0) return;

            var dmg = other.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) return;

            Vector3 hitPoint = other.ClosestPoint(tipPoint.position);
            Vector3 hitNormal = (tipPoint.position - hitPoint).normalized;
            dmg.TakeDamage(damage, hitPoint, hitNormal);
            nextHitTime = Time.time + hitCooldown;
        }
    }
}
