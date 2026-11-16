using System;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;

namespace MathHorde.Core.Levels
{
    /// <summary>Contract core-domain.md 7. ThinkTimeSecondsOverride &lt;= 0 means "use DifficultyProfile's value".</summary>
    public sealed class MathEventScheduleEntry
    {
        public double TriggerGameTime { get; }
        public ProblemType ProblemType { get; }
        public float PreviewDurationSeconds { get; }
        public float ThinkTimeSecondsOverride { get; }
        public ResourceType RewardResource { get; }
        public GateGoal Goal { get; }

        public MathEventScheduleEntry(
            double triggerGameTime,
            ProblemType problemType,
            float previewDurationSeconds,
            float thinkTimeSecondsOverride,
            ResourceType rewardResource,
            GateGoal goal)
        {
            if (triggerGameTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(triggerGameTime), triggerGameTime, "TriggerGameTime nem lehet negatív.");
            }

            if (previewDurationSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(previewDurationSeconds), previewDurationSeconds, "PreviewDurationSeconds > 0 kötelező.");
            }

            TriggerGameTime = triggerGameTime;
            ProblemType = problemType;
            PreviewDurationSeconds = previewDurationSeconds;
            ThinkTimeSecondsOverride = thinkTimeSecondsOverride;
            RewardResource = rewardResource;
            Goal = goal;
        }
    }
}
