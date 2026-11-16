namespace MathHorde.GameLogic.Events
{
    /// <summary>Contract game-logic.md 1. Every event struct carries the game-time it was raised at.</summary>
    public interface IGameEvent
    {
        double GameTime { get; }
    }
}
