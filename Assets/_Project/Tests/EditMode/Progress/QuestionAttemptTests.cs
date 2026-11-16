using System;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Progress
{
    [TestFixture]
    public class QuestionAttemptTests
    {
        private static MathProblemId SomeQuestionId() =>
            MathProblemId.New(new DeterministicRandomSource(1));

        [Test]
        public void ValidValues_ConstructsSuccessfully()
        {
            var attempt = new QuestionAttempt(
                SomeQuestionId(),
                GameSessionId.New(),
                new[] { SkillTag.Multiplication },
                difficulty: 4,
                correctAnswer: "56",
                selectedAnswer: "56",
                wasCorrect: true,
                responseTimeSeconds: 2.5f,
                timeout: false,
                errorCategory: ErrorCategory.None,
                answeredAt: DateTimeOffset.UtcNow);

            Assert.That(attempt.WasCorrect, Is.True);
            Assert.That(attempt.SkillTags.Count, Is.EqualTo(1));
            Assert.That(attempt.SkillTags[0], Is.EqualTo(SkillTag.Multiplication));
        }

        [Test]
        public void NullSkillTags_DefaultsToEmpty()
        {
            var attempt = new QuestionAttempt(
                SomeQuestionId(), GameSessionId.New(), null, 1, "1", null, false,
                0f, true, ErrorCategory.Timeout, DateTimeOffset.UtcNow);

            Assert.That(attempt.SkillTags.Count, Is.EqualTo(0));
        }

        [Test]
        public void TimeoutWithWasCorrectTrue_Throws()
        {
            Assert.That(
                () => new QuestionAttempt(
                    SomeQuestionId(), GameSessionId.New(), null, 1, "1", null, true,
                    0f, true, ErrorCategory.Timeout, DateTimeOffset.UtcNow),
                Throws.ArgumentException);
        }

        [TestCase(0)]
        [TestCase(11)]
        public void OutOfRangeDifficulty_Throws(int difficulty)
        {
            Assert.That(
                () => new QuestionAttempt(
                    SomeQuestionId(), GameSessionId.New(), null, difficulty, "1", "1", true,
                    0f, false, ErrorCategory.None, DateTimeOffset.UtcNow),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void NegativeResponseTime_Throws()
        {
            Assert.That(
                () => new QuestionAttempt(
                    SomeQuestionId(), GameSessionId.New(), null, 1, "1", "1", true,
                    -0.1f, false, ErrorCategory.None, DateTimeOffset.UtcNow),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void MathProblemId_New_IsDeterministicForSameSeed()
        {
            var a = MathProblemId.New(new DeterministicRandomSource(42));
            var b = MathProblemId.New(new DeterministicRandomSource(42));

            Assert.That(a, Is.EqualTo(b));
        }
    }
}
