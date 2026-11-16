using System;
using System.Collections.Generic;

namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// The root document written to statistics.json: a growing, append-only
    /// list of played sessions (ADR-011 1.). SchemaVersion 1 — no migration
    /// exists yet for this document (unlike profile.json's V1→V2), but the
    /// field is present from day one per the local-persistence skill rule
    /// ("minden mentett dokumentum tartalmaz egy SchemaVersion mezőt").
    /// </summary>
    public sealed class StatisticsSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<GameSessionSaveData> Sessions { get; set; } = new List<GameSessionSaveData>();
    }

    public sealed class GameSessionSaveData
    {
        public Guid SessionId { get; set; }
        public string LevelId { get; set; }
        public int Seed { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public int InitialChallengeLevel { get; set; }
        public int FinalChallengeLevel { get; set; }
        public string Result { get; set; }
        public string FailureReason { get; set; }
        public string LossDiagnosis { get; set; }
        public float MathAccuracy { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectQuestions { get; set; }
        public int TimedOutQuestions { get; set; }
        public List<Guid> AppliedRewardEventIds { get; set; } = new List<Guid>();
        public List<QuestionAttemptSaveData> Attempts { get; set; } = new List<QuestionAttemptSaveData>();
    }

    public sealed class QuestionAttemptSaveData
    {
        public Guid QuestionId { get; set; }
        public Guid SessionId { get; set; }
        public List<string> SkillTags { get; set; } = new List<string>();
        public int Difficulty { get; set; }
        public string CorrectAnswer { get; set; }
        public string SelectedAnswer { get; set; }
        public bool WasCorrect { get; set; }
        public float ResponseTimeSeconds { get; set; }
        public bool Timeout { get; set; }
        public string ErrorCategory { get; set; }
        public DateTimeOffset AnsweredAt { get; set; }
    }
}
