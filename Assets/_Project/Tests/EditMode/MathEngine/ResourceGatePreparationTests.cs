using System;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.Core.Skills;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // Historical filename retained; ADR-019 retires the Prepare/Bind API.
    public class ResourceGatePreparationTests
    {
        internal static DifficultyProfile Profile(int count = 2, int capacity = 30, int maxResult = 1000,
            int gap = 1, bool negative = false, IReadOnlyCollection<BinaryOperator> operators = null,
            IReadOnlyCollection<SkillTag> skills = null)
            => ProblemValidatorTests.Profile(count: count, maxOperand: capacity, maxResult: maxResult, gap: gap,
                depth: 0, allowNegative: negative, operators: operators ?? new[] { BinaryOperator.Addition, BinaryOperator.Subtraction }, skills: skills);

        [TestCase(GateGoal.Maximize)]
        [TestCase(GateGoal.Minimize)]
        public void CL1_TenThousandSeeds_RespectEveryInvariantAndHaveBalancedLanes(GateGoal goal)
        {
            var profile = new DifficultyProfile(1, 1, 1, 1,
                new[] { BinaryOperator.Addition, BinaryOperator.Subtraction },
                new[] { SkillTag.Addition, SkillTag.Subtraction, SkillTag.SingleDigitAddition, SkillTag.ResourceComparison },
                1, 10, 10, 2, 2, false, 0, false, 0, false, 2, 14, 8, true);
            var request = new ProblemGenerationRequest { Seed = 1819000, Type = ProblemType.ResourceOperationComparison,
                Difficulty = profile, LaneCount = 3, ThinkTimeSeconds = 14, Goal = goal,
                RewardResource = ResourceType.Health, Reward = new RewardSpec(ResourceType.Health, 30),
                GateLimits = new GateGenerationLimits(2, 50, 2, 4) };
            int[] lanes = new int[2];
            foreach (var result in new MathProblemGenerator().GenerateBatch(request, 10000))
            {
                Assert.That(result.IsSuccess, Is.True, result.ErrorCode);
                var p = result.Value;
                Assert.That(new ProblemValidator().IsValid(p, profile), Is.True, "seed=" + p.Seed);
                Assert.That(p.GateBaseValue, Is.InRange(2, 10));
                Assert.That(Math.Abs(p.Options[0].Value.Value - p.Options[1].Value.Value), Is.GreaterThanOrEqualTo(8));
                lanes[p.Options.Single(o => o.IsCorrect).Lane.Value]++;
                foreach (var option in p.Options)
                {
                    var op = (BinaryOperationNode)option.GateOperation;
                    Assert.That(((ConstantNode)op.Left).Value, Is.EqualTo(p.GateBaseValue.Value));
                    Assert.That(((ConstantNode)op.Right).Value, Is.InRange(2, 10));
                    Assert.That(option.Value.Value, Is.InRange(0, 10));
                }
            }
            Assert.That(lanes[0], Is.InRange(4700, 5300));
            Assert.That(lanes[1], Is.InRange(4700, 5300));
            TestContext.WriteLine("CL1 " + goal + ": 10000/10000 passed; lanes=" + string.Join(",", lanes));
        }

        [TestCase(ResourceType.Shield, "REWARD_RESOURCE_NOT_ALLOWED")]
        [TestCase(ResourceType.EnemyArmor, "REWARD_RESOURCE_NOT_ALLOWED")]
        [TestCase(ResourceType.Energy, "REWARD_RESOURCE_NOT_ALLOWED")]
        [TestCase(ResourceType.Ammo, "AMMO_NOT_ALLOWED")]
        public void BothEntryPointsRejectEveryNonHealthReward(ResourceType resource, string error)
        {
            var good = MathProblemGeneratorTests.Request(type: ProblemType.ResourceOperationComparison);
            var bad = new ProblemGenerationRequest { Seed = good.Seed, Type = good.Type, Difficulty = good.Difficulty,
                LaneCount = 4, ThinkTimeSeconds = 6, RewardResource = resource, Reward = new RewardSpec(resource, 30), GateLimits = good.GateLimits };
            Assert.That(new MathProblemGenerator().Generate(bad).ErrorCode, Is.EqualTo(error));
            var p = new MathProblemGenerator().Generate(good).Value;
            var candidate = new MathProblem(p.Id, p.Type, p.Expression, p.DisplayExpression, p.CorrectAnswer,
                p.Options, p.DifficultyLevel, p.SkillTags, p.ThinkTimeSeconds, bad.Reward, p.Seed, p.Goal, p.TargetValue, null, p.GateBaseValue);
            Assert.That(ProblemValidatorTests.Codes(new ProblemValidator().Validate(candidate, good.Difficulty)), Does.Contain(error));
        }

        [Test]
        public void GlobalLimitsAndEmptyIntersectionsAreEnforced()
        {
            var generator = new MathProblemGenerator();
            var request = new ProblemGenerationRequest { Type = ProblemType.ResourceOperationComparison, Difficulty = Profile(capacity: 30),
                LaneCount = 2, ThinkTimeSeconds = 6, RewardResource = ResourceType.Health, Reward = new RewardSpec(ResourceType.Health, 30),
                GateLimits = new GateGenerationLimits(3, 7, 2, 4) };
            foreach (var result in generator.GenerateBatch(request, 100))
            {
                Assert.That(result.IsSuccess, Is.True);
                foreach (var option in result.Value.Options)
                {
                    var tree = (BinaryOperationNode)option.GateOperation;
                    Assert.That(((ConstantNode)tree.Left).Value, Is.InRange(3, 7));
                    Assert.That(((ConstantNode)tree.Right).Value, Is.InRange(3, 7));
                }
            }
            var invalid = new ProblemGenerationRequest { Type = request.Type, Difficulty = Profile(capacity: 2), LaneCount = 2,
                ThinkTimeSeconds = 6, RewardResource = ResourceType.Health, Reward = request.Reward, GateLimits = request.GateLimits };
            Assert.That(generator.Generate(invalid).ErrorCode, Is.EqualTo("GATE_LIMITS_INVALID"));
        }

        [TestCase(2, BinaryOperator.Addition, 7, BinaryOperator.Subtraction, 2, GateGoal.Maximize, 0)]
        [TestCase(9, BinaryOperator.Subtraction, 1, BinaryOperator.Subtraction, 9, GateGoal.Maximize, 0)]
        [TestCase(9, BinaryOperator.Subtraction, 1, BinaryOperator.Subtraction, 9, GateGoal.Minimize, 1)]
        [TestCase(20, BinaryOperator.Multiplication, 2, BinaryOperator.Addition, 25, GateGoal.Maximize, 1)]
        public void ContractReferenceGatesAreValid(int basis, BinaryOperator first, int a, BinaryOperator second, int b, GateGoal goal, int winner)
        {
            var trees = new[] { ProblemValidatorTests.Binary(first, basis, a), ProblemValidatorTests.Binary(second, basis, b) };
            var evaluator = new ExpressionEvaluator();
            var options = trees.Select((tree, i) => new AnswerOption(i, new Core.Common.LaneIndex(i),
                evaluator.Evaluate(tree, EvaluationOptions.Default).Value, tree, i == winner, null, tree.ToDisplayString())).ToArray();
            var profile = basis == 20 ? Profile(capacity: 100, maxResult: 100, gap: 4, operators: new[] { BinaryOperator.Addition, BinaryOperator.Multiplication }) : Profile(capacity: 10, maxResult: 10, gap: 8);
            var tags = trees.SelectMany(tree => new SkillClassifier().Classify(tree, ProblemType.ResourceOperationComparison)).Distinct().ToArray();
            var p = new MathProblem(new MathProblemId(Guid.NewGuid()), ProblemType.ResourceOperationComparison,
                trees[winner], basis.ToString(), options[winner].Value, options, basis == 20 ? 5 : 1, tags, 14,
                new RewardSpec(ResourceType.Health, 30), 0, goal, null, null, basis);
            Assert.That(new ProblemValidator().IsValid(p, profile), Is.True);
            Assert.That(p.Reward.FlatAmount, Is.EqualTo(30));
        }

        [Test]
        public void SevenTimesEightReferenceUsesFourLanesAndFixedHealthReward()
        {
            var expr = ProblemValidatorTests.Binary(BinaryOperator.Multiplication, 7, 8);
            var options = new[] { 54, 56, 55, 58 }.Select((value, i) => new AnswerOption(i,
                new Core.Common.LaneIndex(i), new EvaluationResult(value), null, value == 56, null, value.ToString())).ToArray();
            var p = new MathProblem(new MathProblemId(Guid.NewGuid()), ProblemType.AnswerLaneSelection, expr,
                "7 × 8 = ?", new EvaluationResult(56), options, 3, new SkillClassifier().Classify(expr, ProblemType.AnswerLaneSelection),
                14, new RewardSpec(ResourceType.Health, 15), 0, GateGoal.Maximize, null, null);
            Assert.That(new ProblemValidator().IsValid(p, Profile(count: 4, capacity: 10, operators: new[] { BinaryOperator.Multiplication })), Is.True);
            Assert.That(p.Options.Select(o => o.Lane.Value), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(p.Options.Single(o => o.IsCorrect).Value.Value, Is.EqualTo(56));
        }
    }
}
