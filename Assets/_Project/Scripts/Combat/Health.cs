// Combat/Health.cs — generic HP component used by tower and drones alike.
// Why one component for both: the only difference is configured max HP
// and what listens to OnDied. Specialised subclasses are not needed.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace DroneDefense.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath = false;

        // UnityEvents are exposed for designer wiring in the inspector;
        // C# events are exposed for typed gameplay code.
        public UnityEvent<float, float> onHealthChanged;   // (current, max)
        public UnityEvent onDied;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;
        public float Normalized => Mathf.Approximately(maxHealth, 0f) ? 0f : CurrentHealth / maxHealth;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = Mathf.Max(0f, value);
            if (refill) CurrentHealth = maxHealth;
            RaiseChanged();
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!IsAlive || amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            RaiseChanged();

            if (CurrentHealth <= 0f)
            {
                OnDied?.Invoke();
                onDied?.Invoke();
                if (destroyOnDeath) Destroy(gameObject);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            onHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }
    }
}
