using MathHorde.Core.Common;

namespace MathHorde.GameLogic.Events
{
    public readonly struct MathEventResolved : IGameEvent
    {
        public MathEventId EventId { get; }
        public LaneIndex SelectedLane { get; }
        public bool WasCorrect { get; }
        public double GameTime { get; }

        public MathEventResolved(MathEventId eventId, LaneIndex selectedLane, bool wasCorrect, double gameTime)
        {
            EventId = eventId;
            SelectedLane = selectedLane;
            WasCorrect = wasCorrect;
            GameTime = gameTime;
        }
    }
}
