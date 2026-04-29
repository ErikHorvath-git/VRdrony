// Core/GameState.cs — top-level game phase enum.

namespace DroneDefense.Core
{
    public enum GameState
    {
        Idle,       // Title / pre-game.
        Playing,    // A wave loop is running.
        GameOver,   // Tower HP hit zero.
        Victory     // All waves cleared.
    }
}
