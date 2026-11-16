using System;
using System.Collections.Generic;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Contract core-domain.md 6. (spec 15.) — one played level's log,
    /// produced exactly once by IGameSessionService.Finalize (game-logic.md
    /// 8.).
    ///
    /// SCOPE NOTE (flagged for the architect, not silently reinterpreted):
    /// the contract's full GameSessionRecord also carries
    /// `IReadOnlyList&lt;QuestionAttempt&gt; Attempts`. QuestionAttempt depends on
    /// MathProblemId/SkillTag/ErrorCategory (core-domain.md 3.), none of
    /// which exist in this codebase yet — they are MathEngine/Problems-layer
    /// types, out of this package's scope (F1-06/07/08, gameplay-dev) and a
    /// different agent's responsibility to land. Every OTHER field below
    /// has no such dependency and is included exactly as contracted.
    /// `Attempts` should be added here, additively, once QuestionAttempt
    /// exists — this class is otherwise contract-complete.
    /// </summary>
    public sealed class GameSessionRecord
    {
        public GameSessionId SessionId { get; }
        public LevelId LevelId { get; }
        public int Seed { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset? FinishedAt { get; }
        public int InitialChallengeLevel { get; }
        public int FinalChallengeLevel { get; }
        public SessionResult Result { get; }
        public FailureReason FailureReason { get; }

        /// <summary>Phase 1-3: always LossDiagnosis.None (core-domain.md 6.).</summary>
        public LossDiagnosis LossDiagnosis { get; }

        public float MathAccuracy { get; }
        public int TotalQuestions { get; }
        public int CorrectQuestions { get; }
        public int TimedOutQuestions { get; }

        /// <summary>Idempotency log (ARCHITECTURE 6.2) — event ids whose reward has already been applied.</summary>
        public IReadOnlyCollection<Guid> AppliedRewardEventIds { get; }

        public GameSessionRecord(
            GameSessionId sessionId,
            LevelId levelId,
            int seed,
            DateTimeOffset startedAt,
            DateTimeOffset? finishedAt,
            int initialChallengeLevel,
            int finalChallengeLevel,
            SessionResult result,
            FailureReason failureReason,
            LossDiagnosis lossDiagnosis,
            float mathAccuracy,
            int totalQuestions,
            int correctQuestions,
            int timedOutQuestions,
            IReadOnlyCollection<Guid> appliedRewardEventIds)
        {
            if (mathAccuracy < 0f || mathAccuracy > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(mathAccuracy), mathAccuracy, "MathAccuracy 0..1 kötelező.");
            }

            SessionId = sessionId;
            LevelId = levelId;
            Seed = seed;
            StartedAt = startedAt;
            FinishedAt = finishedAt;
            InitialChallengeLevel = initialChallengeLevel;
            FinalChallengeLevel = finalChallengeLevel;
            Result = result;
            FailureReason = failureReason;
            LossDiagnosis = lossDiagnosis;
            MathAccuracy = mathAccuracy;
            TotalQuestions = totalQuestions;
            CorrectQuestions = correctQuestions;
            TimedOutQuestions = timedOutQuestions;
            AppliedRewardEventIds = appliedRewardEventIds ?? Array.Empty<Guid>();
        }
    }
}
