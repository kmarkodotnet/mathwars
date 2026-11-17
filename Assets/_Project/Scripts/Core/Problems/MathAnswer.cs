using System;
using MathHorde.Core.Common;

namespace MathHorde.Core.Problems
{
    /// <summary>
    /// The player's submitted answer to one MathEvent (contract
    /// core-domain.md 3.). Mirrors the QuestionAttempt timeout/correctness
    /// guard established in Core.Progress (save-data-engineer scope) for the
    /// same reason: a timed-out answer can never also be marked correct.
    /// </summary>
    public sealed class MathAnswer
    {
        public MathProblemId ProblemId { get; }

        /// <summary>Null only when TimedOut is true (no option was ever selected).</summary>
        public int? SelectedOptionIndex { get; }

        public LaneIndex? SelectedLane { get; }

        public bool WasCorrect { get; }

        /// <summary>Seconds from entering AwaitingDecision to the player's choice.</summary>
        public float ResponseTimeSeconds { get; }

        public bool TimedOut { get; }

        /// <summary>None when WasCorrect; Timeout when TimedOut.</summary>
        public ErrorCategory ErrorCategory { get; }

        public double AnsweredAtGameTime { get; }

        public MathAnswer(
            MathProblemId problemId,
            int? selectedOptionIndex,
            LaneIndex? selectedLane,
            bool wasCorrect,
            float responseTimeSeconds,
            bool timedOut,
            ErrorCategory errorCategory,
            double answeredAtGameTime)
        {
            if (responseTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(responseTimeSeconds), responseTimeSeconds, "ResponseTimeSeconds nem lehet negatív.");
            }

            if (timedOut && wasCorrect)
            {
                throw new ArgumentException("Timeout esetén WasCorrect nem lehet true.", nameof(wasCorrect));
            }

            if (selectedOptionIndex == null && !timedOut)
            {
                throw new ArgumentException("SelectedOptionIndex csak timeout esetén lehet null.", nameof(selectedOptionIndex));
            }

            if (wasCorrect && errorCategory != ErrorCategory.None)
            {
                throw new ArgumentException("Helyes válasz esetén ErrorCategory kötelezően None.", nameof(errorCategory));
            }

            if (timedOut && errorCategory != ErrorCategory.Timeout)
            {
                throw new ArgumentException("Timeout esetén ErrorCategory kötelezően Timeout.", nameof(errorCategory));
            }

            ProblemId = problemId;
            SelectedOptionIndex = selectedOptionIndex;
            SelectedLane = selectedLane;
            WasCorrect = wasCorrect;
            ResponseTimeSeconds = responseTimeSeconds;
            TimedOut = timedOut;
            ErrorCategory = errorCategory;
            AnsweredAtGameTime = answeredAtGameTime;
        }
    }
}
