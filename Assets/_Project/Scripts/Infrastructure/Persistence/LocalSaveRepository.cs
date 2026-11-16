using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Skills;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Contract core-domain.md 6./4., spec 15., local-persistence skill, ADR-011.
// Depends on UnityEngine (Debug logging, and the default root directory is
// Application.persistentDataPath) — per the unity-csharp skill's F1-09
// pitfall note, any test touching this class must live under
// Tests/PlayMode/, never Tests/EditMode/.
namespace MathHorde.Infrastructure.Persistence
{
    public sealed class LocalSaveRepository : ILocalSaveRepository
    {
        public const string DefaultFileName = "profile.json";

        private readonly string _filePath;

        /// <param name="rootDirectory">
        /// Defaults to Application.persistentDataPath. Tests pass an
        /// isolated temp directory so repeated runs never read another
        /// run's leftover file and never touch the real device save.
        /// </param>
        public LocalSaveRepository(string rootDirectory = null, string fileName = DefaultFileName)
        {
            string root = rootDirectory ?? Application.persistentDataPath;
            _filePath = Path.Combine(root, fileName);
        }

        public LocalProfileSnapshot Load()
        {
            if (!AtomicFileStore.TryReadAllText(_filePath, out string json, out string readError))
            {
                // Missing file on first run is expected and NOT a warning;
                // anything else (locked/IO error) is worth surfacing.
                if (File.Exists(_filePath))
                {
                    Debug.LogWarning($"[LocalSaveRepository] A mentés nem olvasható ({_filePath}): {readError}. Friss profillal indul.");
                }

                return CreateFreshSnapshot();
            }

            try
            {
                var rawRoot = JObject.Parse(json);
                var migrated = SaveDataMigrator.MigrateToCurrent(rawRoot);
                var data = migrated.ToObject<PlayerProfileSaveData>();
                return ToDomain(data);
            }
            catch (Exception ex)
            {
                // Covers JsonReaderException (malformed JSON), missing
                // required fields, and any migration failure — the
                // contract's "no unhandled exception on a corrupt file"
                // rule applies to ALL of these, not just IO errors.
                Debug.LogError($"[LocalSaveRepository] Sérült mentésfájl ({_filePath}): {ex.Message}. Friss profillal indul.");
                return CreateFreshSnapshot();
            }
        }

        public void Save(LocalProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var data = ToSaveData(snapshot);
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            AtomicFileStore.WriteAllText(_filePath, json);
        }

        public void Delete()
        {
            AtomicFileStore.DeleteIfExists(_filePath);
        }

        private static LocalProfileSnapshot CreateFreshSnapshot()
        {
            var profile = new PlayerProfile(
                PlayerProfileId.New(),
                displayName: null,
                createdAt: DateTimeOffset.UtcNow,
                currentChallengeLevel: 1,
                highestChallengeLevel: 1,
                unlockedWorlds: Array.Empty<WorldId>(),
                completedLevels: Array.Empty<LevelId>());

            return new LocalProfileSnapshot(
                profile,
                new Dictionary<SkillTag, SkillStatistics>(),
                Array.Empty<DifficultyAdjustment>());
        }

        private static LocalProfileSnapshot ToDomain(PlayerProfileSaveData data)
        {
            var profile = new PlayerProfile(
                new PlayerProfileId(data.ProfileId),
                data.DisplayName,
                data.CreatedAt,
                data.CurrentChallengeLevel,
                data.HighestChallengeLevel,
                (data.UnlockedWorlds ?? new List<string>()).Select(w => new WorldId(w)).ToArray(),
                (data.CompletedLevels ?? new List<string>()).Select(l => new LevelId(l)).ToArray());

            var skills = (data.Skills ?? new List<SkillStatisticsSaveData>())
                .ToDictionary(
                    s => ParseEnum<SkillTag>(s.SkillId),
                    s => new SkillStatistics(
                        ParseEnum<SkillTag>(s.SkillId),
                        s.DifficultyLevel,
                        s.Attempts,
                        s.CorrectAnswers,
                        s.AverageResponseTimeSeconds,
                        s.MedianResponseTimeSeconds,
                        s.LastPracticedAt,
                        s.MasteryEstimate,
                        s.Confidence,
                        (s.TypicalErrors ?? new Dictionary<string, int>())
                            .ToDictionary(kv => ParseEnum<ErrorCategory>(kv.Key), kv => kv.Value)));

            var difficultyHistory = (data.DifficultyHistory ?? new List<DifficultyAdjustmentSaveData>())
                .Select(d => new DifficultyAdjustment(
                    d.PreviousLevel,
                    d.NewLevel,
                    d.Reason,
                    d.Timestamp,
                    d.TriggerMetrics ?? new Dictionary<string, double>()))
                .ToList();

            return new LocalProfileSnapshot(profile, skills, difficultyHistory);
        }

        private static PlayerProfileSaveData ToSaveData(LocalProfileSnapshot snapshot)
        {
            return new PlayerProfileSaveData
            {
                SchemaVersion = PlayerProfileSaveData.CurrentSchemaVersion,
                ProfileId = snapshot.Profile.Id.Value,
                DisplayName = snapshot.Profile.DisplayName,
                CreatedAt = snapshot.Profile.CreatedAt,
                CurrentChallengeLevel = snapshot.Profile.CurrentChallengeLevel,
                HighestChallengeLevel = snapshot.Profile.HighestChallengeLevel,
                UnlockedWorlds = snapshot.Profile.UnlockedWorlds.Select(w => w.Value).ToList(),
                CompletedLevels = snapshot.Profile.CompletedLevels.Select(l => l.Value).ToList(),
                Skills = snapshot.Skills.Values.Select(s => new SkillStatisticsSaveData
                {
                    SkillId = s.SkillId.ToString(),
                    DifficultyLevel = s.DifficultyLevel,
                    Attempts = s.Attempts,
                    CorrectAnswers = s.CorrectAnswers,
                    AverageResponseTimeSeconds = s.AverageResponseTimeSeconds,
                    MedianResponseTimeSeconds = s.MedianResponseTimeSeconds,
                    LastPracticedAt = s.LastPracticedAt,
                    MasteryEstimate = s.MasteryEstimate,
                    Confidence = s.Confidence,
                    TypicalErrors = s.TypicalErrors.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
                }).ToList(),
                DifficultyHistory = snapshot.DifficultyHistory.Select(d => new DifficultyAdjustmentSaveData
                {
                    PreviousLevel = d.PreviousLevel,
                    NewLevel = d.NewLevel,
                    Reason = d.Reason,
                    Timestamp = d.Timestamp,
                    TriggerMetrics = d.TriggerMetrics.ToDictionary(kv => kv.Key, kv => kv.Value)
                }).ToList()
            };
        }

        private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
        {
            if (Enum.TryParse<TEnum>(value, out var parsed))
            {
                return parsed;
            }

            Debug.LogWarning($"[LocalSaveRepository] Ismeretlen {typeof(TEnum).Name} érték a mentésben: \"{value}\". Alapértékre esik vissza.");
            return default;
        }
    }
}
