using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.GameLogic.Events;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 6. Resolves immutable problem data without reading or changing
    /// live resources, rebinding gates, evaluating expressions or materializing answer objects.
    /// </summary>
    public sealed class GateResolutionService : IGateResolutionService
    {
        private readonly IGameEventSink _events;
        private readonly int _eventCapacity;
        private HashSet<MathEventId> _resolvedEvents;
        private int _reservedCount;

        /// <param name="eventCapacity">
        /// Maximum additional distinct resolutions between restores (normally the full level schedule count).
        /// Allocation happens here and in RestoreResolvedEvents, outside the session Tick.
        /// </param>
        public GateResolutionService(IGameEventSink events, int eventCapacity)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            if (eventCapacity < 0) throw new ArgumentOutOfRangeException(nameof(eventCapacity));
            _eventCapacity = eventCapacity;
            _reservedCount = eventCapacity;
            _resolvedEvents = new HashSet<MathEventId>(eventCapacity);
        }

        public bool IsResolved(MathEventId eventId) => _resolvedEvents.Contains(eventId);

        public void RestoreResolvedEvents(IReadOnlyCollection<Guid> resolvedEventIds)
        {
            if (resolvedEventIds == null) throw new ArgumentNullException(nameof(resolvedEventIds));
            int capacity = checked(resolvedEventIds.Count + _eventCapacity);
            var restored = new HashSet<MathEventId>(capacity);
            foreach (var id in resolvedEventIds) restored.Add(new MathEventId(id));
            _resolvedEvents = restored;
            _reservedCount = capacity;
        }

        public GateResolution Resolve(in ResolveCommand command, ActiveMathEvent mathEvent)
        {
            // Replay lookup precedes all task inspection: restored IDs need no surviving ActiveMathEvent.
            if (IsResolved(command.EventId))
                return new GateResolution(command.EventId, GateResolutionOutcome.Duplicate, false,
                    -1, 0, 0, 0, Core.Resources.ResourceType.Health, default);

            if (mathEvent == null) throw new ArgumentNullException(nameof(mathEvent));
            if (command.EventId != mathEvent.EventId)
                throw new ArgumentException("Command and active event identities differ.", nameof(command));
            if (double.IsNaN(command.GameTime) || double.IsInfinity(command.GameTime)
                || command.GameTime < mathEvent.DecisionOpenedAt
                || command.GameTime - mathEvent.DecisionOpenedAt > float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(command));

            // A deadline command is emitted after the director has already made the event Expired.
            // Session-ended/preparation-failed events never emit a command and must not create attempts.
            if (mathEvent.State != MathEventState.AwaitingDecision && mathEvent.State != MathEventState.Resolving
                && !(command.TimedOut && mathEvent.State == MathEventState.Expired))
                throw new InvalidOperationException("Only a decision or a finalized deadline can be resolved.");

            if (mathEvent.Problem == null)
                throw new InvalidOperationException("A finalized problem is required for resolution.");
            var reward = mathEvent.Problem.Reward;
            var problemId = mathEvent.Problem.Id;
            int basis = mathEvent.GateBaseValue;
            bool timedOut = command.TimedOut || command.GameTime >= mathEvent.DeadlineGameTime;
            int optionIndex = -1;
            int operationResult = 0;
            bool correct = false;
            var error = timedOut ? ErrorCategory.Timeout : ErrorCategory.Unclassified;

            if (!timedOut)
            {
                for (int i = 0; i < mathEvent.Problem.Options.Count; i++)
                {
                    var option = mathEvent.Problem.Options[i];
                    if (option.Lane != command.SelectedLane) continue;
                    optionIndex = option.OptionIndex;
                    operationResult = option.Value.Value;
                    correct = option.IsCorrect;
                    error = option.DistractorErrorModel ?? ErrorCategory.Unclassified;
                    break;
                }
                if (optionIndex < 0)
                    throw new ArgumentException("Selected lane is not an offered option.", nameof(command));
                if (correct) error = ErrorCategory.None;
            }

            int delta = timedOut ? 0 : !correct ? reward.IncorrectAmount : reward.FlatAmount;
            var answer = new MathAnswerSnapshot(problemId, timedOut ? (int?)null : optionIndex,
                timedOut ? (Core.Common.LaneIndex?)null : command.SelectedLane, correct,
                (float)(command.GameTime - mathEvent.DecisionOpenedAt), timedOut, error, command.GameTime);
            var outcome = timedOut ? GateResolutionOutcome.Expired
                : correct ? GateResolutionOutcome.CorrectRewarded : GateResolutionOutcome.IncorrectNoReward;
            var resolution = new GateResolution(command.EventId, outcome, correct, optionIndex,
                operationResult, basis, delta, reward.Resource, answer);

            // Never permit HashSet growth inside Tick. Invalid callers must fix startup reservation.
            if (_resolvedEvents.Count >= _reservedCount)
                throw new InvalidOperationException("Resolution capacity exhausted; reserve the full schedule before Tick.");
            _resolvedEvents.Add(command.EventId);
            _events.Publish(new MathEventResolved(command.EventId, command.SelectedLane, correct, command.GameTime));
            return resolution;
        }
    }
}
