using MathHorde.Core.Problems;
using MathHorde.Core.Resources;

namespace MathHorde.GameLogic
{
    public enum GateResolutionOutcome
    {
        CorrectRewarded = 0,
        IncorrectNoReward = 1,
        Expired = 2,
        Duplicate = 3
    }

    /// <summary>A raw mathematical result. Only RewardService applies the delta to live resources.</summary>
    public readonly struct GateResolution
    {
        public MathEventId EventId { get; }
        public GateResolutionOutcome Outcome { get; }
        public bool WasCorrect { get; }
        public int SelectedOptionIndex { get; }
        public int OperationResult { get; }
        public int GateBaseValue { get; }
        public int RewardDelta { get; }
        public ResourceType Resource { get; }

        /// <summary>Default for Duplicate: a replay must not produce another attempt.</summary>
        public MathAnswerSnapshot Answer { get; }

        public GateResolution(MathEventId eventId, GateResolutionOutcome outcome, bool wasCorrect,
            int selectedOptionIndex, int operationResult, int gateBaseValue, int rewardDelta,
            ResourceType resource, MathAnswerSnapshot answer)
        {
            EventId = eventId;
            Outcome = outcome;
            WasCorrect = wasCorrect;
            SelectedOptionIndex = selectedOptionIndex;
            OperationResult = operationResult;
            GateBaseValue = gateBaseValue;
            RewardDelta = rewardDelta;
            Resource = resource;
            Answer = answer;
        }
    }
}
