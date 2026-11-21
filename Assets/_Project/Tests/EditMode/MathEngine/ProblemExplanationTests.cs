using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    [TestFixture]
    public class ProblemExplanationTests
    {
        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void Explain_CorrectWrongOrTimedOutAnswer_ReturnsEmptyExplanation(bool correct, bool timedOut)
        {
            var problem = Problem();
            var answer = Answer(problem, correct, timedOut);
            IProblemExplanationService service = new NullExplanationService();

            var result = service.Explain(problem, answer);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.TemplateKey, Is.EqualTo(string.Empty));
            Assert.That(result.Parameters.Count, Is.EqualTo(0));
            Assert.That(result.Steps.Count, Is.EqualTo(0));
            Assert.That(problem.Explanation, Is.Null);
            Assert.That(problem.CorrectAnswer, Is.EqualTo(new EvaluationResult(56)));
            Assert.That(answer.WasCorrect, Is.EqualTo(correct));
            Assert.That(answer.TimedOut, Is.EqualTo(timedOut));
        }

        [Test]
        public void Explain_RepeatedCalls_ReturnSharedImmutableEmptyModel()
        {
            var problem = Problem();
            var service = new NullExplanationService();
            var first = service.Explain(problem, Answer(problem, true, false));
            var second = service.Explain(problem, Answer(problem, false, true));

            Assert.That(second, Is.SameAs(first));
            Assert.That(() => ((IDictionary<string, string>)first.Parameters).Add("a", "7"),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<ExplanationStep>)first.Steps).Add(Step()),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(second.Parameters.Count, Is.EqualTo(0));
            Assert.That(second.Steps.Count, Is.EqualTo(0));
        }

        [Test]
        public void Explain_NullProblemOrAnswer_RejectsProgrammerError()
        {
            var problem = Problem();
            var service = new NullExplanationService();
            Assert.That(() => service.Explain(null, Answer(problem, true, false)),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => service.Explain(problem, null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Model_LocalizationKeysParametersAndOrderedSteps_ArePreserved()
        {
            var steps = new[] { Step(), new ExplanationStep("28 × 2 = 56", "explain.step.double_it") };
            var parameters = new Dictionary<string, string> { { "a", "7" }, { "b", "8" } };
            var model = new ExplanationModel("explain.mul.double", parameters, steps);

            Assert.That(model.TemplateKey, Is.EqualTo("explain.mul.double"));
            Assert.That(model.Parameters.Count, Is.EqualTo(2));
            Assert.That(model.Parameters["a"], Is.EqualTo("7"));
            Assert.That(model.Parameters["b"], Is.EqualTo("8"));
            Assert.That(model.Steps.Count, Is.EqualTo(2));
            Assert.That(model.Steps[0].Expression, Is.EqualTo("7 × 4 = 28"));
            Assert.That(model.Steps[0].NoteKey, Is.EqualTo("explain.step.half"));
            Assert.That(model.Steps[1].Expression, Is.EqualTo("28 × 2 = 56"));
            Assert.That(model.Steps[1].NoteKey, Is.EqualTo("explain.step.double_it"));
        }

        [Test]
        public void Model_CallerCollectionMutation_CannotChangePublishedData()
        {
            var originalStep = Step();
            var steps = new List<ExplanationStep> { originalStep };
            var parameters = new Dictionary<string, string> { { "a", "7" } };
            var model = new ExplanationModel("explain.mul.double", parameters, steps);

            parameters["a"] = "99";
            parameters.Add("b", "8");
            steps.Clear();

            Assert.That(model.Parameters.Count, Is.EqualTo(1));
            Assert.That(model.Parameters["a"], Is.EqualTo("7"));
            Assert.That(model.Steps.Count, Is.EqualTo(1));
            Assert.That(model.Steps[0], Is.SameAs(originalStep));
            Assert.That(() => ((IDictionary<string, string>)model.Parameters)["a"] = "99",
                Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<ExplanationStep>)model.Steps)[0] = Step(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void Model_NullRequiredData_IsRejected()
        {
            var parameters = new Dictionary<string, string>();
            var steps = Array.Empty<ExplanationStep>();
            Assert.That(() => new ExplanationModel(null, parameters, steps), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new ExplanationModel(string.Empty, null, steps), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new ExplanationModel(string.Empty, parameters, null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new ExplanationModel(string.Empty, parameters, new ExplanationStep[] { null }),
                Throws.TypeOf<ArgumentException>());
            parameters.Add("a", null);
            Assert.That(() => new ExplanationModel(string.Empty, parameters, steps), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Step_NullExpressionOrNoteKey_IsRejected()
        {
            Assert.That(() => new ExplanationStep(null, "explain.step.half"), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new ExplanationStep("7 × 4 = 28", null), Throws.TypeOf<ArgumentNullException>());
        }

        private static ExplanationStep Step() => new ExplanationStep("7 × 4 = 28", "explain.step.half");

        private static MathProblem Problem()
        {
            return new MathProblem(
                new MathProblemId(new Guid("13131313-1313-1313-1313-131313131313")),
                ProblemType.AnswerLaneSelection,
                new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(7), new ConstantNode(8)),
                "7 × 8 = ?", new EvaluationResult(56),
                new[]
                {
                    new AnswerOption(0, new LaneIndex(0), new EvaluationResult(56), null, true, null, "56"),
                    new AnswerOption(1, new LaneIndex(1), new EvaluationResult(63), null, false,
                        ErrorCategory.NeighbourInTimesTable, "63")
                },
                3, new[] { SkillTag.Multiplication, SkillTag.TimesTable7 }, 6f,
                new RewardSpec(ResourceType.Health, 30, false), 213, GateGoal.Maximize, null, null);
        }

        private static MathAnswer Answer(MathProblem problem, bool correct, bool timedOut)
        {
            return new MathAnswer(problem.Id, timedOut ? (int?)null : correct ? 0 : 1,
                timedOut ? (LaneIndex?)null : new LaneIndex(correct ? 0 : 1), correct, 2f, timedOut,
                timedOut ? ErrorCategory.Timeout : correct ? ErrorCategory.None : ErrorCategory.NeighbourInTimesTable,
                10d);
        }
    }
}
