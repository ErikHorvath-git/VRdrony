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
using DroneDefense.Core;
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

        [Header("Scoring")]
        [Tooltip("If true, killing the drone with a weapon awards score. " +
                 "Self-detonations against the tower do NOT award score.")]
        [SerializeField] private bool awardScoreOnWeaponKill = true;

        private Rigidbody rb;
        private Health health;
        private Transform target;     // tower
        private Health targetHealth;
        private State state = State.Approach;
        private float spawnPhase;
        private bool selfDetonated;   // set when EnterAttack runs

        public bool IsAlive => state != State.Dead && health != null && health.IsAlive;

        public void Initialize(Transform towerTransform, Health towerHealth)
        {
            target = towerTransform;
            targetHealth = towerHealth;
            // Forward target to optional ranged-attack module on the same
            // GameObject so kamikaze drones can soften the player up with
            // periodic potshots.
            var shooter = GetComponent<DroneShooter>();
            if (shooter != null) shooter.Initialize(towerTransform, towerHealth);
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
            selfDetonated = true;
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

            SpawnDebris();

            // Score only when killed by weapons (not when crashing into tower).
            if (!selfDetonated && awardScoreOnWeaponKill && ScoreManager.Instance != null)
                ScoreManager.Instance.RegisterKill(transform.position);

            Destroy(gameObject);
        }

        // Tiny cubes that fly outward as physical chunks of the dead drone.
        // No prefab needed — we build them from primitives and let
        // gravity + Rigidbody do the rest. Auto-cleanup after a few
        // seconds so they don't pile up.
        private void SpawnDebris()
        {
            const int chunks = 6;
            for (int i = 0; i < chunks; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                d.name = "DroneDebris";
                d.transform.position = transform.position + Random.insideUnitSphere * 0.12f;
                d.transform.rotation = Random.rotation;
                d.transform.localScale = new Vector3(
                    Random.Range(0.05f, 0.10f),
                    Random.Range(0.04f, 0.08f),
                    Random.Range(0.05f, 0.12f));

                // Match the drone's dark colour so it reads as drone parts.
                var rend = d.GetComponent<Renderer>();
                if (rend != null && rend.sharedMaterial != null)
                {
                    var mat = new Material(rend.sharedMaterial);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.20f));
                    else mat.color = new Color(0.18f, 0.18f, 0.20f);
                    rend.sharedMaterial = mat;
                }

                var rb = d.AddComponent<Rigidbody>();
                rb.mass = 0.1f;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Vector3 outward = (Random.insideUnitSphere + Vector3.up * 0.4f).normalized * Random.Range(3.5f, 6.5f);
                rb.linearVelocity = outward;
                rb.angularVelocity = Random.insideUnitSphere * 12f;

                // Don't accumulate — clean up after the chunks settle.
                Destroy(d, 3.5f);
            }
        }
    }
}
