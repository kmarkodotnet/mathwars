using MathHorde.Core.Resources;

namespace MathHorde.GameLogic.Events
{
    public readonly struct RewardApplied : IGameEvent
    {
        public MathEventId EventId { get; }
        public ResourceType Resource { get; }
        public int AppliedDelta { get; }
        public double GameTime { get; }

        public RewardApplied(MathEventId eventId, ResourceType resource, int appliedDelta, double gameTime)
        {
            EventId = eventId;
            Resource = resource;
            AppliedDelta = appliedDelta;
            GameTime = gameTime;
        }
    }

    public readonly struct RewardSkipped : IGameEvent
    {
        public MathEventId EventId { get; }
        public string ReasonCode { get; }
        public double GameTime { get; }

        public RewardSkipped(MathEventId eventId, string reasonCode, double gameTime)
        {
            EventId = eventId;
            ReasonCode = reasonCode;
            GameTime = gameTime;
        }
    }
}
