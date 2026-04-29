// Enemies/Drone.cs — single-state-machine kamikaze drone.
//
// States:
//   Approach: fly toward the tower, slowing as you near it.
//   Attack:   close enough to detonate — apply damage to tower, die.
//   Dead:     visuals off, collider off, ready to despawn.
//
// Why no NavMesh / pathing: the level is open air, drones come in
// straight-ish and only need to deal with a single static target.
// Pathing would be overkill for the MVP.

using DroneDefense.Combat;
using UnityEngine;

namespace DroneDefense.Enemies
{
    [RequireComponent(typeof(Rigidbody), typeof(Health))]
    public class Drone : MonoBehaviour, IDamageable
    {
        public enum State { Approach, Attack, Dead }

        [Header("Tuning")]
        [SerializeField] private float speed = 2.0f;
        [SerializeField] private float detonateDistance = 1.0f;
        [SerializeField] private float impactDamage = 15f;
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobFrequency = 1.5f;

        [Header("Refs")]
        [SerializeField] private GameObject explosionPrefab;

        private Rigidbody rb;
        private Health health;
        private Transform target;     // tower
        private Health targetHealth;
        private State state = State.Approach;
        private float spawnPhase;

        public bool IsAlive => state != State.Dead && health != null && health.IsAlive;

        public void Initialize(Transform towerTransform, Health towerHealth)
        {
            target = towerTransform;
            targetHealth = towerHealth;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 1f;

            health.OnDied += HandleSelfDied;
        }

        private void OnEnable()
        {
            spawnPhase = Random.value * Mathf.PI * 2f;
            state = State.Approach;
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDied -= HandleSelfDied;
        }

        private void FixedUpdate()
        {
            if (target == null || state == State.Dead)
            {
                rb.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            float dist = toTarget.magnitude;

            if (state == State.Approach)
            {
                if (dist <= detonateDistance)
                {
                    EnterAttack();
                }
                else
                {
                    Vector3 dir = toTarget.normalized;
                    Vector3 desired = dir * speed;
                    desired.y += Mathf.Sin(Time.time * bobFrequency + spawnPhase) * bobAmplitude;
                    rb.linearVelocity = desired;
                    if (desired.sqrMagnitude > 0.01f)
                        rb.MoveRotation(Quaternion.LookRotation(desired));
                }
            }
        }

        private void EnterAttack()
        {
            state = State.Attack;
            if (targetHealth != null && targetHealth.IsAlive)
                targetHealth.TakeDamage(impactDamage, transform.position, Vector3.up);

            // Self-kill — Health.OnDied will route to HandleSelfDied.
            health.TakeDamage(99999f, transform.position, Vector3.up);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (state == State.Dead) return;
            health.TakeDamage(amount, hitPoint, hitNormal);
        }

        private void HandleSelfDied()
        {
            state = State.Dead;
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
