// Player/GunHoldToggle.cs — grip-button grab/release for the gun.
//
// Behaviour:
//   - Default: the gun is "held" by both hands via TwoHandedGunGrip.
//     Rigidbody is kinematic, no gravity.
//   - Press right grip → the gun is RELEASED. TwoHandedGunGrip is
//     disabled, Rigidbody.useGravity flips on, kinematic flips off →
//     the gun falls under gravity.
//   - Press right grip again WITH the right hand within `pickupRadius`
//     of the gun → the gun is RE-GRABBED. Snaps back to the right
//     hand and TwoHandedGunGrip resumes.
//
// This is the lightweight grab — no full XRGrabInteractable from XRI.
// It feels right for our MVP: you can drop the gun deliberately, you
// can pick it back up when you reach for it. Fancy two-handed XRI
// grab/throw + collision physics is the next iteration.

using DroneDefense.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDefense.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class GunHoldToggle : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform rightHand;
        [SerializeField] private TwoHandedGunGrip twoHandedGrip;
        [SerializeField] private RangedWeapon weapon;

        [Header("Pickup")]
        [SerializeField] private float pickupRadius = 0.30f;

        public bool IsHeld { get; private set; } = true;

        private Rigidbody rb;
        private InputAction gripAction;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ApplyHeldState();
        }

        private void OnEnable()
        {
            gripAction = new InputAction(name: "GunGrip",
                type: InputActionType.Button,
                binding: "<XRController>{RightHand}/gripPressed");
            // Editor / dev fallback: G key toggles too.
            gripAction.AddBinding("<Keyboard>/g");
            gripAction.performed += HandleGrip;
            gripAction.Enable();
        }

        private void OnDisable()
        {
            if (gripAction == null) return;
            gripAction.performed -= HandleGrip;
            gripAction.Disable();
            gripAction.Dispose();
            gripAction = null;
        }

        private void HandleGrip(InputAction.CallbackContext _)
        {
            if (IsHeld)
            {
                Drop();
            }
            else
            {
                if (rightHand == null) return;
                if (Vector3.Distance(transform.position, rightHand.position) <= pickupRadius)
                    PickUp();
            }
        }

        private void Drop()
        {
            IsHeld = false;
            ApplyHeldState();
        }

        private void PickUp()
        {
            IsHeld = true;
            // Snap to the right hand so the grab feels instant.
            if (rightHand != null) transform.position = rightHand.position;
            ApplyHeldState();
        }

        private void ApplyHeldState()
        {
            if (rb != null)
            {
                rb.isKinematic = IsHeld;
                rb.useGravity = !IsHeld;
            }
            if (twoHandedGrip != null) twoHandedGrip.enabled = IsHeld;
            // Stop accidental fires while the gun is on the ground.
            if (weapon != null) weapon.enabled = IsHeld;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
#endif
    }
}
