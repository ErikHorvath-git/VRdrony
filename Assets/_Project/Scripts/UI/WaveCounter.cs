// UI/WaveCounter.cs — text readout: "Wave 2 / 5".
// Uses Unity built-in TextMesh (3D world text). TMP would be nicer but
// adds a package dep — TextMesh is a one-line setup.

using DroneDefense.Waves;
using UnityEngine;

namespace DroneDefense.UI
{
    [RequireComponent(typeof(TextMesh))]
    public class WaveCounter : MonoBehaviour
    {
        [SerializeField] private WaveSpawner spawner;
        private TextMesh label;

        private void Awake()
        {
            label = GetComponent<TextMesh>();
        }

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.OnWaveStarted += Refresh;
                spawner.OnWaveCleared += Refresh;
            }
            Refresh(spawner != null ? spawner.CurrentWave : 0,
                    spawner != null ? spawner.WaveCount : 0);
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.OnWaveStarted -= Refresh;
                spawner.OnWaveCleared -= Refresh;
            }
        }

        private void Refresh(int waveIndex, int total)
        {
            if (label == null) return;
            label.text = total <= 0 ? "Ready" : $"Wave {waveIndex} / {total}";
        }
    }
}
