namespace MathHorde.GameLogic
{
    /// <summary>Per-event lifecycle, distinct from the app/session state (game-logic.md 5.).</summary>
    public enum MathEventState
    {
        Inactive = 0,
        Preview = 1,
        Approaching = 2,
        AwaitingDecision = 3,
        Resolving = 4,
        Completed = 5,
        Expired = 6
    }
}
