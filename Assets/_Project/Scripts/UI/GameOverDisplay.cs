// UI/GameOverDisplay.cs — shows a "GAME OVER" / "VICTORY" message tied
// to GameManager state changes. Hidden during Idle/Playing.

using DroneDefense.Core;
using UnityEngine;

namespace DroneDefense.UI
{
    [RequireComponent(typeof(TextMesh))]
    public class GameOverDisplay : MonoBehaviour
    {
        [SerializeField] private string playingText = "";
        [SerializeField] private string gameOverText = "GAME OVER";
        [SerializeField] private string victoryText = "VICTORY";
        [SerializeField] private Color gameOverColor = new Color(0.95f, 0.2f, 0.2f);
        [SerializeField] private Color victoryColor = new Color(0.95f, 0.85f, 0.2f);

        private TextMesh label;

        private void Awake()
        {
            label = GetComponent<TextMesh>();
            label.text = playingText;
        }

        private void OnEnable()
        {
            Invoke(nameof(TryBind), 0f);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= HandleState;
        }

        private void TryBind()
        {
            if (GameManager.Instance == null) { Invoke(nameof(TryBind), 0.2f); return; }
            GameManager.Instance.OnStateChanged += HandleState;
            HandleState(GameManager.Instance.State);
        }

        private void HandleState(GameState state)
        {
            switch (state)
            {
                case GameState.GameOver:
                    label.text = gameOverText;
                    label.color = gameOverColor;
                    break;
                case GameState.Victory:
                    label.text = victoryText;
                    label.color = victoryColor;
                    break;
                default:
                    label.text = playingText;
                    label.color = Color.white;
                    break;
            }
        }
    }
}
