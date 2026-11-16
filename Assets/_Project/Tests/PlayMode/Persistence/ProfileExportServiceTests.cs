// F6-09/ADR-011 6. — JSON/CSV export, built only from the same domain model
// the repositories return (no duplicated/divergeable data source).
using System;
using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Skills;
using MathHorde.Infrastructure.Persistence;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.Persistence
{
    [TestFixture]
    public class ProfileExportServiceTests
    {
        private TempSaveDirectory _tempDir;
        private LocalSaveRepository _saveRepository;
        private LocalStatisticsRepository _statisticsRepository;
        private ProfileExportService _exportService;

        [SetUp]
        public void SetUp()
        {
            _tempDir = new TempSaveDirectory();
            _saveRepository = new LocalSaveRepository(_tempDir.Path);
            _statisticsRepository = new LocalStatisticsRepository(_tempDir.Path);
            _exportService = new ProfileExportService(_saveRepository, _statisticsRepository);

            var profile = new PlayerProfile(
                PlayerProfileId.New(), "Benedek", DateTimeOffset.UtcNow, 3, 3,
                new[] { new WorldId("w1") }, new[] { new LevelId("w1-l01") });
            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Addition] = new SkillStatistics(SkillTag.Addition, 2, 10, 8, 2f, 1.9f, DateTimeOffset.UtcNow, 0.7f, 0.6f, null)
            };
            _saveRepository.Save(new LocalProfileSnapshot(profile, skills, Array.Empty<DifficultyAdjustment>()));

            var sessionId = GameSessionId.New();
            _statisticsRepository.Append(new GameSessionRecord(
                sessionId, new LevelId("w1-l01"), 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                3, 3, SessionResult.Victory, FailureReason.None, LossDiagnosis.None,
                1.0f, 5, 5, 0, Array.Empty<Guid>(), Array.Empty<QuestionAttempt>()));
        }

        [TearDown]
        public void TearDown() => _tempDir.Dispose();

        [Test]
        public void ExportToJson_ContainsProfileDisplayName()
        {
            string json = _exportService.ExportToJson();

            Assert.That(json, Does.Contain("Benedek"));
        }

        [Test]
        public void ExportToJson_ContainsSkillAndSessionData()
        {
            string json = _exportService.ExportToJson();

            Assert.That(json, Does.Contain("Addition"));
            Assert.That(json, Does.Contain("w1-l01"));
        }

        [Test]
        public void ExportSkillsToCsv_HasHeaderAndOneDataRow()
        {
            string csv = _exportService.ExportSkillsToCsv();
            string[] lines = csv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Does.Contain("SkillId"));
            Assert.That(lines[1], Does.Contain("Addition"));
        }

        [Test]
        public void ExportSessionsToCsv_HasHeaderAndOneDataRow()
        {
            string csv = _exportService.ExportSessionsToCsv();
            string[] lines = csv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Does.Contain("SessionId"));
            Assert.That(lines[1], Does.Contain("w1-l01"));
        }

        [Test]
        public void ExportSkillsCsvToFile_WritesReadableFile()
        {
            string path = System.IO.Path.Combine(_tempDir.Path, "skills-export.csv");

            _exportService.ExportSkillsCsvToFile(path);

            Assert.That(System.IO.File.Exists(path), Is.True);
            Assert.That(System.IO.File.ReadAllText(path), Does.Contain("Addition"));
        }
    }
}
