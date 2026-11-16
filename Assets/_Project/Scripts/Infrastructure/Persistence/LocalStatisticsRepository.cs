using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using Newtonsoft.Json;
using UnityEngine;

// Contract core-domain.md 6., spec 15., local-persistence skill, ADR-011.
// Same UnityEngine-dependency / PlayMode-only-test caveat as
// LocalSaveRepository (unity-csharp skill F1-09 pitfall note).
namespace MathHorde.Infrastructure.Persistence
{
    public sealed class LocalStatisticsRepository : ILocalStatisticsRepository
    {
        public const string DefaultFileName = "statistics.json";

        private readonly string _filePath;

        public LocalStatisticsRepository(string rootDirectory = null, string fileName = DefaultFileName)
        {
            string root = rootDirectory ?? Application.persistentDataPath;
            _filePath = Path.Combine(root, fileName);
        }

        public IReadOnlyList<GameSessionRecord> LoadAll()
        {
            if (!AtomicFileStore.TryReadAllText(_filePath, out string json, out string readError))
            {
                if (File.Exists(_filePath))
                {
                    Debug.LogWarning($"[LocalStatisticsRepository] A statisztika-fájl nem olvasható ({_filePath}): {readError}. Üres napló.");
                }

                return Array.Empty<GameSessionRecord>();
            }

            try
            {
                var data = JsonConvert.DeserializeObject<StatisticsSaveData>(json) ?? new StatisticsSaveData();
                return data.Sessions.Select(ToDomain).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalStatisticsRepository] Sérült statisztika-fájl ({_filePath}): {ex.Message}. Üres napló.");
                return Array.Empty<GameSessionRecord>();
            }
        }

        public void Append(GameSessionRecord session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            List<GameSessionRecord> existing = LoadAll().ToList();
            existing.Add(session);

            var data = new StatisticsSaveData
            {
                SchemaVersion = StatisticsSaveData.CurrentSchemaVersion,
                Sessions = existing.Select(ToSaveData).ToList()
            };

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            AtomicFileStore.WriteAllText(_filePath, json);
        }

        public void Delete()
        {
            AtomicFileStore.DeleteIfExists(_filePath);
        }

        private static GameSessionRecord ToDomain(GameSessionSaveData s)
        {
            var attempts = (s.Attempts ?? new List<QuestionAttemptSaveData>())
                .Select(a => new QuestionAttempt(
                    new MathProblemId(a.QuestionId),
                    new GameSessionId(a.SessionId),
                    (a.SkillTags ?? new List<string>()).Select(ParseEnum<SkillTag>).ToArray(),
                    a.Difficulty,
                    a.CorrectAnswer,
                    a.SelectedAnswer,
                    a.WasCorrect,
                    a.ResponseTimeSeconds,
                    a.Timeout,
                    ParseEnum<ErrorCategory>(a.ErrorCategory),
                    a.AnsweredAt))
                .ToList();

            return new GameSessionRecord(
                new GameSessionId(s.SessionId),
                new LevelId(s.LevelId),
                s.Seed,
                s.StartedAt,
                s.FinishedAt,
                s.InitialChallengeLevel,
                s.FinalChallengeLevel,
                ParseEnum<SessionResult>(s.Result),
                ParseEnum<FailureReason>(s.FailureReason),
                ParseEnum<LossDiagnosis>(s.LossDiagnosis),
                s.MathAccuracy,
                s.TotalQuestions,
                s.CorrectQuestions,
                s.TimedOutQuestions,
                s.AppliedRewardEventIds ?? new List<Guid>(),
                attempts);
        }

        private static GameSessionSaveData ToSaveData(GameSessionRecord r)
        {
            return new GameSessionSaveData
            {
                SessionId = r.SessionId.Value,
                LevelId = r.LevelId.Value,
                Seed = r.Seed,
                StartedAt = r.StartedAt,
                FinishedAt = r.FinishedAt,
                InitialChallengeLevel = r.InitialChallengeLevel,
                FinalChallengeLevel = r.FinalChallengeLevel,
                Result = r.Result.ToString(),
                FailureReason = r.FailureReason.ToString(),
                LossDiagnosis = r.LossDiagnosis.ToString(),
                MathAccuracy = r.MathAccuracy,
                TotalQuestions = r.TotalQuestions,
                CorrectQuestions = r.CorrectQuestions,
                TimedOutQuestions = r.TimedOutQuestions,
                AppliedRewardEventIds = r.AppliedRewardEventIds.ToList(),
                Attempts = r.Attempts.Select(a => new QuestionAttemptSaveData
                {
                    QuestionId = a.QuestionId.Value,
                    SessionId = a.SessionId.Value,
                    SkillTags = a.SkillTags.Select(t => t.ToString()).ToList(),
                    Difficulty = a.Difficulty,
                    CorrectAnswer = a.CorrectAnswer,
                    SelectedAnswer = a.SelectedAnswer,
                    WasCorrect = a.WasCorrect,
                    ResponseTimeSeconds = a.ResponseTimeSeconds,
                    Timeout = a.Timeout,
                    ErrorCategory = a.ErrorCategory.ToString(),
                    AnsweredAt = a.AnsweredAt
                }).ToList()
            };
        }

        private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
        {
            if (Enum.TryParse<TEnum>(value, out var parsed))
            {
                return parsed;
            }

            Debug.LogWarning($"[LocalStatisticsRepository] Ismeretlen {typeof(TEnum).Name} érték a mentésben: \"{value}\". Alapértékre esik vissza.");
            return default;
        }
    }
}
