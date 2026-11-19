using MathHorde.Core.Resources;

namespace MathHorde.GameLogic
{
    public readonly struct RewardApplication
    {
        public MathEventId EventId { get; }
        public RewardApplicationStatus Status { get; }
        public int RequestedDelta { get; }
        public int AppliedDelta { get; }
        public ResourceType Resource { get; }
        public RewardApplication(MathEventId eventId, RewardApplicationStatus status, int requestedDelta, int appliedDelta, ResourceType resource)
        { EventId = eventId; Status = status; RequestedDelta = requestedDelta; AppliedDelta = appliedDelta; Resource = resource; }
    }

    public enum RewardApplicationStatus
    {
        Applied = 0, AlreadyApplied = 1, SkippedIncorrect = 2, SkippedExpired = 3
    }
}
