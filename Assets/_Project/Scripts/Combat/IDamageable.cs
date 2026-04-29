// Combat/IDamageable.cs — uniform damage receiver contract.
// Anything that can take damage (tower, drone, eventually destructible
// scenery) implements this. Weapons hit-test for IDamageable, never
// for concrete types — keeps the weapon code agnostic.

using UnityEngine;

namespace DroneDefense.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal);
    }
}
