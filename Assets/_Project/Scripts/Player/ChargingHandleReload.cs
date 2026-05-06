// Player/ChargingHandleReload.cs — VR-realistic reload by pulling the
// charging handle on the right side of the receiver with the left hand.
//
// How it feels in-game:
//   1. Player keeps right hand on pistol grip and reaches LEFT hand
//      across to the charging-handle position on the receiver's RIGHT.
//   2. Player yanks the left hand REARWARD (toward themselves) — same
//      motion as cycling a real AK / M16 bolt.
//   3. We detect the rearward swipe in the gun's local-space Z and
//      fire a Reload() event: weapon cooldown resets so you can
//      immediately fire again, and an optional event hooks into a
//      sound / VFX / animation later.
//
// We DO NOT couple this to a controller button — the user wants the
// physical pull motion to be the action, and trigger-button reloading
// would feel arcade-y.

using System;
using DroneDefense.Weapons;
using UnityEngine;

namespace DroneDefense.Player
{
    public class ChargingHandleReload : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private RangedWeapon weapon;

        [Header("Charging-handle zone (gun local space)")]
        [Tooltip("Centre of the charging-handle hot-spot, in the GUN's local " +
                 "space. For an AK47 wrapped in our 'Crossbow' root the handle " +
                 "sits roughly above and slightly right of the receiver origin.")]
        [SerializeField] private Vector3 zoneCenter = new Vector3(0.06f, 0.10f, 0.05f);

        [Tooltip("Radius (m) of the hot-spot sphere that the left hand must " +
                 "be inside before a pull-back gesture counts as a reload.")]
        [SerializeField] private float zoneRadius = 0.18f;

        [Header("Pull-back gesture")]
        [Tooltip("How far (m) along the gun's local -Z the left hand must " +
                 "travel while inside the zone for a reload to trigger.")]
        [SerializeField] private float pullbackDistance = 0.10f;

        [Tooltip("Cooldown between successful reloads to avoid spam.")]
        [SerializeField] private float cooldown = 0.6f;

        public event Action OnReload;

        private float lastReloadTime = -999f;
        private bool inZone;
        private float entryLocalZ;

        private void LateUpdate()
        {
            if (leftHand == null) return;

            // Left hand position in gun's local space.
            Vector3 local = transform.InverseTransformPoint(leftHand.position);
            float dist = Vector3.Distance(local, zoneCenter);
            bool nowInside = dist <= zoneRadius;

            if (nowInside && !inZone)
            {
                // Entering the zone — record the local Z (forward axis).
                entryLocalZ = local.z;
                inZone = true;
            }
            else if (nowInside && inZone)
            {
                // While inside, watch for rearward travel (decreasing Z).
                float travelled = entryLocalZ - local.z;
                if (travelled >= pullbackDistance && Time.time - lastReloadTime >= cooldown)
                {
                    TriggerReload();
                    inZone = false; // require a fresh entry for the next reload
                }
            }
            else if (!nowInside && inZone)
            {
                inZone = false;
            }
        }

        private void TriggerReload()
        {
            lastReloadTime = Time.time;

            // For our MVP RangedWeapon, the cooldown gate is internal —
            // the simplest "reload" is to call Fire() with the cooldown
            // already expired, which it is by definition since we just
            // gestured. But we don't want to actually fire, so instead we
            // just invoke the event for sound / haptics / future state
            // resets. The weapon stays ready to fire on the next trigger.
            OnReload?.Invoke();

            if (weapon != null)
            {
                // Reset the next-fire timer so the player can shoot
                // immediately after the reload gesture.
                var so = weapon.GetType().GetField("nextFireTime",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (so != null) so.SetValue(weapon, 0f);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(zoneCenter, zoneRadius);
        }
#endif
    }
}
