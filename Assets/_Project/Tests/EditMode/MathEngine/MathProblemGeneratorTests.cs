using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.Core.Skills;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    [TestFixture]
    public class MathProblemGeneratorTests
    {
        [TestCase(ProblemType.AnswerLaneSelection)]
        [TestCase(ProblemType.MissingOperand)]
        [TestCase(ProblemType.EquationSolving)]
        [TestCase(ProblemType.TargetValueSelection)]
        [TestCase(ProblemType.ResourceOperationComparison)]
        public void Generate_AllTypes_PreserveMetadataAndPassRealValidator(ProblemType type)
        {
            var request = Request(type: type);
            var result = new MathProblemGenerator().Generate(request);
            Assert.That(result.IsSuccess, Is.True, result.ErrorCode);
            var p = result.Value;
            Assert.That(new ProblemValidator().IsValid(p, request.Difficulty), Is.True);
            Assert.That(p.Type, Is.EqualTo(type));
            Assert.That(p.Seed, Is.EqualTo(request.Seed));
            Assert.That(p.Id.Value, Is.Not.EqualTo(Guid.Empty));
            Assert.That(p.Reward, Is.SameAs(request.Reward));
            Assert.That(p.ThinkTimeSeconds, Is.EqualTo(request.ThinkTimeSeconds));
            Assert.That(p.DifficultyLevel, Is.EqualTo(request.Difficulty.ChallengeLevel));
            Assert.That(p.GateBaseValue.HasValue, Is.EqualTo(type == ProblemType.ResourceOperationComparison));
            Assert.That(p.TargetValue, Is.EqualTo(request.TargetValue));
            if (type == ProblemType.AnswerLaneSelection) Assert.That(p.DisplayExpression, Does.EndWith(" = ?"));
            if (type == ProblemType.TargetValueSelection) Assert.That(p.CorrectAnswer.Value, Is.EqualTo(request.TargetValue.Value));
        }

        [TestCase(GateGoal.Maximize)]
        [TestCase(GateGoal.Minimize)]
        [TestCase(GateGoal.ReachTarget)]
        public void Generate_Gates_PreserveRawValuesSnapshotAndGoal(GateGoal goal)
        {
            var request = Request(type: ProblemType.ResourceOperationComparison, goal: goal, capacity: 50);
            var result = new MathProblemGenerator().Generate(request);
            Assert.That(result.IsSuccess, Is.True, result.ErrorCode);
            var evaluator = new ExpressionEvaluator();
            foreach (var option in result.Value.Options)
            {
                var binary = option.GateOperation as BinaryOperationNode;
                Assert.That(binary, Is.Not.Null);
                Assert.That(binary.Left is ConstantNode, Is.True);
                Assert.That(binary.Right is ConstantNode, Is.True);
                Assert.That(evaluator.EvaluateWith(binary, result.Value.GateBaseValue.Value, EvaluationOptions.Default).Value,
                    Is.EqualTo(option.Value));
                Assert.That(option.DisplayLabel, Does.Not.Contain("?"));
            }

            Assert.That(result.Value.Goal, Is.EqualTo(goal));

        }

        [TestCase(BinaryOperator.Addition)]
        [TestCase(BinaryOperator.Subtraction)]
        [TestCase(BinaryOperator.Multiplication)]
        [TestCase(BinaryOperator.Division)]
        [TestCase(BinaryOperator.Modulo)]
        [TestCase(BinaryOperator.IntegerDivisionWithRemainder)]
        public void Generate_RequiredArithmeticAndRemainders_ArePreserved(BinaryOperator op)
        {
            var generator = new MathProblemGenerator();
            for (int seed = 0; seed < 32; seed++)
            {
                var profile = ProblemValidatorTests.Profile(count: 2 + seed % 3, depth: 0, maxOperand: 30, operators: new[] { op });
                var result = generator.Generate(Request(seed: seed, profile: profile));
                Assert.That(result.IsSuccess, Is.True, op + " seed " + seed + ": " + result.ErrorCode);
                Assert.That(new ProblemValidator().IsValid(result.Value, profile), Is.True);
                Assert.That(result.Value.CorrectAnswer.Remainder.HasValue, Is.EqualTo(op == BinaryOperator.IntegerDivisionWithRemainder));
                Assert.That(result.Value.Expression.ToDisplayString(), Does.Not.Contain("("));
            }
        }







        [Test]
        public void Generate_NegativeResultsRequireAndRespectProfilePermission()
        {
            var generator = new MathProblemGenerator();
            var profile = ProblemValidatorTests.Profile(allowNegative: true, operators: new[] { BinaryOperator.Subtraction });
            bool negative = false;
            for (int seed = 0; seed < 100; seed++)
            {
                var result = generator.Generate(Request(seed: seed, profile: profile));
                Assert.That(result.IsSuccess, Is.True, result.ErrorCode);
                Assert.That(new ProblemValidator().IsValid(result.Value, profile), Is.True);
                negative |= result.Value.CorrectAnswer.Value < 0;
            }
            Assert.That(negative, Is.True);
        }

        [Test]
        public void Generate_IntMaxOperandBoundary_FailsWithoutLeakingSamplerOverflow()
        {
            var profile = ProblemValidatorTests.Profile(minOperand: int.MaxValue - 1, maxOperand: int.MaxValue,
                operators: new[] { BinaryOperator.Addition });
            Assert.That(new MathProblemGenerator().Generate(Request(profile: profile)).ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
        }



        [Test]
        public void Generate_ReachTargetWithoutTarget_FailsBeforeAnyCandidate()
        {
            var validator = new RecordingValidator();
            var request = Request(type: ProblemType.ResourceOperationComparison, goal: GateGoal.ReachTarget, omitTarget: true);
            Assert.That(Generator(new RecordingExpressionGenerator(), validator).Generate(request).ErrorCode, Is.EqualTo("GATE_TARGET_MISSING"));
            Assert.That(validator.Candidates.Count, Is.EqualTo(0));
        }



        [Test]
        public void Generate_NullInputsAndInvalidRequest_AreExplicit()
        {
            var generator = new MathProblemGenerator();
            Assert.That(() => generator.Generate(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => generator.Generate(new ProblemGenerationRequest()), Throws.TypeOf<ArgumentNullException>());
            foreach (float time in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.That(generator.Generate(Request(time: time)).ErrorCode, Is.EqualTo("INVALID_REQUEST"));
            Assert.That(generator.Generate(Request(lanes: 2, count: 3)).ErrorCode, Is.EqualTo("INVALID_REQUEST"));
            Assert.That(generator.Generate(Request(lanes: 5)).ErrorCode, Is.EqualTo("INVALID_REQUEST"));
            Assert.That(generator.Generate(Request(type: (ProblemType)99)).ErrorCode, Is.EqualTo("INVALID_REQUEST"));
            Assert.That(generator.Generate(Request(type: ProblemType.TargetValueSelection, omitTarget: true)).ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        }

        [Test]
        public void Generate_RetriesSixteenTimesThenNarrowsWithinOriginalProfile()
        {
            var expressions = new RecordingExpressionGenerator();
            var validator = new RecordingValidator(reject: 16);
            var request = Request();
            var result = Generator(expressions, validator).Generate(request);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(expressions.Requests.Count, Is.EqualTo(17));
            for (int i = 0; i < 16; i++)
            {
                Assert.That(expressions.Requests[i].MaxOperandValue, Is.EqualTo(30));
                Assert.That(expressions.Requests[i].MaxOperandCount, Is.EqualTo(4));
            }
            Assert.That(expressions.Requests[16].MaxOperandValue, Is.EqualTo(15));
            Assert.That(expressions.Requests[16].MaxOperandCount, Is.EqualTo(3));
            Assert.That(expressions.Requests[16].MinOperandCount, Is.EqualTo(request.Difficulty.MinOperandCount));
            Assert.That(expressions.Requests[16].MaxResultValue, Is.EqualTo(request.Difficulty.MaxResultValue));
            Assert.That(validator.Candidates.Select(p => p.Id).Distinct().Count(), Is.EqualTo(1));
            Assert.That(validator.Candidates.All(p => p.Seed == request.Seed), Is.True);
        }



        [Test]
        public void Generate_ExhaustionIsBoundedAndNeverReturnsRejectedCandidate()
        {
            var validator = new RecordingValidator(reject: int.MaxValue);
            var expressions = new RecordingExpressionGenerator();
            var result = Generator(expressions, validator).Generate(Request());
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
            Assert.That(validator.Candidates.Count, Is.EqualTo(96));
            Assert.That(expressions.Requests.Last().MaxOperandValue, Is.EqualTo(1));
            Assert.That(expressions.Requests.Last().MaxOperandCount, Is.EqualTo(2));
        }

        [Test]
        public void Generate_ExpressionFailure_IsRetriedAndBounded()
        {
            var expressions = new RecordingExpressionGenerator(fail: true);
            var validator = new RecordingValidator();
            Assert.That(Generator(expressions, validator).Generate(Request()).ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
            Assert.That(expressions.Requests.Count, Is.EqualTo(96));
            Assert.That(validator.Candidates.Count, Is.EqualTo(0));
        }

        [Test]
        public void Generate_LockedSkillsCannotBeUnlockedByHighChallengeLevel()
        {
            var profile = ProblemValidatorTests.Profile(operators: new[] { BinaryOperator.Multiplication }, skills: new[] { SkillTag.Addition });
            Assert.That(new MathProblemGenerator().Generate(Request(profile: profile)).ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
            var skills = new SkillProfile(null, DateTimeOffset.MinValue);
            var result = new MathProblemGenerator().Generate(Request(profile: profile, skills: skills));
            Assert.That(result.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
        }

        [Test]
        public void Generate_ProfilePrerequisitesAreRespected()
        {
            var skills = new SkillProfile(null, DateTimeOffset.MinValue);
            var request = Request(skills: skills);
            var result = new MathProblemGenerator().Generate(request);
            Assert.That(result.IsSuccess, Is.True, result.ErrorCode);
            Assert.That(result.Value.SkillTags.All(t => new SkillClassifier().IsEligible(t, skills)), Is.True);
        }

        [Test]
        public void GenerateBatch_StablePrefixDistinctIdsAndReplayableSeeds()
        {
            var generator = new MathProblemGenerator();
            var request = Request(type: ProblemType.ResourceOperationComparison, goal: GateGoal.ReachTarget, capacity: 53);
            var shortBatch = generator.GenerateBatch(request, 8);
            var longBatch = generator.GenerateBatch(request, 32);
            Assert.That(Signature(shortBatch[0].Value), Is.EqualTo(Signature(generator.Generate(request).Value)));
            for (int i = 0; i < longBatch.Count; i++)
            {
                Assert.That(longBatch[i].IsSuccess, Is.True, "item " + i);
                var p = longBatch[i].Value;
                var replay = generator.Generate(Request(seed: p.Seed, type: request.Type, goal: request.Goal, capacity: 53));
                Assert.That(Signature(p), Is.EqualTo(Signature(replay.Value)));
                if (i < shortBatch.Count) Assert.That(Signature(p), Is.EqualTo(Signature(shortBatch[i].Value)));
                Assert.That(p.GateBaseValue, Is.InRange(1, 30));
                Assert.That(p.TargetValue, Is.EqualTo(25));
            }
            Assert.That(longBatch.Select(r => r.Value.Id).Distinct().Count(), Is.EqualTo(32));
            Assert.That(generator.GenerateBatch(request, 0).Count, Is.EqualTo(0));
            Assert.That(() => generator.GenerateBatch(request, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => generator.GenerateBatch(null, 0), Throws.TypeOf<ArgumentNullException>());
        }



        [Test]
        public void Generate_ConcurrentPrefetchIsDeterministic()
        {
            var generator = new MathProblemGenerator();
            var signatures = new string[32];
            Parallel.For(0, signatures.Length, i => signatures[i] = Signature(generator.Generate(Request(seed: i)).Value));
            for (int i = 0; i < signatures.Length; i++) Assert.That(signatures[i], Is.EqualTo(Signature(generator.Generate(Request(seed: i)).Value)));
        }

        [Test]
        public void Generate_DistractorDrawCountCannotShiftExpressionOrCorrectLane()
        {
            var evaluator = new ExpressionEvaluator();
            var classifier = new SkillClassifier();
            var changedDistractors = new MathProblemGenerator(new ExpressionGenerator(evaluator, new EquationSolver(evaluator)),
                evaluator, new EquationSolver(evaluator), new ExtraDrawDistractorGenerator(), classifier, new ProblemValidator());
            var normal = new MathProblemGenerator();
            for (int seed = 0; seed < 100; seed++)
            {
                var request = Request(seed: seed);
                var expected = normal.Generate(request).Value;
                var actual = changedDistractors.Generate(request).Value;
                Assert.That(actual.Expression.ToDisplayString(), Is.EqualTo(expected.Expression.ToDisplayString()));
                Assert.That(actual.CorrectAnswer, Is.EqualTo(expected.CorrectAnswer));
                Assert.That(actual.Options.Single(o => o.IsCorrect).Lane, Is.EqualTo(expected.Options.Single(o => o.IsCorrect).Lane));
                Assert.That(actual.Id, Is.EqualTo(expected.Id));
            }
        }

        internal static ProblemGenerationRequest Request(int seed = 209013, ProblemType type = ProblemType.AnswerLaneSelection,
            int count = 3, int lanes = 4, GateGoal goal = GateGoal.Maximize, int? capacity = 1000,
            bool omitTarget = false, float time = 7f, DifficultyProfile profile = null, SkillProfile skills = null, int snapshot = 20)
        {
            bool gate = type == ProblemType.ResourceOperationComparison;
            bool unknown = type == ProblemType.MissingOperand || type == ProblemType.EquationSolving;
            var resource = ResourceType.Health;
            return new ProblemGenerationRequest
            {
                Seed = seed, Type = type,
                Difficulty = profile ?? new DifficultyProfile(4, 4, 4, 4,
                    new[] { BinaryOperator.Addition, BinaryOperator.Subtraction, BinaryOperator.Multiplication, BinaryOperator.Division },
                    (SkillTag[])Enum.GetValues(typeof(SkillTag)), 0, 30, 1000, 2, 4, true, 2, unknown, unknown ? 1 : 0, false, count, 6f, 4, true),
                LaneCount = lanes, ThinkTimeSeconds = time, RewardResource = resource,
                Reward = new RewardSpec(resource, 5), Goal = goal,
                GateLimits = gate ? new GateGenerationLimits(1, 100, 2, 4) : (GateGenerationLimits?)null,
                TargetValue = !omitTarget && (type == ProblemType.TargetValueSelection || gate && goal == GateGoal.ReachTarget) ? 25 : (int?)null,
                SkillProfile = skills,
            };
        }

        internal static string Signature(MathProblem p)
            => p.Id + "|" + p.Seed + "|" + p.Type + "|" + p.DisplayExpression + "|" + p.CorrectAnswer.ToDisplayString()
                + "|" + p.Goal + "|" + p.TargetValue + "|" + p.GateBaseValue + "|" + string.Join(",", p.SkillTags)
                + "|" + string.Join(";", p.Options.Select(o => o.OptionIndex + ":" + o.Lane.Value + ":" + o.Value.ToDisplayString()
                    + ":" + o.IsCorrect + ":" + o.DisplayLabel + ":" + o.GateOperation?.ToDisplayString() + ":" + o.DistractorErrorModel));

        private static MathProblemGenerator Generator(IExpressionGenerator expressions, IProblemValidator validator)
        {
            var evaluator = new ExpressionEvaluator();
            return new MathProblemGenerator(expressions, evaluator, new EquationSolver(evaluator), new DistractorGenerator(), new SkillClassifier(), validator);
        }

        private sealed class RecordingExpressionGenerator : IExpressionGenerator
        {
            public List<ExpressionGenerationRequest> Requests { get; } = new List<ExpressionGenerationRequest>();
            private readonly bool _fail;
            public RecordingExpressionGenerator(bool fail = false) { _fail = fail; }
            public Result<GeneratedExpression> Generate(ExpressionGenerationRequest request)
            {
                Requests.Add(request);
                if (_fail) return Result<GeneratedExpression>.Fail("GENERATION_EXHAUSTED");
                var expr = ProblemValidatorTests.Binary(BinaryOperator.Addition, 6, 4);
                return Result<GeneratedExpression>.Ok(new GeneratedExpression(expr, new EvaluationResult(10), null, Array.Empty<SkillTag>(), 1));
            }
        }

        private sealed class RecordingValidator : IProblemValidator
        {
            public List<MathProblem> Candidates { get; } = new List<MathProblem>();
            private readonly int _reject;
            public RecordingValidator(int reject = 0) { _reject = reject; }
            public ValidationReport Validate(MathProblem problem, DifficultyProfile profile)
            {
                Candidates.Add(problem);
                return new ValidationReport(Candidates.Count <= _reject
                    ? new[] { new ValidationIssue("TEST_REJECTED", "Reject candidate.", ValidationSeverity.Error) }
                    : Array.Empty<ValidationIssue>());
            }
            public bool IsValid(MathProblem problem, DifficultyProfile profile) => Validate(problem, profile).IsValid;
        }

        private sealed class ExtraDrawDistractorGenerator : IDistractorGenerator
        {
            public Result<IReadOnlyList<Distractor>> Generate(DistractorRequest request)
            {
                for (int i = 0; i < 57; i++) request.Random.NextDouble();
                return new DistractorGenerator().Generate(request);
            }
        }
    }
}
