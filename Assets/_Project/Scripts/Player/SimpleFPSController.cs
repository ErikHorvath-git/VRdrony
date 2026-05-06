// Player/SimpleFPSController.cs — non-VR fallback so the game is
// playable in the editor without a Quest headset connected.
//
// Mouse Y/X drives the camera pitch + yaw. Left mouse button calls
// Fire() on a referenced RangedWeapon. Position is fixed (the player
// stands on top of the tower).
//
// In a real Quest build, replace this rig with the XR Origin and a
// proper Action-based controller setup. The MVPBootstrap detects an
// XR Origin in the scene and skips creating this fallback when it's
// present.

using DroneDefense.Weapons;
using UnityEngine;

namespace DroneDefense.Player
{
    public class SimpleFPSController : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private Transform pitchPivot;       // usually the camera transform
        [SerializeField] private float lookSensitivity = 2.5f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private bool lockCursor = true;

        [Header("Weapon")]
        [SerializeField] private RangedWeapon rangedWeapon;
        [SerializeField] private bool autoFire = false;
        [SerializeField] private float autoFireInterval = 0.6f;

        private float yaw;
        private float pitch;
        private float autoFireTimer;

        private void Start()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
#endif
            yaw = transform.eulerAngles.y;
            pitch = pitchPivot != null ? pitchPivot.localEulerAngles.x : 0f;
            if (pitch > 180f) pitch -= 360f;
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            // Toggle cursor lock with Escape so editor isn't a hostage.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = (Cursor.lockState == CursorLockMode.Locked) ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = (Cursor.lockState != CursorLockMode.Locked);
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float mx = Input.GetAxis("Mouse X");
                float my = Input.GetAxis("Mouse Y");
                yaw   += mx * lookSensitivity;
                pitch -= my * lookSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            // Fire on left mouse OR Space.
            if (rangedWeapon != null)
            {
                if (autoFire)
                {
                    autoFireTimer += Time.deltaTime;
                    if (autoFireTimer >= autoFireInterval)
                    {
                        autoFireTimer = 0f;
                        rangedWeapon.Fire();
                    }
                }
                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
                {
                    rangedWeapon.Fire();
                }
            }
#endif
        }
    }
}
