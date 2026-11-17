using System;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Problems
{
    // MathAnswer (MathHorde.Core.Problems, contract core-domain.md 3.).
    // Mirrors the Core.Progress.QuestionAttempt timeout/correctness guard.

    [TestFixture]
    public class MathAnswerTests
    {
        private static MathProblemId SampleProblemId()
        {
            return new MathProblemId(Guid.NewGuid());
        }

        [Test]
        public void Constructor_CorrectAnswer_SetsAllProperties()
        {
            var problemId = SampleProblemId();

            var answer = new MathAnswer(
                problemId,
                selectedOptionIndex: 2,
                selectedLane: new LaneIndex(2),
                wasCorrect: true,
                responseTimeSeconds: 1.5f,
                timedOut: false,
                errorCategory: ErrorCategory.None,
                answeredAtGameTime: 12.3);

            Assert.That(answer.ProblemId, Is.EqualTo(problemId));
            Assert.That(answer.SelectedOptionIndex, Is.EqualTo(2));
            Assert.That(answer.SelectedLane, Is.EqualTo(new LaneIndex(2)));
            Assert.That(answer.WasCorrect, Is.True);
            Assert.That(answer.TimedOut, Is.False);
            Assert.That(answer.ErrorCategory, Is.EqualTo(ErrorCategory.None));
        }

        [Test]
        public void Constructor_Timeout_AllowsNullSelectedOptionIndex()
        {
            var answer = new MathAnswer(
                SampleProblemId(),
                selectedOptionIndex: null,
                selectedLane: null,
                wasCorrect: false,
                responseTimeSeconds: 5f,
                timedOut: true,
                errorCategory: ErrorCategory.Timeout,
                answeredAtGameTime: 20.0);

            Assert.That(answer.SelectedOptionIndex, Is.Null);
            Assert.That(answer.TimedOut, Is.True);
            Assert.That(answer.ErrorCategory, Is.EqualTo(ErrorCategory.Timeout));
        }

        [Test]
        public void Constructor_TimeoutAndCorrect_Throws()
        {
            Assert.That(
                () => new MathAnswer(SampleProblemId(), null, null, true, 1f, true, ErrorCategory.Timeout, 1.0),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_NullSelectedOptionIndexWithoutTimeout_Throws()
        {
            Assert.That(
                () => new MathAnswer(SampleProblemId(), null, null, false, 1f, false, ErrorCategory.Unclassified, 1.0),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_CorrectWithNonNoneErrorCategory_Throws()
        {
            Assert.That(
                () => new MathAnswer(SampleProblemId(), 0, null, true, 1f, false, ErrorCategory.CarryError, 1.0),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_TimeoutWithNonTimeoutErrorCategory_Throws()
        {
            Assert.That(
                () => new MathAnswer(SampleProblemId(), null, null, false, 1f, true, ErrorCategory.None, 1.0),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_NegativeResponseTime_Throws()
        {
            Assert.That(
                () => new MathAnswer(SampleProblemId(), 0, null, true, -0.1f, false, ErrorCategory.None, 1.0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
