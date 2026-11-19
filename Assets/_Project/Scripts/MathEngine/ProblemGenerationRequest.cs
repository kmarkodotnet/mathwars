using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.Core.Skills;

namespace MathHorde.MathEngine
{
    /// <summary>All generation inputs, including the captured resource context (ADR-013).</summary>
    public sealed class ProblemGenerationRequest
    {
        public int Seed { get; init; }
        public ProblemType Type { get; init; }
        public DifficultyProfile Difficulty { get; init; }
        public SkillProfile SkillProfile { get; init; }
        public int LaneCount { get; init; }
        public float ThinkTimeSeconds { get; init; }
        public ResourceType RewardResource { get; init; }
        public RewardSpec Reward { get; init; }
        public GateGoal Goal { get; init; }
        public int? ResourceSnapshotValue { get; init; }
        public int? ResourceCapacity { get; init; }
        public int? TargetValue { get; init; }
    }
}
