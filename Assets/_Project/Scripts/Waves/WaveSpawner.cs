// Waves/WaveSpawner.cs — minimal wave manager.
//
// Each wave: N drones spawned at intervals. After all drones in a wave
// are dead, brief intermission, then next wave with more drones / faster
// spawn. After the configured wave count is cleared, GameManager.Victory().

using System.Collections;
using System.Collections.Generic;
using DroneDefense.Combat;
using DroneDefense.Core;
using DroneDefense.Enemies;
using UnityEngine;

namespace DroneDefense.Waves
{
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Drone dronePrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Transform target;       // tower
        [SerializeField] private Health targetHealth;

        [Header("Wave config")]
        [SerializeField] private int waveCount = 5;
        [SerializeField] private int firstWaveSize = 3;
        [SerializeField] private int waveSizeIncrement = 2;
        [SerializeField] private float spawnIntervalStart = 2.0f;
        [SerializeField] private float spawnIntervalMin = 0.6f;
        [SerializeField] private float spawnIntervalDecay = 0.15f;
        [SerializeField] private float intermissionSeconds = 4.0f;

        private readonly List<Drone> live = new List<Drone>();
        private int currentWave = 0;

        public int CurrentWave => currentWave;
        public int WaveCount => waveCount;
        public int LiveDrones => live.Count;

        public delegate void WaveEvent(int waveIndex, int waveCount);
        public event WaveEvent OnWaveStarted;
        public event WaveEvent OnWaveCleared;

        private void OnEnable()
        {
            if (gameManager != null) gameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState s)
        {
            if (s == GameState.Playing) StartCoroutine(RunWaves());
            if (s == GameState.GameOver || s == GameState.Victory)
            {
                StopAllCoroutines();
                ClearLive();
            }
        }

        private IEnumerator RunWaves()
        {
            for (currentWave = 1; currentWave <= waveCount; currentWave++)
            {
                int size = firstWaveSize + (currentWave - 1) * waveSizeIncrement;
                float interval = Mathf.Max(spawnIntervalMin,
                    spawnIntervalStart - spawnIntervalDecay * (currentWave - 1));

                OnWaveStarted?.Invoke(currentWave, waveCount);

                for (int i = 0; i < size; i++)
                {
                    SpawnOne();
                    yield return new WaitForSeconds(interval);
                }

                while (LiveDronesAlive() > 0) yield return null;

                OnWaveCleared?.Invoke(currentWave, waveCount);
                yield return new WaitForSeconds(intermissionSeconds);
            }

            if (gameManager != null) gameManager.Victory();
        }

        private void SpawnOne()
        {
            if (dronePrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
            Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Drone d = Instantiate(dronePrefab, sp.position, sp.rotation);
            d.Initialize(target, targetHealth);
            live.Add(d);
        }

        private int LiveDronesAlive()
        {
            int count = 0;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i] == null) { live.RemoveAt(i); continue; }
                if (live[i].IsAlive) count++;
            }
            return count;
        }

        private void ClearLive()
        {
            for (int i = 0; i < live.Count; i++)
                if (live[i] != null) Destroy(live[i].gameObject);
            live.Clear();
        }
    }
}
