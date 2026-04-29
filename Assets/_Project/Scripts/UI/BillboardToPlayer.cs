// UI/BillboardToPlayer.cs — keeps a world-space UI element facing the
// player's headset. Cheaper than canvas billboard mode for one-off bars.

using UnityEngine;

namespace DroneDefense.UI
{
    public class BillboardToPlayer : MonoBehaviour
    {
        [SerializeField] private Transform target;   // head; if null, finds main camera

        private void LateUpdate()
        {
            if (target == null)
            {
                if (Camera.main == null) return;
                target = Camera.main.transform;
            }

            Vector3 dir = transform.position - target.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
