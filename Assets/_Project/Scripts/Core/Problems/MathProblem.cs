using System;
using System.Collections.Generic;
using MathHorde.Core.Expressions;

namespace MathHorde.Core.Problems
{
    /// <summary>
    /// A single generated math problem (contract core-domain.md 3., spec
    /// 6.1-6.5). Immutable (core-domain.md 0.).
    ///
    /// SCOPE NOTE (important for every future caller): core-domain.md 3.
    /// lists invariants I1-I11 (exactly one correct Option, pairwise-distinct
    /// Option values, 2..4 Options with unique lanes, non-empty SkillTags
    /// reflecting the actual tree operators, no division by zero, in-range
    /// arithmetic, unique integer solution for an unknown, uniform correct-
    /// lane distribution, distinct gate results with a minimum gap, negative
    /// results gated by DifficultyProfile, ...) and explicitly says they are
    /// enforced by the <c>ProblemValidator</c> (F2-08 task scope), not by
    /// this constructor. That split is deliberate: math-engine.md P4/P5 has
    /// the generator build CANDIDATE problems and run the validator before
    /// deciding whether to retry — "invalid" is a Result/validation-code
    /// outcome, never a thrown exception, for exactly these business rules.
    /// This constructor therefore only guards (a) required references being
    /// non-null and (b) the handful of single-field range/consistency
    /// comments that appear on MathProblem's own properties but are NOT part
    /// of the I1-I11 table (DifficultyLevel 1..10, ThinkTimeSeconds &gt; 0,
    /// TargetValue only for a target-selection/ReachTarget problem) — those are ordinary
    /// programmer-error guards, not business-rule validation.
    /// </summary>
    public sealed class MathProblem
    {
        public MathProblemId Id { get; }

        public ProblemType Type { get; }

        /// <summary>The tree to display/evaluate.</summary>
        public ExpressionNode Expression { get; }

        /// <summary>Pre-computed display form, e.g. "7 × 8 = ?".</summary>
        public string DisplayExpression { get; }

        public EvaluationResult CorrectAnswer { get; }

        /// <summary>2..4 elements (I4); never empty, even for TargetValueSelection.</summary>
        public IReadOnlyList<AnswerOption> Options { get; }

        /// <summary>1..10.</summary>
        public int DifficultyLevel { get; }

        /// <summary>At least 1 element (I5).</summary>
        public IReadOnlyList<SkillTag> SkillTags { get; }

        /// <summary>&gt; 0.</summary>
        public float ThinkTimeSeconds { get; }

        public RewardSpec Reward { get; }

        /// <summary>Reproducibility (spec 6.3).</summary>
        public int Seed { get; }

        /// <summary>The gate's goal, for its icon.</summary>
        public GateGoal Goal { get; }

        /// <summary>TargetValueSelection or a ReachTarget resource gate.</summary>
        public int? TargetValue { get; }

        /// <summary>Captured resource pool maximum. Candidate validity is checked by ProblemValidator.</summary>
        public int? ResourceCapacity { get; }

        /// <summary>Optional; may be null (spec 6.5).</summary>
        public string Explanation { get; }

        public MathProblem(
            MathProblemId id,
            ProblemType type,
            ExpressionNode expression,
            string displayExpression,
            EvaluationResult correctAnswer,
            IReadOnlyList<AnswerOption> options,
            int difficultyLevel,
            IReadOnlyList<SkillTag> skillTags,
            float thinkTimeSeconds,
            RewardSpec reward,
            int seed,
            GateGoal goal,
            int? targetValue,
            string explanation,
            int? resourceCapacity = null)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (displayExpression == null)
            {
                throw new ArgumentNullException(nameof(displayExpression));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (skillTags == null)
            {
                throw new ArgumentNullException(nameof(skillTags));
            }

            if (reward == null)
            {
                throw new ArgumentNullException(nameof(reward));
            }

            if (difficultyLevel < 1 || difficultyLevel > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(difficultyLevel), difficultyLevel, "DifficultyLevel 1..10 kötelező.");
            }

            if (thinkTimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(thinkTimeSeconds), thinkTimeSeconds, "ThinkTimeSeconds pozitív kötelező.");
            }

            if (targetValue.HasValue && type != ProblemType.TargetValueSelection
                && !(type == ProblemType.ResourceOperationComparison && goal == GateGoal.ReachTarget))
            {
                throw new ArgumentException("TargetValue requires target selection or a ReachTarget resource gate.", nameof(targetValue));
            }

            Id = id;
            Type = type;
            Expression = expression;
            DisplayExpression = displayExpression;
            CorrectAnswer = correctAnswer;
            Options = options;
            DifficultyLevel = difficultyLevel;
            SkillTags = skillTags;
            ThinkTimeSeconds = thinkTimeSeconds;
            Reward = reward;
            Seed = seed;
            Goal = goal;
            TargetValue = targetValue;
            ResourceCapacity = resourceCapacity;
            Explanation = explanation;
        }
    }
}
