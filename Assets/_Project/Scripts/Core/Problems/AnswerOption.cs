using System;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;

namespace MathHorde.Core.Problems
{
    /// <summary>
    /// One offered answer option on a lane or a gate (contract core-domain.md
    /// 3.). Immutable (core-domain.md 0.).
    ///
    /// SCOPE NOTE: the cross-option invariants I2-I4 (exactly one IsCorrect
    /// among a MathProblem's Options, pairwise-distinct Value, 2..4 options
    /// with unique 0..Count-1 Lane) are explicitly the ProblemValidator's job
    /// (core-domain.md 3., "a ProblemValidator kényszeríti", F2-08 task
    /// scope) — they span the whole Options collection, not a single
    /// AnswerOption, and the generator (F2-05..F2-07) must be able to build
    /// candidate options before validation decides whether to retry (P4/P5,
    /// math-engine.md 0.). This type therefore only enforces the per-option
    /// invariants stated directly on its own fields.
    /// </summary>
    public sealed class AnswerOption
    {
        /// <summary>0..N-1, assigned by the generator.</summary>
        public int OptionIndex { get; }

        /// <summary>Which lane this option arrives on.</summary>
        public LaneIndex Lane { get; }

        /// <summary>The number shown for an AnswerLaneSelection option.</summary>
        public EvaluationResult Value { get; }

        /// <summary>The gate operation shown for a ResourceOperationComparison option (e.g. BinaryOp(x, Unknown, 2) → "×2"). Null when not applicable.</summary>
        public ExpressionNode GateOperation { get; }

        public bool IsCorrect { get; }

        /// <summary>Null whenever IsCorrect is true — a correct option never carries a distractor error model.</summary>
        public ErrorCategory? DistractorErrorModel { get; }

        /// <summary>"56" or "×2" or "+25".</summary>
        public string DisplayLabel { get; }

        public AnswerOption(
            int optionIndex,
            LaneIndex lane,
            EvaluationResult value,
            ExpressionNode gateOperation,
            bool isCorrect,
            ErrorCategory? distractorErrorModel,
            string displayLabel)
        {
            if (optionIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(optionIndex), optionIndex, "OptionIndex nem lehet negatív.");
            }

            if (isCorrect && distractorErrorModel.HasValue)
            {
                throw new ArgumentException("Helyes válasz (IsCorrect == true) nem hordozhat DistractorErrorModel-t.", nameof(distractorErrorModel));
            }

            if (string.IsNullOrEmpty(displayLabel))
            {
                throw new ArgumentException("DisplayLabel nem lehet üres vagy null.", nameof(displayLabel));
            }

            OptionIndex = optionIndex;
            Lane = lane;
            Value = value;
            GateOperation = gateOperation;
            IsCorrect = isCorrect;
            DistractorErrorModel = distractorErrorModel;
            DisplayLabel = displayLabel;
        }
    }
}
