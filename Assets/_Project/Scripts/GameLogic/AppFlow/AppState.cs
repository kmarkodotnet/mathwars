namespace MathHorde.GameLogic.AppFlow
{
    /// <summary>
    /// Application-level state (ARCHITECTURE.md 4. — one mermaid node each).
    /// Distinct from MathEventState (ARCHITECTURE.md 5.), which tracks a
    /// single math-event instance INSIDE Gameplay and never touches this
    /// enum.
    /// </summary>
    public enum AppState
    {
        Boot = 0,
        MainMenu = 1,
        WorldSelection = 2,
        Gameplay = 3,
        Pause = 4,
        LevelComplete = 5,
        GameOver = 6,
        ParentDashboard = 7,
        Settings = 8
    }
}
