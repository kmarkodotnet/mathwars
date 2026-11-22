using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Problems
{
    // MathProblem (MathHorde.Core.Problems, contract core-domain.md 3.).
    //
    // By design this constructor only guards (a) required references and
    // (b) the single-field range/consistency comments that are NOT part of
    // the I1-I11 table (DifficultyLevel, ThinkTimeSeconds, TargetValue) — the
    // I1-I11 cross-field/collection business invariants are the
    // ProblemValidator's job (F2-08), see the type's own XML doc comment.

    [TestFixture]
    public class MathProblemTests
    {
        private static ExpressionNode SampleExpression()
        {
            return new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(7), new ConstantNode(8));
        }

        private static IReadOnlyList<AnswerOption> SampleOptions()
        {
            return new[]
            {
                new AnswerOption(0, new LaneIndex(0), new EvaluationResult(56), null, true, null, "56"),
                new AnswerOption(1, new LaneIndex(1), new EvaluationResult(63), null, false, ErrorCategory.NeighbourInTimesTable, "63"),
            };
        }

        private static MathProblem BuildValidProblem(
            ProblemType type = ProblemType.AnswerLaneSelection,
            int difficultyLevel = 3,
            float thinkTimeSeconds = 6f,
            int? targetValue = null,
            IReadOnlyList<SkillTag> skillTags = null,
            GateGoal goal = GateGoal.Maximize,
            int? gateBaseValue = null)
        {
            return new MathProblem(
                id: new MathProblemId(Guid.NewGuid()),
                type: type,
                expression: SampleExpression(),
                displayExpression: "7 × 8 = ?",
                correctAnswer: new EvaluationResult(56),
                options: SampleOptions(),
                difficultyLevel: difficultyLevel,
                skillTags: skillTags ?? new[] { SkillTag.Multiplication },
                thinkTimeSeconds: thinkTimeSeconds,
                reward: new RewardSpec(ResourceType.Health, 30),
                seed: 12345,
                goal: goal,
                targetValue: targetValue,
                explanation: null,
                gateBaseValue: gateBaseValue);
        }

        [Test]
        public void Constructor_ValidProblem_SetsAllProperties()
        {
            var problem = BuildValidProblem();

            Assert.That(problem.Type, Is.EqualTo(ProblemType.AnswerLaneSelection));
            Assert.That(problem.DisplayExpression, Is.EqualTo("7 × 8 = ?"));
            Assert.That(problem.CorrectAnswer, Is.EqualTo(new EvaluationResult(56)));
            Assert.That(problem.Options.Count, Is.EqualTo(2));
            Assert.That(problem.DifficultyLevel, Is.EqualTo(3));
            Assert.That(problem.SkillTags.Count, Is.EqualTo(1));
            Assert.That(problem.ThinkTimeSeconds, Is.EqualTo(6f));
            Assert.That(problem.Seed, Is.EqualTo(12345));
            Assert.That(problem.Goal, Is.EqualTo(GateGoal.Maximize));
            Assert.That(problem.TargetValue, Is.Null);
            Assert.That(problem.GateBaseValue, Is.Null);
            Assert.That(problem.Explanation, Is.Null);
        }

        [Test]
        public void Constructor_TargetValueSelectionWithTargetValue_IsAllowed()
        {
            var problem = BuildValidProblem(type: ProblemType.TargetValueSelection, targetValue: 60);

            Assert.That(problem.TargetValue, Is.EqualTo(60));
        }

        [Test]
        public void Constructor_TargetValueOnNonTargetValueSelectionType_Throws()
        {
            Assert.That(
                () => BuildValidProblem(type: ProblemType.AnswerLaneSelection, targetValue: 60),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_ReachTargetResourceGate_PreservesTargetAndCapacity()
        {
            var problem = BuildValidProblem(type: ProblemType.ResourceOperationComparison,
                targetValue: 60, goal: GateGoal.ReachTarget, gateBaseValue: 75);
            Assert.That(problem.TargetValue, Is.EqualTo(60));
            Assert.That(problem.GateBaseValue, Is.EqualTo(75));
        }

        [TestCase(GateGoal.Maximize)]
        [TestCase(GateGoal.Minimize)]
        public void Constructor_ResourceGateWithTargetForOtherGoal_Throws(GateGoal goal)
        {
            Assert.That(() => BuildValidProblem(type: ProblemType.ResourceOperationComparison,
                targetValue: 60, goal: goal), Throws.TypeOf<ArgumentException>());
        }

        [TestCase(null)]
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_InvalidGateContext_RemainsAValidatableCandidate(int? capacity)
        {
            var problem = BuildValidProblem(type: ProblemType.ResourceOperationComparison,
                goal: GateGoal.ReachTarget, gateBaseValue: capacity);
            Assert.That(problem.GateBaseValue, Is.EqualTo(capacity));
            Assert.That(problem.TargetValue, Is.Null);
        }

        [Test]
        public void Constructor_CapacityOnOtherType_IsDeferredToValidator()
        {
            Assert.That(BuildValidProblem(gateBaseValue: 100).GateBaseValue, Is.EqualTo(100));
        }

        [TestCase(0)]
        [TestCase(11)]
        public void Constructor_DifficultyLevelOutOfRange_Throws(int difficultyLevel)
        {
            Assert.That(
                () => BuildValidProblem(difficultyLevel: difficultyLevel),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(1)]
        [TestCase(10)]
        public void Constructor_DifficultyLevelAtBounds_Succeeds(int difficultyLevel)
        {
            var problem = BuildValidProblem(difficultyLevel: difficultyLevel);

            Assert.That(problem.DifficultyLevel, Is.EqualTo(difficultyLevel));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Constructor_ThinkTimeNotPositive_Throws(float thinkTimeSeconds)
        {
            Assert.That(
                () => BuildValidProblem(thinkTimeSeconds: thinkTimeSeconds),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_NullExpression_Throws()
        {
            Assert.That(
                () => new MathProblem(
                    new MathProblemId(Guid.NewGuid()), ProblemType.AnswerLaneSelection, null, "x", new EvaluationResult(1),
                    SampleOptions(), 1, new[] { SkillTag.Addition }, 1f, new RewardSpec(ResourceType.Health, 1), 1,
                    GateGoal.Maximize, null, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NullOptions_Throws()
        {
            Assert.That(
                () => new MathProblem(
                    new MathProblemId(Guid.NewGuid()), ProblemType.AnswerLaneSelection, SampleExpression(), "x", new EvaluationResult(1),
                    null, 1, new[] { SkillTag.Addition }, 1f, new RewardSpec(ResourceType.Health, 1), 1,
                    GateGoal.Maximize, null, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NullSkillTags_Throws()
        {
            Assert.That(
                () => new MathProblem(
                    new MathProblemId(Guid.NewGuid()), ProblemType.AnswerLaneSelection, SampleExpression(), "x", new EvaluationResult(1),
                    SampleOptions(), 1, null, 1f, new RewardSpec(ResourceType.Health, 1), 1,
                    GateGoal.Maximize, null, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NullReward_Throws()
        {
            Assert.That(
                () => new MathProblem(
                    new MathProblemId(Guid.NewGuid()), ProblemType.AnswerLaneSelection, SampleExpression(), "x", new EvaluationResult(1),
                    SampleOptions(), 1, new[] { SkillTag.Addition }, 1f, null, 1,
                    GateGoal.Maximize, null, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NullDisplayExpression_Throws()
        {
            Assert.That(
                () => new MathProblem(
                    new MathProblemId(Guid.NewGuid()), ProblemType.AnswerLaneSelection, SampleExpression(), null, new EvaluationResult(1),
                    SampleOptions(), 1, new[] { SkillTag.Addition }, 1f, new RewardSpec(ResourceType.Health, 1), 1,
                    GateGoal.Maximize, null, null),
                Throws.TypeOf<ArgumentNullException>());
        }
    }
}
