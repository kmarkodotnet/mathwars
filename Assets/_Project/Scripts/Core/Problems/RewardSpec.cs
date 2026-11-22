using MathHorde.Core.Resources;

namespace MathHorde.Core.Problems
{
    /// <summary>Fixed health reward, independent of the problem arithmetic (ADR-018/019).
    /// Candidate invariants are enforced by the generator and ProblemValidator.</summary>
    public sealed class RewardSpec
    {
        public ResourceType Resource { get; }
        public int FlatAmount { get; }
        public int IncorrectAmount { get; }

        public RewardSpec(ResourceType resource, int flatAmount, int incorrectAmount = 0)
        { Resource = resource; FlatAmount = flatAmount; IncorrectAmount = incorrectAmount; }
    }
}
