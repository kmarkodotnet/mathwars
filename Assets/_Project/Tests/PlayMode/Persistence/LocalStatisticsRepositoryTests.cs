// F6-02/ADR-011 — ILocalStatisticsRepository: append + round-trip for
// GameSessionRecord (with its QuestionAttempt log), corrupt-file handling.
using System;
using System.IO;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Infrastructure.Persistence;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MathHorde.Tests.PlayMode.Persistence
{
    [TestFixture]
    public class LocalStatisticsRepositoryTests
    {
        private TempSaveDirectory _tempDir;

        [SetUp]
        public void SetUp() => _tempDir = new TempSaveDirectory();

        [TearDown]
        public void TearDown() => _tempDir.Dispose();

        private static GameSessionRecord BuildSampleSession()
        {
            var sessionId = GameSessionId.New();
            var attempt = new QuestionAttempt(
                MathProblemId.New(new DeterministicRandomSource(7)),
                sessionId,
                new[] { SkillTag.Addition },
                difficulty: 2,
                correctAnswer: "65",
                selectedAnswer: "65",
                wasCorrect: true,
                responseTimeSeconds: 3.2f,
                timeout: false,
                errorCategory: ErrorCategory.None,
                answeredAt: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

            return new GameSessionRecord(
                sessionId,
                new LevelId("w1-l01"),
                seed: 123,
                startedAt: new DateTimeOffset(2026, 1, 1, 11, 59, 0, TimeSpan.Zero),
                finishedAt: new DateTimeOffset(2026, 1, 1, 12, 5, 0, TimeSpan.Zero),
                initialChallengeLevel: 3,
                finalChallengeLevel: 4,
                result: SessionResult.Victory,
                failureReason: FailureReason.None,
                lossDiagnosis: LossDiagnosis.None,
                mathAccuracy: 1.0f,
                totalQuestions: 1,
                correctQuestions: 1,
                timedOutQuestions: 0,
                appliedRewardEventIds: new[] { Guid.NewGuid() },
                attempts: new[] { attempt });
        }

        [Test]
        public void RoundTrip_AppendThenLoadAll_PreservesSessionFields()
        {
            var repository = new LocalStatisticsRepository(_tempDir.Path);
            GameSessionRecord original = BuildSampleSession();

            repository.Append(original);
            var loaded = repository.LoadAll();

            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].SessionId, Is.EqualTo(original.SessionId));
            Assert.That(loaded[0].LevelId.Value, Is.EqualTo("w1-l01"));
            Assert.That(loaded[0].Result, Is.EqualTo(SessionResult.Victory));
            Assert.That(loaded[0].MathAccuracy, Is.EqualTo(1.0f));
        }

        [Test]
        public void RoundTrip_AppendThenLoadAll_PreservesQuestionAttempts()
        {
            var repository = new LocalStatisticsRepository(_tempDir.Path);
            GameSessionRecord original = BuildSampleSession();

            repository.Append(original);
            var loaded = repository.LoadAll();

            Assert.That(loaded[0].Attempts.Count, Is.EqualTo(1));
            QuestionAttempt attempt = loaded[0].Attempts[0];
            Assert.That(attempt.WasCorrect, Is.True);
            Assert.That(attempt.SkillTags.Count, Is.EqualTo(1));
            Assert.That(attempt.SkillTags[0], Is.EqualTo(SkillTag.Addition));
            Assert.That(attempt.CorrectAnswer, Is.EqualTo("65"));
        }

        [Test]
        public void Append_TwoSessions_BothSurviveInOrder()
        {
            var repository = new LocalStatisticsRepository(_tempDir.Path);
            GameSessionRecord first = BuildSampleSession();
            GameSessionRecord second = BuildSampleSession();

            repository.Append(first);
            repository.Append(second);

            var loaded = repository.LoadAll();
            Assert.That(loaded.Count, Is.EqualTo(2));
        }

        [Test]
        public void LoadAll_NoFileYet_ReturnsEmpty()
        {
            var repository = new LocalStatisticsRepository(_tempDir.Path);

            Assert.That(repository.LoadAll().Count, Is.EqualTo(0));
        }

        [Test]
        public void LoadAll_CorruptFile_ReturnsEmptyAndLogsInsteadOfThrowing()
        {
            string path = Path.Combine(_tempDir.Path, LocalStatisticsRepository.DefaultFileName);
            File.WriteAllText(path, "not json at all {{{");
            var repository = new LocalStatisticsRepository(_tempDir.Path);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Sérült statisztika-fájl"));

            System.Collections.Generic.IReadOnlyList<GameSessionRecord> loaded = null;
            Assert.That(() => loaded = repository.LoadAll(), Throws.Nothing);
            Assert.That(loaded.Count, Is.EqualTo(0));
        }

        [Test]
        public void Delete_RemovesFile()
        {
            var repository = new LocalStatisticsRepository(_tempDir.Path);
            repository.Append(BuildSampleSession());

            repository.Delete();

            string path = Path.Combine(_tempDir.Path, LocalStatisticsRepository.DefaultFileName);
            Assert.That(File.Exists(path), Is.False);
        }
    }
}
