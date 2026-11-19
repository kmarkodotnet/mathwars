using System;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// One event's lifecycle (ARCHITECTURE.md 5., ADR-014). This is a pure
    /// implementation helper, not an extension of IMathEventDirector's contract.
    /// The director owns scheduling, carriers, snapshots and resolution commands.
    /// There are no combat, wave or clock references, and no per-transition allocations.
    /// </summary>
    public sealed class MathEventStateMachine
    {
        private double _lastTransitionTime;

        public MathEventId EventId { get; }
        public MathEventState State { get; private set; } = MathEventState.Inactive;
        public bool IsTerminal => State == MathEventState.Completed || State == MathEventState.Expired;

        public event Action<MathEventTransition> Transitioned;

        public MathEventStateMachine(MathEventId eventId)
        {
            EventId = eventId;
        }

        public bool CanFire(MathEventTrigger trigger) => TryGetNext(trigger, out _);

        /// <summary>
        /// Invalid or repeated lifecycle inputs return false without notification.
        /// Accepted transitions use nondecreasing, finite session game time.
        /// SessionEnded expires every nonterminal state, including Resolving.
        /// </summary>
        public bool Fire(MathEventTrigger trigger, double gameTime)
        {
            if (double.IsNaN(gameTime) || double.IsInfinity(gameTime) || gameTime < _lastTransitionTime)
            {
                throw new ArgumentOutOfRangeException(nameof(gameTime), "Game time must be finite and nondecreasing.");
            }

            if (!TryGetNext(trigger, out MathEventState next))
            {
                return false;
            }

            var transition = new MathEventTransition(EventId, State, next, trigger, gameTime);
            State = next;
            _lastTransitionTime = gameTime;
            Transitioned?.Invoke(transition);
            return true;
        }

        private bool TryGetNext(MathEventTrigger trigger, out MathEventState next)
        {
            next = State;
            if (IsTerminal)
            {
                return false;
            }

            if (trigger == MathEventTrigger.SessionEnded)
            {
                next = MathEventState.Expired;
                return true;
            }

            switch (State)
            {
                case MathEventState.Inactive when trigger == MathEventTrigger.Schedule:
                    next = MathEventState.Preview;
                    return true;
                case MathEventState.Preview when trigger == MathEventTrigger.PreviewElapsed:
                    next = MathEventState.Approaching;
                    return true;
                case MathEventState.Approaching when trigger == MathEventTrigger.DecisionZoneEntered:
                    next = MathEventState.AwaitingDecision;
                    return true;
                case MathEventState.Approaching when trigger == MathEventTrigger.DeadlineReached:
                case MathEventState.AwaitingDecision when trigger == MathEventTrigger.DeadlineReached:
                    next = MathEventState.Expired;
                    return true;
                case MathEventState.AwaitingDecision when trigger == MathEventTrigger.LaneSelected:
                    next = MathEventState.Resolving;
                    return true;
                case MathEventState.Resolving when trigger == MathEventTrigger.RewardApplied:
                    next = MathEventState.Completed;
                    return true;
                default:
                    return false;
            }
        }
    }
}
