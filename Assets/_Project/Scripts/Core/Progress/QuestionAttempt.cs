using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// A single answered (or timed-out) math question, part of a
    /// GameSessionRecord's Attempts log (contract core-domain.md 6., spec
    /// 15.). This is the record the Infrastructure persistence layer writes
    /// to disk and the parent dashboard export reads back — the save-data
    /// task's own scope, now that MathProblemId/SkillTag/ErrorCategory
    /// exist (Core.Problems).
    /// </summary>
    public sealed class QuestionAttempt
    {
        public MathProblemId QuestionId { get; }
        public GameSessionId SessionId { get; }
        public IReadOnlyList<SkillTag> SkillTags { get; }
        public int Difficulty { get; }

        /// <summary>Display-form of the correct answer, e.g. "7 maradék 1".</summary>
        public string CorrectAnswer { get; }

        /// <summary>Null ⇒ timeout (no answer was given).</summary>
        public string SelectedAnswer { get; }

        public bool WasCorrect { get; }
        public float ResponseTimeSeconds { get; }
        public bool Timeout { get; }
        public ErrorCategory ErrorCategory { get; }
        public DateTimeOffset AnsweredAt { get; }

        public QuestionAttempt(
            MathProblemId questionId,
            GameSessionId sessionId,
            IReadOnlyList<SkillTag> skillTags,
            int difficulty,
            string correctAnswer,
            string selectedAnswer,
            bool wasCorrect,
            float responseTimeSeconds,
            bool timeout,
            ErrorCategory errorCategory,
            DateTimeOffset answeredAt)
        {
            if (difficulty < 1 || difficulty > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Difficulty 1..10 kötelező.");
            }

            if (responseTimeSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(responseTimeSeconds), responseTimeSeconds, "ResponseTimeSeconds nem lehet negatív.");
            }

            if (timeout && wasCorrect)
            {
                throw new ArgumentException("Timeout esetén WasCorrect nem lehet true.", nameof(wasCorrect));
            }

            QuestionId = questionId;
            SessionId = sessionId;
            SkillTags = skillTags ?? Array.Empty<SkillTag>();
            Difficulty = difficulty;
            CorrectAnswer = correctAnswer;
            SelectedAnswer = selectedAnswer;
            WasCorrect = wasCorrect;
            ResponseTimeSeconds = responseTimeSeconds;
            Timeout = timeout;
            ErrorCategory = errorCategory;
            AnsweredAt = answeredAt;
        }
    }
}
