// Player/WallBoundLocomotion.cs — clamps the XR Origin to a strafe segment
// along the castle wall. We let the player physically walk in their room,
// but if they try to push beyond the segment endpoints (via thumbstick
// strafe or a long real-room walk), we clamp the rig position so they
// can never leave the wall.
//
// The player faces +Z (drones come from +Z). The wall is along the X axis.
// Drag the rig anywhere along [minX, maxX]; vertical (Y) and depth (Z) stay
// at their start values.

using UnityEngine;

namespace DroneDefense.Player
{
    public class WallBoundLocomotion : MonoBehaviour
    {
        [Header("Bounds (world space)")]
        [SerializeField] private float minX = -3f;
        [SerializeField] private float maxX = 3f;

        [Header("Optional thumbstick strafe")]
        [Tooltip("If left null, only physical movement is clamped — no thumbstick locomotion.")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private float strafeSpeed = 1.5f;

        private float lockedY;
        private float lockedZ;

        private void Start()
        {
            lockedY = transform.position.y;
            lockedZ = transform.position.z;
        }

        private void LateUpdate()
        {
            // Optional thumbstick strafe — only compiles if the legacy Input
            // Manager is enabled. With "new Input System only", the player
            // simply walks physically and the clamp below does the rest.
#if ENABLE_LEGACY_INPUT_MANAGER
            if (headTransform != null)
            {
                float h = Input.GetAxis("Horizontal");
                if (Mathf.Abs(h) > 0.01f)
                {
                    Vector3 p = transform.position;
                    p.x += h * strafeSpeed * Time.deltaTime;
                    transform.position = p;
                }
            }
#endif

            // Hard clamp every frame regardless of input source.
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = lockedY;
            pos.z = lockedZ;
            transform.position = pos;
        }

        public void SetBounds(float min, float max)
        {
            minX = Mathf.Min(min, max);
            maxX = Mathf.Max(min, max);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 a = new Vector3(minX, transform.position.y, transform.position.z);
            Vector3 b = new Vector3(maxX, transform.position.y, transform.position.z);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.1f);
            Gizmos.DrawWireSphere(b, 0.1f);
        }
#endif
    }
}
