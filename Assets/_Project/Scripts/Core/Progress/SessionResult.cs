namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Contract core-domain.md 6. Needed by the SessionEnded event payload
    /// (F1-05, MathHorde.GameLogic.Events) ahead of the full GameSessionRecord
    /// type, which is a separate task's scope.
    /// </summary>
    public enum SessionResult
    {
        InProgress,
        Victory,
        Defeat,
        Abandoned
    }
}
