// F6-09/ADR-011 6. — ProfileDataEraser: single, irreversible,
// confirmation-gated deletion of ALL files belonging to the profile.
using System;
using System.IO;
using MathHorde.Core.Progress;
using MathHorde.Infrastructure.Persistence;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.Persistence
{
    [TestFixture]
    public class ProfileDataEraserTests
    {
        private TempSaveDirectory _tempDir;
        private LocalSaveRepository _saveRepository;
        private LocalStatisticsRepository _statisticsRepository;
        private ProfileDataEraser _eraser;

        [SetUp]
        public void SetUp()
        {
            _tempDir = new TempSaveDirectory();
            _saveRepository = new LocalSaveRepository(_tempDir.Path);
            _statisticsRepository = new LocalStatisticsRepository(_tempDir.Path);
            _eraser = new ProfileDataEraser(_saveRepository, _statisticsRepository);

            _saveRepository.Save(_saveRepository.Load());
            _statisticsRepository.Append(new GameSessionRecord(
                GameSessionId.New(), new LevelId("w1-l01"), 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                1, 1, SessionResult.Victory, FailureReason.None, LossDiagnosis.None,
                1f, 1, 1, 0, Array.Empty<Guid>(), Array.Empty<QuestionAttempt>()));
        }

        [TearDown]
        public void TearDown() => _tempDir.Dispose();

        [Test]
        public void Erase_WithoutConfirmation_ThrowsAndDeletesNothing()
        {
            Assert.That(() => _eraser.Erase(confirmed: false), Throws.TypeOf<InvalidOperationException>());

            string profilePath = Path.Combine(_tempDir.Path, LocalSaveRepository.DefaultFileName);
            string statsPath = Path.Combine(_tempDir.Path, LocalStatisticsRepository.DefaultFileName);
            Assert.That(File.Exists(profilePath), Is.True);
            Assert.That(File.Exists(statsPath), Is.True);
        }

        [Test]
        public void Erase_Confirmed_DeletesBothFiles()
        {
            _eraser.Erase(confirmed: true);

            string profilePath = Path.Combine(_tempDir.Path, LocalSaveRepository.DefaultFileName);
            string statsPath = Path.Combine(_tempDir.Path, LocalStatisticsRepository.DefaultFileName);
            Assert.That(File.Exists(profilePath), Is.False);
            Assert.That(File.Exists(statsPath), Is.False);
        }

        [Test]
        public void Erase_Confirmed_SubsequentLoadStartsFresh()
        {
            _eraser.Erase(confirmed: true);

            Assert.That(_saveRepository.Load().Profile.CurrentChallengeLevel, Is.EqualTo(1));
            Assert.That(_statisticsRepository.LoadAll().Count, Is.EqualTo(0));
        }
    }
}
