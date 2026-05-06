// Effects/DestructionEffect.cs — short-lived particle burst used as the
// drone "exploded" VFX. Built procedurally so we don't need an asset.
//
// On Awake we configure the attached ParticleSystem (set up by the
// MVPBootstrap when it builds the prefab). On enable, we Play() and
// schedule a Destroy after the configured lifetime.

using UnityEngine;

namespace DroneDefense.Effects
{
    [RequireComponent(typeof(ParticleSystem))]
    public class DestructionEffect : MonoBehaviour
    {
        [SerializeField] private float autoDestroySeconds = 2.0f;
        [SerializeField] private bool playOnEnable = true;

        private ParticleSystem ps;

        private void Awake()
        {
            ps = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            if (playOnEnable && ps != null) ps.Play(true);
            Destroy(gameObject, autoDestroySeconds);
        }
    }
}
