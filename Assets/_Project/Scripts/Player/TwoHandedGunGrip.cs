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

        [Tooltip("If TRUE the barrel direction is set to the line from " +
                 "right hand → left hand whenever the support hand is far " +
                 "enough away (twoHandMinDistance). " +
                 "If FALSE (default for VR realism) the gun simply follows " +
                 "the right controller's rotation — same as how shooters " +
                 "like Onward / Population One handle aim. The left hand " +
                 "is then purely visual support.")]
        [SerializeField] private bool useSupportHandToAim = true;

        [Tooltip("Only used when useSupportHandToAim = true.")]
        [SerializeField] private float twoHandMinDistance = 0.20f;

        [Tooltip("If TRUE the gun is rolled so its 'up' is world up — feels " +
                 "stable but not realistic. Disable to let the right wrist " +
                 "control the gun's roll axis. Only used when " +
                 "useSupportHandToAim = true.")]
        [SerializeField] private bool stabiliseRoll = false;

        private void LateUpdate()
        {
            if (rightHand == null) return;

            // Anchor on the right hand (pistol grip).
            transform.position = rightHand.position - rightHand.rotation * gripLocalOffset;

            if (useSupportHandToAim && leftHand != null)
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

            // Default: follow the right hand's rotation. The barrel points
            // wherever the right controller is pointing — natural VR aim.
            transform.rotation = rightHand.rotation;
        }
    }
}
