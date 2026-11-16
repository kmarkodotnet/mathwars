using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MathHorde.Core.Progress;
using MathHorde.Core.Skills;
using Newtonsoft.Json;

// Parent dashboard export (spec 11.5, local-persistence skill: "Export:
// strukturált JSON vagy CSV, ugyanazon doménmodellből generálva, mint a
// szülői dashboard — nincs duplikált, divergálható adatforrás"). Reads
// EXCLUSIVELY through ILocalSaveRepository/ILocalStatisticsRepository — no
// second data source, no network call (ADR-011 6./7.).
namespace MathHorde.Infrastructure.Persistence
{
    public sealed class ProfileExportService
    {
        private readonly ILocalSaveRepository _saveRepository;
        private readonly ILocalStatisticsRepository _statisticsRepository;

        public ProfileExportService(ILocalSaveRepository saveRepository, ILocalStatisticsRepository statisticsRepository)
        {
            _saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            _statisticsRepository = statisticsRepository ?? throw new ArgumentNullException(nameof(statisticsRepository));
        }

        /// <summary>One consolidated JSON document: profile + skills + difficulty history + full session/attempt log.</summary>
        public string ExportToJson()
        {
            LocalProfileSnapshot snapshot = _saveRepository.Load();
            IReadOnlyList<GameSessionRecord> sessions = _statisticsRepository.LoadAll();

            var export = new
            {
                Profile = new
                {
                    Id = snapshot.Profile.Id.Value,
                    snapshot.Profile.DisplayName,
                    snapshot.Profile.CreatedAt,
                    snapshot.Profile.CurrentChallengeLevel,
                    snapshot.Profile.HighestChallengeLevel,
                    UnlockedWorlds = snapshot.Profile.UnlockedWorlds.Select(w => w.Value).ToArray(),
                    CompletedLevels = snapshot.Profile.CompletedLevels.Select(l => l.Value).ToArray()
                },
                Skills = snapshot.Skills.Values.Select(s => new
                {
                    SkillId = s.SkillId.ToString(),
                    s.DifficultyLevel,
                    s.Attempts,
                    s.CorrectAnswers,
                    s.Accuracy,
                    s.AverageResponseTimeSeconds,
                    s.MedianResponseTimeSeconds,
                    s.LastPracticedAt,
                    s.MasteryEstimate,
                    s.Confidence
                }).ToArray(),
                DifficultyHistory = snapshot.DifficultyHistory.Select(d => new
                {
                    d.PreviousLevel,
                    d.NewLevel,
                    d.Reason,
                    d.Timestamp
                }).ToArray(),
                Sessions = sessions.Select(s => new
                {
                    SessionId = s.SessionId.Value,
                    LevelId = s.LevelId.Value,
                    s.StartedAt,
                    s.FinishedAt,
                    Result = s.Result.ToString(),
                    s.MathAccuracy,
                    s.TotalQuestions,
                    s.CorrectQuestions,
                    s.TimedOutQuestions
                }).ToArray()
            };

            return JsonConvert.SerializeObject(export, Formatting.Indented);
        }

        /// <summary>One row per skill — the parent dashboard's per-skill table (spec 11.3/11.5).</summary>
        public string ExportSkillsToCsv()
        {
            LocalProfileSnapshot snapshot = _saveRepository.Load();

            var sb = new StringBuilder();
            sb.AppendLine("SkillId,DifficultyLevel,Attempts,CorrectAnswers,Accuracy,AverageResponseTimeSeconds,MedianResponseTimeSeconds,LastPracticedAt,MasteryEstimate,Confidence");

            foreach (SkillStatistics s in snapshot.Skills.Values.OrderBy(s => s.SkillId.ToString(), StringComparer.Ordinal))
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    CsvField(s.SkillId.ToString()),
                    CsvField(s.DifficultyLevel),
                    CsvField(s.Attempts),
                    CsvField(s.CorrectAnswers),
                    CsvField(s.Accuracy),
                    CsvField(s.AverageResponseTimeSeconds),
                    CsvField(s.MedianResponseTimeSeconds),
                    CsvField(s.LastPracticedAt?.ToString("O") ?? string.Empty),
                    CsvField(s.MasteryEstimate),
                    CsvField(s.Confidence)
                }));
            }

            return sb.ToString();
        }

        /// <summary>One row per played session — the parent dashboard's session history table (spec 11.5).</summary>
        public string ExportSessionsToCsv()
        {
            IReadOnlyList<GameSessionRecord> sessions = _statisticsRepository.LoadAll();

            var sb = new StringBuilder();
            sb.AppendLine("SessionId,LevelId,StartedAt,FinishedAt,Result,FailureReason,MathAccuracy,TotalQuestions,CorrectQuestions,TimedOutQuestions");

            foreach (GameSessionRecord s in sessions.OrderBy(s => s.StartedAt))
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    CsvField(s.SessionId.Value.ToString()),
                    CsvField(s.LevelId.Value),
                    CsvField(s.StartedAt.ToString("O")),
                    CsvField(s.FinishedAt?.ToString("O") ?? string.Empty),
                    CsvField(s.Result.ToString()),
                    CsvField(s.FailureReason.ToString()),
                    CsvField(s.MathAccuracy),
                    CsvField(s.TotalQuestions),
                    CsvField(s.CorrectQuestions),
                    CsvField(s.TimedOutQuestions)
                }));
            }

            return sb.ToString();
        }

        public void ExportJsonToFile(string path) => AtomicFileStore.WriteAllText(path, ExportToJson());

        public void ExportSkillsCsvToFile(string path) => AtomicFileStore.WriteAllText(path, ExportSkillsToCsv());

        public void ExportSessionsCsvToFile(string path) => AtomicFileStore.WriteAllText(path, ExportSessionsToCsv());

        private static string CsvField(object value)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            {
                return text;
            }

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
