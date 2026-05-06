// Player/XRControllerWeaponInput.cs — listens for Quest 3 / generic
// XRController trigger button via the new Input System and calls
// RangedWeapon.Fire() on press.
//
// Why this script exists:
//   The XR Interaction Toolkit's XRDirectInteractor "Activated" event
//   is great when the weapon is grabbed/held, but our weapons are
//   parented directly under the controller transform — we never
//   "activate" them through XRI's grab mechanic. Polling the trigger
//   action directly is simpler and works regardless of XRI version.
//
// Bindings: by default we listen on
//   <XRController>{RightHand}/triggerPressed
// which is mapped on Quest controllers, generic OpenXR controllers,
// and the XR Device Simulator. Override via the bindingPath inspector
// field if you want LeftHand or a specific runtime.

using DroneDefense.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDefense.Player
{
    public class XRControllerWeaponInput : MonoBehaviour
    {
        public enum Hand { Right, Left }

        [SerializeField] private RangedWeapon weapon;
        [SerializeField] private Hand hand = Hand.Right;
        [Tooltip("Optional override binding path. If set, used instead of the default Right/Left trigger binding.")]
        [SerializeField] private string bindingPathOverride = "";

        private InputAction triggerAction;

        private void OnEnable()
        {
            string path = !string.IsNullOrEmpty(bindingPathOverride)
                ? bindingPathOverride
                : (hand == Hand.Right
                    ? "<XRController>{RightHand}/triggerPressed"
                    : "<XRController>{LeftHand}/triggerPressed");

            triggerAction = new InputAction(name: "Fire", type: InputActionType.Button, binding: path);
            // Fall-back bindings so the action also fires from the XR Device
            // Simulator's mouse/keyboard mappings and from desktop testing.
            triggerAction.AddBinding("<Keyboard>/space");
            triggerAction.AddBinding("<Mouse>/leftButton");
            triggerAction.performed += HandleFire;
            triggerAction.Enable();
        }

        private void OnDisable()
        {
            if (triggerAction == null) return;
            triggerAction.performed -= HandleFire;
            triggerAction.Disable();
            triggerAction.Dispose();
            triggerAction = null;
        }

        private void HandleFire(InputAction.CallbackContext _)
        {
            if (weapon != null) weapon.Fire();
        }
    }
}
