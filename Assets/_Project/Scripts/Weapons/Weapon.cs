// Weapons/Weapon.cs — abstract base for weapons held in a controller.
// Concrete weapons (MeleeWeapon, RangedWeapon) define how damage is
// applied. The base only carries shared fields and helpers.

using UnityEngine;

namespace DroneDefense.Weapons
{
    public abstract class Weapon : MonoBehaviour
    {
        [Header("Common")]
        [SerializeField] protected float damage = 25f;
        [SerializeField] protected LayerMask hitMask = ~0;

        public float Damage => damage;
    }
}
