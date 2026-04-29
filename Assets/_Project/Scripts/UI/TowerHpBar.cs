// UI/TowerHpBar.cs — drives a horizontal fill bar from a Health component.
// Uses a child Transform's local X scale as the fill (no Canvas needed).
// Simpler than UGUI for a Quest world-space hp bar.

using DroneDefense.Combat;
using UnityEngine;

namespace DroneDefense.UI
{
    public class TowerHpBar : MonoBehaviour
    {
        [SerializeField] private Health source;
        [SerializeField] private Transform fill;       // child whose localScale.x is animated
        [SerializeField] private Renderer fillRenderer; // optional — colour by health
        [SerializeField] private Gradient colorByHealth;

        private void OnEnable()
        {
            if (source != null)
            {
                source.OnHealthChanged += UpdateBar;
                UpdateBar(source.CurrentHealth, source.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (source != null) source.OnHealthChanged -= UpdateBar;
        }

        private void UpdateBar(float cur, float max)
        {
            if (fill == null || max <= 0f) return;
            float n = Mathf.Clamp01(cur / max);
            Vector3 s = fill.localScale;
            s.x = n;
            fill.localScale = s;

            if (fillRenderer != null && colorByHealth != null)
                fillRenderer.material.color = colorByHealth.Evaluate(n);
        }
    }
}
