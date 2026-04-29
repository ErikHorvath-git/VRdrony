// Core/GameManager.cs — single-scene game state controller.
// Holds the GameState machine and the tower Health reference.
// Other systems subscribe to OnStateChanged to react.

using System;
using DroneDefense.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDefense.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private Health towerHealth;

        [Header("Behaviour")]
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private float restartDelaySeconds = 4f;

        public GameState State { get; private set; } = GameState.Idle;
        public Health TowerHealth => towerHealth;

        public event Action<GameState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (towerHealth != null) towerHealth.OnDied += HandleTowerDied;
        }

        private void OnDisable()
        {
            if (towerHealth != null) towerHealth.OnDied -= HandleTowerDied;
        }

        private void Start()
        {
            if (autoStartOnPlay) StartGame();
        }

        public void StartGame()
        {
            if (towerHealth != null) towerHealth.SetMaxHealth(towerHealth.MaxHealth, refill: true);
            SetState(GameState.Playing);
        }

        public void GameOver()
        {
            SetState(GameState.GameOver);
            Invoke(nameof(Restart), restartDelaySeconds);
        }

        public void Victory()
        {
            SetState(GameState.Victory);
            Invoke(nameof(Restart), restartDelaySeconds);
        }

        public void Restart()
        {
            // Reload current scene — simplest reliable reset.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleTowerDied()
        {
            if (State == GameState.Playing) GameOver();
        }

        private void SetState(GameState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(next);
        }
    }
}
