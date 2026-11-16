using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;

namespace MathHorde.Core.Skills
{
    /// <summary>
    /// One skill's measured statistics (contract core-domain.md 4., spec
    /// 15./8.). MasteryEstimate and Confidence are supplied by the caller
    /// (F4-02 SkillAssessmentService computes them) — this type does not
    /// compute its own mastery formula, it only carries and validates the
    /// numbers; Accuracy is the one field the contract marks "származtatott"
    /// (derived), so it is a computed property instead of a constructor
    /// parameter.
    /// </summary>
    public sealed class SkillStatistics
    {
        public SkillTag SkillId { get; }
        public int DifficultyLevel { get; }
        public int Attempts { get; }
        public int CorrectAnswers { get; }

        /// <summary>Derived: CorrectAnswers / Attempts, 0 if Attempts == 0 (core-domain.md 4.).</summary>
        public float Accuracy => Attempts == 0 ? 0f : (float)CorrectAnswers / Attempts;

        public float AverageResponseTimeSeconds { get; }
        public float MedianResponseTimeSeconds { get; }
        public DateTimeOffset? LastPracticedAt { get; }

        /// <summary>0.0..1.0 — computed by SkillAssessmentService (F4-02), carried here as-is.</summary>
        public float MasteryEstimate { get; }

        /// <summary>0.0..1.0 — computed by SkillAssessmentService (F4-02), carried here as-is.</summary>
        public float Confidence { get; }

        public IReadOnlyDictionary<ErrorCategory, int> TypicalErrors { get; }

        public SkillStatistics(
            SkillTag skillId,
            int difficultyLevel,
            int attempts,
            int correctAnswers,
            float averageResponseTimeSeconds,
            float medianResponseTimeSeconds,
            DateTimeOffset? lastPracticedAt,
            float masteryEstimate,
            float confidence,
            IReadOnlyDictionary<ErrorCategory, int> typicalErrors)
        {
            if (difficultyLevel < 1 || difficultyLevel > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(difficultyLevel), difficultyLevel, "DifficultyLevel 1..10 kötelező.");
            }

            if (attempts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "Attempts nem lehet negatív.");
            }

            if (correctAnswers < 0 || correctAnswers > attempts)
            {
                throw new ArgumentOutOfRangeException(nameof(correctAnswers), correctAnswers, "0 <= CorrectAnswers <= Attempts kötelező.");
            }

            if (masteryEstimate < 0f || masteryEstimate > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(masteryEstimate), masteryEstimate, "MasteryEstimate 0..1 kötelező.");
            }

            if (confidence < 0f || confidence > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence 0..1 kötelező.");
            }

            SkillId = skillId;
            DifficultyLevel = difficultyLevel;
            Attempts = attempts;
            CorrectAnswers = correctAnswers;
            AverageResponseTimeSeconds = averageResponseTimeSeconds;
            MedianResponseTimeSeconds = medianResponseTimeSeconds;
            LastPracticedAt = lastPracticedAt;
            MasteryEstimate = masteryEstimate;
            Confidence = confidence;
            TypicalErrors = typicalErrors ?? new Dictionary<ErrorCategory, int>();
        }
    }
}
