// Core/ScoreManager.cs — global score singleton.
// Tracks total kills, current score (with combo multiplier), best combo.
// UI components subscribe to OnScoreChanged / OnComboChanged for display.

using System;
using UnityEngine;

namespace DroneDefense.Core
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Tuning")]
        [SerializeField] private int killBaseScore = 100;
        [SerializeField] private float comboWindowSeconds = 3f;
        [SerializeField] private int maxComboMultiplier = 5;

        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }

        public event Action<int> OnScoreChanged;     // total score
        public event Action<int, int> OnComboChanged; // current, best
        public event Action<int> OnKillsChanged;

        private float lastKillTime = -999f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Decay combo if window expires.
            if (Combo > 0 && Time.time - lastKillTime > comboWindowSeconds)
            {
                Combo = 0;
                OnComboChanged?.Invoke(Combo, BestCombo);
            }
        }

        public void RegisterKill(Vector3 worldPosition)
        {
            float now = Time.time;
            if (now - lastKillTime <= comboWindowSeconds) Combo = Mathf.Min(Combo + 1, maxComboMultiplier * 4);
            else Combo = 1;
            lastKillTime = now;

            int multiplier = Mathf.Clamp(1 + (Combo - 1) / 3, 1, maxComboMultiplier);
            int gained = killBaseScore * multiplier;
            Score += gained;
            Kills += 1;
            if (Combo > BestCombo) BestCombo = Combo;

            OnScoreChanged?.Invoke(Score);
            OnKillsChanged?.Invoke(Kills);
            OnComboChanged?.Invoke(Combo, BestCombo);
        }

        public void ResetScore()
        {
            Score = 0;
            Kills = 0;
            Combo = 0;
            BestCombo = 0;
            lastKillTime = -999f;
            OnScoreChanged?.Invoke(Score);
            OnKillsChanged?.Invoke(Kills);
            OnComboChanged?.Invoke(Combo, BestCombo);
        }
    }
}
