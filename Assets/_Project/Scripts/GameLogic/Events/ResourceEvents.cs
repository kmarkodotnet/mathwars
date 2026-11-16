using MathHorde.Core.Resources;

namespace MathHorde.GameLogic.Events
{
    /// <summary>Contract game-logic.md 1. Emitted by ResourceService on every ApplyDelta/TryConsume/SetMax/Initialize.</summary>
    public readonly struct ResourceChanged : IGameEvent
    {
        public ResourceChange Change { get; }
        public double GameTime { get; }

        public ResourceChanged(ResourceChange change, double gameTime)
        {
            Change = change;
            GameTime = gameTime;
        }
    }
}
