using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;

namespace MathHorde.GameLogic
{
    public sealed class ActiveMathEvent
    {
        internal MathEventStateMachine Machine { get; }
        internal List<GateCarrier> CarrierBuffer { get; } = new List<GateCarrier>(4);
        public MathEventId EventId => Machine.EventId;
        public MathEventState State => Machine.State;
        public ProblemType Type { get; }
        public MathProblemId? ProblemId => ResourceGate.HasValue ? ResourceGate.Value.ProblemId : Problem?.Id;
        public MathProblem Problem { get; internal set; }
        public ResourceGateBinding? ResourceGate { get; internal set; }
        public ResourceType RewardResource { get; }
        public GateGoal Goal { get; }
        public int? TargetValue { get; }
        public double PreviewStartedAt { get; internal set; }
        public double DecisionOpenedAt { get; internal set; }
        public double DeadlineGameTime { get; internal set; }
        public int SnapshotValue { get; internal set; }
        public ResourceType SnapshotResource => RewardResource;
        public IReadOnlyList<GateCarrier> Carriers { get; }

        internal ActiveMathEvent(MathEventId id, ProblemType type, ResourceType resource, GateGoal goal, int? target)
        {
            Machine = new MathEventStateMachine(id);
            Type = type; RewardResource = resource; Goal = goal; TargetValue = target;
            Carriers = CarrierBuffer.AsReadOnly();
        }
    }

    public readonly struct GateCarrier
    {
        public int OptionIndex { get; }
        public LaneIndex Lane { get; }
        public Vec2 Position { get; }
        public string DisplayLabel { get; }
        public bool IsCorrect { get; }
        public GateCarrier(int optionIndex, LaneIndex lane, Vec2 position, string displayLabel, bool isCorrect)
        { OptionIndex = optionIndex; Lane = lane; Position = position; DisplayLabel = displayLabel; IsCorrect = isCorrect; }
    }

    public readonly struct ResolveCommand
    {
        public MathEventId EventId { get; }
        public LaneIndex SelectedLane { get; }
        public bool TimedOut { get; }
        public double GameTime { get; }
        public ResolveCommand(MathEventId eventId, LaneIndex selectedLane, bool timedOut, double gameTime)
        { EventId = eventId; SelectedLane = selectedLane; TimedOut = timedOut; GameTime = gameTime; }
    }
}
