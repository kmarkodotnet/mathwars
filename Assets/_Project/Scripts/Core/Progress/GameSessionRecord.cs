using System;
using System.Collections.Generic;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Contract core-domain.md 6. (spec 15.) — one played level's log,
    /// produced exactly once by IGameSessionService.Finalize (game-logic.md
    /// 8.).
    ///
    /// UPDATE (save-data-engineer task, ADR-011): QuestionAttempt now exists
    /// (Core.Progress), so `Attempts` is added here, additively — the
    /// constructor gained one new trailing parameter and the one existing
    /// call site (GameSessionService.Finalize) was updated to pass
    /// `Array.Empty&lt;QuestionAttempt&gt;()` until the Phase-4
    /// PerformanceTracker (F4-01) actually populates it per question. This
    /// class is now contract-complete.
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

        /// <summary>Per-question log for this session (contract core-domain.md 6., spec 15.). Empty in Phase 1-3 until F4-01's PerformanceTracker populates it.</summary>
        public IReadOnlyList<QuestionAttempt> Attempts { get; }

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
            IReadOnlyCollection<Guid> appliedRewardEventIds,
            IReadOnlyList<QuestionAttempt> attempts)
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
            Attempts = attempts ?? Array.Empty<QuestionAttempt>();
        }
    }
}
