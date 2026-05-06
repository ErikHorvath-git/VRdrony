// UI/ScoreDisplay.cs — drives a TextMesh from ScoreManager events.
// Used by both the Score panel and the Combo panel; pick which one
// via the `mode` field. Avoids the need for a Canvas.

using DroneDefense.Core;
using UnityEngine;

namespace DroneDefense.UI
{
    [RequireComponent(typeof(TextMesh))]
    public class ScoreDisplay : MonoBehaviour
    {
        public enum Mode { Score, Kills, Combo }

        [SerializeField] private Mode mode = Mode.Score;
        [SerializeField] private string prefix = "";

        private TextMesh label;
        private ScoreManager bound;

        private void Awake()
        {
            label = GetComponent<TextMesh>();
        }

        private void OnEnable()
        {
            // Defer subscription one frame in case ScoreManager isn't up yet.
            Invoke(nameof(TryBind), 0f);
        }

        private void OnDisable()
        {
            if (bound != null)
            {
                bound.OnScoreChanged -= HandleScore;
                bound.OnKillsChanged -= HandleKills;
                bound.OnComboChanged -= HandleCombo;
                bound = null;
            }
        }

        private void TryBind()
        {
            if (ScoreManager.Instance == null) { Invoke(nameof(TryBind), 0.2f); return; }
            bound = ScoreManager.Instance;
            bound.OnScoreChanged += HandleScore;
            bound.OnKillsChanged += HandleKills;
            bound.OnComboChanged += HandleCombo;
            // Push initial values.
            HandleScore(bound.Score);
            HandleKills(bound.Kills);
            HandleCombo(bound.Combo, bound.BestCombo);
        }

        private void HandleScore(int score)
        {
            if (mode == Mode.Score) label.text = $"{prefix}{score}";
        }

        private void HandleKills(int kills)
        {
            if (mode == Mode.Kills) label.text = $"{prefix}{kills}";
        }

        private void HandleCombo(int combo, int best)
        {
            if (mode != Mode.Combo) return;
            if (combo <= 1) label.text = $"{prefix}—";
            else label.text = $"{prefix}x{combo}";
        }
    }
}
