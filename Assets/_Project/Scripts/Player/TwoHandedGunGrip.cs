// Player/TwoHandedGunGrip.cs — realistic VR two-handed gun hold.
//
// Both real-life and VR shooters hold a rifle with the dominant hand on
// the pistol grip and the support hand on the forend, with the barrel
// pointing along the line from grip to forend. We replicate that:
//
//   gun.position = rightHand.position + gripOffsetFromRoot (so the
//                  pistol grip lands in the right palm)
//   gun.forward  = (leftHand.position - rightHand.position).normalized
//                  (so the barrel points wherever the support hand is
//                  steering it)
//
// When the left hand drifts away from a sensible support distance we
// fall back to letting the gun follow the right hand's rotation only —
// this prevents the gun snapping wildly when the player drops their
// left hand.
//
// Result: the player holds two controllers, raises both like a rifle,
// and the gun aims down whatever line their hands form. Pulling the
// right trigger fires from the muzzle (which is the actual barrel
// tip, not the pistol-grip origin).

using UnityEngine;

namespace DroneDefense.Player
{
    [DefaultExecutionOrder(50)] // run AFTER TrackedPoseDriver writes hand poses
    public class TwoHandedGunGrip : MonoBehaviour
    {
        [Header("Hands")]
        [SerializeField] private Transform rightHand;   // pistol-grip hand
        [SerializeField] private Transform leftHand;    // support / forend hand

        [Header("Gun-relative offsets")]
        [Tooltip("Local offset (in gun space) from the gun's transform " +
                 "origin to where the pistol grip sits in the right palm. " +
                 "Negated and applied as a position correction so gun.origin " +
                 "lands in the right hand.")]
        [SerializeField] private Vector3 gripLocalOffset = Vector3.zero;

        [Tooltip("Minimum hand separation required to use two-handed aim. " +
                 "Below this we fall back to right-hand-only rotation (e.g. " +
                 "when the player is holstering or has dropped a hand).")]
        [SerializeField] private float twoHandMinDistance = 0.15f;

        [Tooltip("If true, snaps roll so the gun stays upright (no barrel-roll). " +
                 "Use the right hand's up-vector to define what 'upright' is.")]
        [SerializeField] private bool stabiliseRoll = true;

        private void LateUpdate()
        {
            if (rightHand == null) return;

            // Anchor on the right hand.
            transform.position = rightHand.position - rightHand.rotation * gripLocalOffset;

            if (leftHand != null)
            {
                Vector3 lineToLeft = leftHand.position - rightHand.position;
                if (lineToLeft.magnitude >= twoHandMinDistance)
                {
                    Vector3 forward = lineToLeft.normalized;
                    Vector3 up = stabiliseRoll ? Vector3.up : rightHand.up;
                    transform.rotation = Quaternion.LookRotation(forward, up);
                    return;
                }
            }

            // Fallback: follow the right hand's rotation.
            transform.rotation = rightHand.rotation;
        }
    }
}
