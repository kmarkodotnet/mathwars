using System;
using System.Collections.Generic;

// DTOs get public setters by design (core-domain.md 0.: "publikus settert
// csak DTO kap") — they exist purely for Newtonsoft.Json round-tripping and
// are translated to/from the immutable Core domain types by
// LocalSaveRepository. See ADR-011 for the file-layout and versioning
// rationale.
namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// The root document written to profile.json. SchemaVersion 2 (ADR-011):
    /// HighestChallengeLevel and DifficultyHistory were added relative to
    /// the hypothetical V1 shape — see SaveDataMigrator.MigrateV1ToV2.
    /// </summary>
    public sealed class PlayerProfileSaveData
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public Guid ProfileId { get; set; }
        public string DisplayName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int CurrentChallengeLevel { get; set; }
        public int HighestChallengeLevel { get; set; }
        public List<string> UnlockedWorlds { get; set; } = new List<string>();
        public List<string> CompletedLevels { get; set; } = new List<string>();
        public List<SkillStatisticsSaveData> Skills { get; set; } = new List<SkillStatisticsSaveData>();
        public List<DifficultyAdjustmentSaveData> DifficultyHistory { get; set; } = new List<DifficultyAdjustmentSaveData>();
    }

    public sealed class SkillStatisticsSaveData
    {
        public string SkillId { get; set; }
        public int DifficultyLevel { get; set; }
        public int Attempts { get; set; }
        public int CorrectAnswers { get; set; }
        public float AverageResponseTimeSeconds { get; set; }
        public float MedianResponseTimeSeconds { get; set; }
        public DateTimeOffset? LastPracticedAt { get; set; }
        public float MasteryEstimate { get; set; }
        public float Confidence { get; set; }
        public Dictionary<string, int> TypicalErrors { get; set; } = new Dictionary<string, int>();
    }

    public sealed class DifficultyAdjustmentSaveData
    {
        public int PreviousLevel { get; set; }
        public int NewLevel { get; set; }
        public string Reason { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Dictionary<string, double> TriggerMetrics { get; set; } = new Dictionary<string, double>();
    }
}
