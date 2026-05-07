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

        [Tooltip("Optional ballistic weapon (RangedWeapon spawns projectiles).")]
        [SerializeField] private RangedWeapon weapon;
        [Tooltip("Optional hitscan weapon (instant raycast). " +
                 "Takes priority over the ballistic weapon if both are set.")]
        [SerializeField] private HitscanWeapon hitscan;
        [SerializeField] private Hand hand = Hand.Right;
        [Tooltip("Optional override binding path. If set, used instead of the default Right/Left trigger binding.")]
        [SerializeField] private string bindingPathOverride = "";

        private InputAction triggerAction;

        private void OnEnable()
        {
            string handTag = hand == Hand.Right ? "RightHand" : "LeftHand";

            triggerAction = new InputAction(name: "Fire", type: InputActionType.Button);

            if (!string.IsNullOrEmpty(bindingPathOverride))
            {
                triggerAction.AddBinding(bindingPathOverride);
            }
            else
            {
                // Quest 2 / 3 / Touch controllers report under multiple
                // device classes depending on the OpenXR / Oculus runtime
                // version. Bind ALL the common paths so we don't miss the
                // trigger on any platform.
                triggerAction.AddBinding($"<XRController>{{{handTag}}}/triggerPressed");
                triggerAction.AddBinding($"<XRController>{{{handTag}}}/trigger");
                triggerAction.AddBinding($"<OculusTouchController>{{{handTag}}}/triggerPressed");
                triggerAction.AddBinding($"<OculusTouchController>{{{handTag}}}/trigger");
                triggerAction.AddBinding($"<MetaQuestTouchPlusController>{{{handTag}}}/triggerPressed");
                triggerAction.AddBinding($"<MetaQuestTouchProController>{{{handTag}}}/triggerPressed");
            }

            // Editor / desktop fall-backs.
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
            if (hitscan != null) hitscan.Fire();
            else if (weapon != null) weapon.Fire();
        }
    }
}
