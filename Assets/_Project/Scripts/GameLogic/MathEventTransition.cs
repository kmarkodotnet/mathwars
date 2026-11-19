namespace MathHorde.GameLogic
{
    /// <summary>One accepted lifecycle transition, stamped with the caller's shared game time.</summary>
    public readonly struct MathEventTransition
    {
        public MathEventId EventId { get; }
        public MathEventState From { get; }
        public MathEventState To { get; }
        public MathEventTrigger Trigger { get; }
        public double GameTime { get; }

        public MathEventTransition(MathEventId eventId, MathEventState from,
            MathEventState to, MathEventTrigger trigger, double gameTime)
        {
            EventId = eventId;
            From = from;
            To = to;
            Trigger = trigger;
            GameTime = gameTime;
        }
    }
}
