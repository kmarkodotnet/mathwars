// F6-01/ADR-011 — LocalSaveRepository: round-trip, atomic write, corrupt-file
// handling. Lives under Tests/PlayMode (not EditMode) because Infrastructure
// references UnityEngine (unity-csharp skill F1-09 pitfall note) — the
// EditMode asmdef does not even reference MathHorde.Infrastructure.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Skills;
using MathHorde.Infrastructure.Persistence;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MathHorde.Tests.PlayMode.Persistence
{
    [TestFixture]
    public class LocalSaveRepositoryTests
    {
        private TempSaveDirectory _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = new TempSaveDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            _tempDir.Dispose();
        }

        private static LocalProfileSnapshot BuildSampleSnapshot()
        {
            var profile = new PlayerProfile(
                PlayerProfileId.New(),
                "Kincső",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                currentChallengeLevel: 4,
                highestChallengeLevel: 6,
                unlockedWorlds: new[] { new WorldId("w1"), new WorldId("w2") },
                completedLevels: new[] { new LevelId("w1-l01"), new LevelId("w1-l02") });

            var skills = new Dictionary<SkillTag, SkillStatistics>
            {
                [SkillTag.Multiplication] = new SkillStatistics(
                    SkillTag.Multiplication, 3, 20, 15, 2.1f, 1.9f,
                    new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                    0.6f, 0.5f,
                    new Dictionary<ErrorCategory, int> { [ErrorCategory.NeighbourInTimesTable] = 3 })
            };

            var history = new List<DifficultyAdjustment>
            {
                new DifficultyAdjustment(3, 4, "LEVEL_PASSED_FIRST_TRY", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                    new Dictionary<string, double> { ["starRatio"] = 1.0 })
            };

            return new LocalProfileSnapshot(profile, skills, history);
        }

        [Test]
        public void RoundTrip_SaveThenLoad_PreservesProfileFields()
        {
            var repository = new LocalSaveRepository(_tempDir.Path);
            LocalProfileSnapshot original = BuildSampleSnapshot();

            repository.Save(original);
            LocalProfileSnapshot loaded = repository.Load();

            Assert.That(loaded.Profile.Id, Is.EqualTo(original.Profile.Id));
            Assert.That(loaded.Profile.DisplayName, Is.EqualTo("Kincső"));
            Assert.That(loaded.Profile.CurrentChallengeLevel, Is.EqualTo(4));
            Assert.That(loaded.Profile.HighestChallengeLevel, Is.EqualTo(6));
            Assert.That(loaded.Profile.UnlockedWorlds.Count, Is.EqualTo(2));
            Assert.That(loaded.Profile.CompletedLevels.Count, Is.EqualTo(2));
        }

        [Test]
        public void RoundTrip_SaveThenLoad_PreservesSkillStatistics()
        {
            var repository = new LocalSaveRepository(_tempDir.Path);
            LocalProfileSnapshot original = BuildSampleSnapshot();

            repository.Save(original);
            LocalProfileSnapshot loaded = repository.Load();

            Assert.That(loaded.Skills.Count, Is.EqualTo(1));
            SkillStatistics loadedSkill = loaded.Skills[SkillTag.Multiplication];
            Assert.That(loadedSkill.Attempts, Is.EqualTo(20));
            Assert.That(loadedSkill.CorrectAnswers, Is.EqualTo(15));
            Assert.That(loadedSkill.TypicalErrors[ErrorCategory.NeighbourInTimesTable], Is.EqualTo(3));
        }

        [Test]
        public void RoundTrip_SaveThenLoad_PreservesDifficultyHistory()
        {
            var repository = new LocalSaveRepository(_tempDir.Path);
            LocalProfileSnapshot original = BuildSampleSnapshot();

            repository.Save(original);
            LocalProfileSnapshot loaded = repository.Load();

            Assert.That(loaded.DifficultyHistory.Count, Is.EqualTo(1));
            Assert.That(loaded.DifficultyHistory[0].Reason, Is.EqualTo("LEVEL_PASSED_FIRST_TRY"));
            Assert.That(loaded.DifficultyHistory[0].TriggerMetrics["starRatio"], Is.EqualTo(1.0));
        }

        [Test]
        public void Load_NoSaveFileYet_ReturnsFreshDefaultProfile()
        {
            var repository = new LocalSaveRepository(_tempDir.Path);

            LocalProfileSnapshot snapshot = repository.Load();

            Assert.That(snapshot.Profile.CurrentChallengeLevel, Is.EqualTo(1));
            Assert.That(snapshot.Skills.Count, Is.EqualTo(0));
            Assert.That(snapshot.DifficultyHistory.Count, Is.EqualTo(0));
        }

        [Test]
        public void Load_CorruptFile_ReturnsFreshProfileAndLogsInsteadOfThrowing()
        {
            string path = System.IO.Path.Combine(_tempDir.Path, LocalSaveRepository.DefaultFileName);
            File.WriteAllText(path, "{ this is not valid json ][");
            var repository = new LocalSaveRepository(_tempDir.Path);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Sérült mentésfájl"));

            LocalProfileSnapshot snapshot = null;
            Assert.That(() => snapshot = repository.Load(), Throws.Nothing);
            Assert.That(snapshot.Profile.CurrentChallengeLevel, Is.EqualTo(1));
        }

        [Test]
        public void Load_EmptyFile_ReturnsFreshProfileAndLogsInsteadOfThrowing()
        {
            string path = System.IO.Path.Combine(_tempDir.Path, LocalSaveRepository.DefaultFileName);
            File.WriteAllText(path, string.Empty);
            var repository = new LocalSaveRepository(_tempDir.Path);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Sérült mentésfájl"));

            LocalProfileSnapshot snapshot = null;
            Assert.That(() => snapshot = repository.Load(), Throws.Nothing);
            Assert.That(snapshot.Profile.CurrentChallengeLevel, Is.EqualTo(1));
        }

        [Test]
        public void Save_NeverLeavesTempFilesBehind()
        {
            var repository = new LocalSaveRepository(_tempDir.Path);
            repository.Save(BuildSampleSnapshot());

            string[] tempFiles = Directory.GetFiles(_tempDir.Path, "*.tmp-*");
            Assert.That(tempFiles.Length, Is.EqualTo(0));
        }

        [Test]
        public void RepeatedSaves_SimulatingConcurrentAutosave_LastWriteWins()
        {
            var repository = new LocalSaveRepository(_tempDir.Path);
            LocalProfileSnapshot first = BuildSampleSnapshot();
            repository.Save(first);

            var secondProfile = new PlayerProfile(
                first.Profile.Id, first.Profile.DisplayName, first.Profile.CreatedAt,
                currentChallengeLevel: 7, highestChallengeLevel: 7,
                unlockedWorlds: first.Profile.UnlockedWorlds, completedLevels: first.Profile.CompletedLevels);
            var second = new LocalProfileSnapshot(secondProfile, first.Skills, first.DifficultyHistory);

            // Two "autosave" calls landing back-to-back — each one is a
            // single atomic write, so whichever one runs last fully wins;
            // there is no interleaved/partial state in between.
            repository.Save(second);

            LocalProfileSnapshot loaded = repository.Load();
            Assert.That(loaded.Profile.CurrentChallengeLevel, Is.EqualTo(7));
        }

        [Test]
        public void Delete_RemovesSaveFile_AndSubsequentLoadIsFresh()
        {
            var repository = new LocalSaveRepository(_tempDir.Path);
            repository.Save(BuildSampleSnapshot());

            repository.Delete();

            string path = System.IO.Path.Combine(_tempDir.Path, LocalSaveRepository.DefaultFileName);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(repository.Load().Profile.CurrentChallengeLevel, Is.EqualTo(1));
        }

        [Test]
        public void Delete_NoFileYet_DoesNotThrow()
        {
            var repository = new LocalSaveRepository(_tempDir.Path);

            Assert.That(() => repository.Delete(), Throws.Nothing);
        }
    }
}
