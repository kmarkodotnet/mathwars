using MathHorde.Core.Problems;
using MathHorde.Core.Resources;

namespace MathHorde.GameLogic.Events
{
    public readonly struct MathEventPreviewStarted : IGameEvent
    {
        public MathEventId EventId { get; }
        public ProblemType Type { get; }
        public MathProblemId? ProblemId { get; }
        public string DisplayExpression { get; }
        public ResourceType Resource { get; }
        public GateGoal Goal { get; }
        public int? TargetValue { get; }
        public double GameTime { get; }
        public MathEventPreviewStarted(MathEventId eventId, ProblemType type, MathProblemId? problemId, string displayExpression, ResourceType resource, GateGoal goal, int? targetValue, double gameTime)
        {
            EventId = eventId;
            Type = type;
            ProblemId = problemId;
            DisplayExpression = displayExpression;
            Resource = resource;
            Goal = goal;
            TargetValue = targetValue;
            GameTime = gameTime;
        }
    }

    public readonly struct MathEventGatesSpawned : IGameEvent
    {
        public MathEventId EventId { get; }
        public int SnapshotValue { get; }
        public ResourceType SnapshotResource { get; }
        public double GameTime { get; }
        public MathEventGatesSpawned(MathEventId eventId, int snapshotValue, ResourceType snapshotResource, double gameTime)
        {
            EventId = eventId;
            SnapshotValue = snapshotValue;
            SnapshotResource = snapshotResource;
            GameTime = gameTime;
        }
    }

    public readonly struct MathEventDecisionWindowOpened : IGameEvent
    {
        public MathEventId EventId { get; }
        public double DeadlineGameTime { get; }
        public double GameTime { get; }
        public MathEventDecisionWindowOpened(MathEventId eventId, double deadlineGameTime, double gameTime)
        {
            EventId = eventId;
            DeadlineGameTime = deadlineGameTime;
            GameTime = gameTime;
        }
    }

    public readonly struct MathEventCompleted : IGameEvent
    {
        public MathEventId EventId { get; }
        public int AppliedDelta { get; }
        public ResourceType Resource { get; }
        public double GameTime { get; }
        public MathEventCompleted(MathEventId eventId, int appliedDelta, ResourceType resource, double gameTime)
        {
            EventId = eventId;
            AppliedDelta = appliedDelta;
            Resource = resource;
            GameTime = gameTime;
        }
    }

    public readonly struct MathEventExpired : IGameEvent
    {
        public MathEventId EventId { get; }
        public string ReasonCode { get; }
        public double GameTime { get; }
        public MathEventExpired(MathEventId eventId, string reasonCode, double gameTime)
        {
            EventId = eventId;
            ReasonCode = reasonCode;
            GameTime = gameTime;
        }
    }

    public readonly struct MathEventPreparationFailed : IGameEvent
    {
        public MathEventId EventId { get; }
        public string ErrorCode { get; }
        public double GameTime { get; }
        public MathEventPreparationFailed(MathEventId eventId, string errorCode, double gameTime)
        {
            EventId = eventId;
            ErrorCode = errorCode;
            GameTime = gameTime;
        }
    }

}
