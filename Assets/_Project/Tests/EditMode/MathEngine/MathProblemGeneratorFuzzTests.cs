using System;
using MathHorde.Core.Resources;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    [TestFixture]
    [Category("Fuzz")]
    public class MathProblemGeneratorFuzzTests
    {
        private const int Samples = 10000;
        private const int MasterSeed = 209000;

        [TestCase(ProblemType.ResourceOperationComparison, 2)]
        [TestCase(ProblemType.ResourceOperationComparison, 3)]
        [TestCase(ProblemType.ResourceOperationComparison, 4)]
        [TestCase(ProblemType.AnswerLaneSelection, 2)]
        [TestCase(ProblemType.AnswerLaneSelection, 3)]
        [TestCase(ProblemType.AnswerLaneSelection, 4)]
        [TestCase(ProblemType.MissingOperand, 2)]
        [TestCase(ProblemType.MissingOperand, 3)]
        [TestCase(ProblemType.MissingOperand, 4)]
        [TestCase(ProblemType.EquationSolving, 2)]
        [TestCase(ProblemType.EquationSolving, 3)]
        [TestCase(ProblemType.EquationSolving, 4)]
        [TestCase(ProblemType.TargetValueSelection, 2)]
        [TestCase(ProblemType.TargetValueSelection, 3)]
        [TestCase(ProblemType.TargetValueSelection, 4)]
        public void Generate_TenThousandSeedsPerTypeAndOptionCount_ValidAndNoAnswerPositionStrategy(ProblemType type, int count)
        {
            var generator = new MathProblemGenerator();
            var evaluator = new ExpressionEvaluator();
            var solver = new EquationSolver(evaluator);
            var validator = new ProblemValidator();
            var lanes = new int[count];
            var ids = new HashSet<MathProblemId>();
            int largest = 0, smallest = 0;
            for (int i = 0; i < Samples; i++)
            {
                var goal = type == ProblemType.ResourceOperationComparison ? (GateGoal)(i % 3) : GateGoal.Maximize;
                var request = MathProblemGeneratorTests.Request(seed: MasterSeed + i, type: type, count: count, goal: goal,
                    capacity: type == ProblemType.ResourceOperationComparison ? 40 + i % 100 : 1000);
                var result = generator.Generate(request);
                string context = "type " + type + " seed " + request.Seed + " count " + count + " goal " + goal;
                Assert.That(result.IsSuccess, Is.True, context + ": " + result.ErrorCode);
                var p = result.Value;
                var report = validator.Validate(p, request.Difficulty);
                Assert.That(report.IsValid, Is.True, context + ": " + string.Join(",", report.Issues.Select(issue => issue.Code)));
                Assert.That(p.Seed, Is.EqualTo(request.Seed), context + " metadata seed");
                Assert.That(p.Type, Is.EqualTo(type), context + " metadata type");
                Assert.That(p.DifficultyLevel, Is.EqualTo(request.Difficulty.ChallengeLevel), context + " metadata difficulty");
                Assert.That(p.Id.Value, Is.Not.EqualTo(Guid.Empty), context + " metadata id");
                Assert.That(ids.Add(p.Id), Is.True, context + " duplicate id");
                Assert.That(p.Options.Count, Is.InRange(2, 4), context + " option count bounds");
                Assert.That(p.Options.Count, Is.EqualTo(count), context);
                Assert.That(p.Options.Count(o => o.IsCorrect), Is.EqualTo(1), context);
                Assert.That(p.Options.Select(o => o.Value).Distinct().Count(), Is.EqualTo(count), context);
                Assert.That(p.Options.Select(o => o.Lane).Distinct().Count(), Is.EqualTo(count), context);
                Assert.That(p.Options.All(o => o.Lane.Value >= 0 && o.Lane.Value < count), Is.True, context);
                Assert.That(p.Options.All(o => !o.IsCorrect || !o.DistractorErrorModel.HasValue), Is.True, context);
                Assert.That(p.Options.All(o => o.GateOperation != null || o.IsCorrect || o.DistractorErrorModel.HasValue), Is.True, context);
                var correct = p.Options.Single(o => o.IsCorrect);
                Assert.That(correct.Value, Is.EqualTo(p.CorrectAnswer), context);
                lanes[correct.Lane.Value]++;
                if (p.CorrectAnswer.Value == p.Options.Max(o => o.Value.Value)) largest++;
                if (p.CorrectAnswer.Value == p.Options.Min(o => o.Value.Value)) smallest++;
                AssertOptionOracle(p, context);
                if (type == ProblemType.ResourceOperationComparison)
                {
                    AssertGateOracle(p, result.Value.GateBaseValue.Value, request.Difficulty.MinGateGap, evaluator, context);
                }
                else if (type == ProblemType.MissingOperand || type == ProblemType.EquationSolving)
                {
                    var equation = (EqualityNode)p.Expression;
                    Assert.That(solver.Solve(equation, EvaluationOptions.Default).Value, Is.EqualTo(p.CorrectAnswer.Value), context);
                    Assert.That(evaluator.CheckEquality(equation, p.CorrectAnswer.Value, EvaluationOptions.Default).Value, Is.True, context);
                }
                else
                {
                    Assert.That(evaluator.Evaluate(p.Expression, EvaluationOptions.Default).Value, Is.EqualTo(p.CorrectAnswer), context);
                    if (type == ProblemType.TargetValueSelection) Assert.That(p.CorrectAnswer.Value, Is.EqualTo(p.TargetValue.Value), context);
                }

                if (i % 97 == 0)
                    Assert.That(MathProblemGeneratorTests.Signature(generator.Generate(request).Value), Is.EqualTo(MathProblemGeneratorTests.Signature(p)), context);
            }

            double expected = (double)Samples / count;
            double chiSquared = lanes.Sum(n => (n - expected) * (n - expected) / expected);
            // Upper-tail 1% quantiles, df = count - 1. No statistical dependency.
            double critical = count == 2 ? 6.634896601 : count == 3 ? 9.210340372 : 11.344866730;
            TestContext.WriteLine(type + " count=" + count + " N=" + Samples + " seeds=" + MasterSeed + ".." + (MasterSeed + Samples - 1)
                + " lanes=[" + string.Join(",", lanes) + "] chiSquared=" + chiSquared + " critical=" + critical
                + " largestRate=" + (double)largest / Samples + " smallestRate=" + (double)smallest / Samples);
            Assert.That(lanes.Sum(), Is.EqualTo(Samples), type + " complete statistical sample");
            Assert.That(chiSquared, Is.LessThan(critical), type + " lane chi-square, count " + count);
            if (type == ProblemType.ResourceOperationComparison) return; // goal-based extrema are intentional
            Assert.That((double)largest / Samples, Is.LessThanOrEqualTo(1.0 / count + .10), type + " largest, count " + count);
            Assert.That((double)smallest / Samples, Is.LessThanOrEqualTo(1.0 / count + .10), type + " smallest, count " + count);
        }

        [Test]
        public void Generate_TenThousandModuloAndRemainderProblems_OptionsMatchIndependentArithmetic()
        {
            var generator = new MathProblemGenerator();
            for (int i = 0; i < Samples; i++)
            {
                var op = i % 2 == 0 ? BinaryOperator.Modulo : BinaryOperator.IntegerDivisionWithRemainder;
                var profile = ProblemValidatorTests.Profile(count: 2 + i % 3, depth: 0, maxOperand: 30, operators: new[] { op });
                var request = MathProblemGeneratorTests.Request(seed: 2120000 + i, profile: profile);
                var result = generator.Generate(request);
                string context = "seed " + request.Seed + " operator " + op + " count " + profile.AnswerOptionCount;
                Assert.That(result.IsSuccess, Is.True, context + ": " + result.ErrorCode);
                Assert.That(new ProblemValidator().IsValid(result.Value, profile), Is.True, context);
                Assert.That(result.Value.Options.Count, Is.EqualTo(profile.AnswerOptionCount), context);
                Assert.That(result.Value.Options.Count(o => o.IsCorrect), Is.EqualTo(1), context);
                Assert.That(result.Value.Options.Select(o => o.Value).Distinct().Count(), Is.EqualTo(profile.AnswerOptionCount), context);
                Assert.That(result.Value.CorrectAnswer.Remainder.HasValue, Is.EqualTo(op == BinaryOperator.IntegerDivisionWithRemainder), context);
                AssertOptionOracle(result.Value, context);
            }
        }



        [Test]
        public void Generate_TenThousandMixedOperationGatePairs_LargerOperandIsNotAWinningStrategy()
        {
            var generator = new MathProblemGenerator();
            int hits = 0, measured = 0, nonObvious = 0;
            for (int i = 0; i < Samples; i++)
            {
                var profile = ProblemValidatorTests.Profile(count: 2, maxOperand: 40,
                    operators: new[] { BinaryOperator.Addition, BinaryOperator.Multiplication });
                var request = MathProblemGeneratorTests.Request(seed: 2091000 + i, type: ProblemType.ResourceOperationComparison,
                    count: 2, profile: profile);
                var result = generator.Generate(request);
                Assert.That(result.IsSuccess, Is.True, "seed " + request.Seed);
                var options = result.Value.Options;
                int a = ((ConstantNode)((BinaryOperationNode)options[0].GateOperation).Right).Value;
                int b = ((ConstantNode)((BinaryOperationNode)options[1].GateOperation).Right).Value;
                Assert.That(((BinaryOperationNode)options[0].GateOperation).Operator,
                    Is.Not.EqualTo(((BinaryOperationNode)options[1].GateOperation).Operator));
                if (a == b) continue;
                measured++;
                bool wins = options[a > b ? 0 : 1].IsCorrect;
                if (wins) hits++; else nonObvious++;
            }
            Assert.That(measured, Is.GreaterThan(9000));
            Assert.That((double)hits / measured, Is.LessThanOrEqualTo(.70));
            Assert.That((double)nonObvious / measured, Is.GreaterThanOrEqualTo(.30));
        }

        private static void AssertGateOracle(MathProblem p, int basis, int gap, IExpressionEvaluator evaluator, string context, params object[] unused)
        {
            foreach (var option in p.Options)
            {
                var tree = (BinaryOperationNode)option.GateOperation;
                Assert.That(((ConstantNode)tree.Left).Value, Is.EqualTo(basis), context);
                Assert.That(evaluator.Evaluate(tree, EvaluationOptions.Default).Value, Is.EqualTo(option.Value), context);
                foreach (var other in p.Options)
                    if (other.OptionIndex != option.OptionIndex)
                        Assert.That(Math.Abs((long)option.Value.Value - other.Value.Value), Is.GreaterThanOrEqualTo(gap), context);
            }
            var best = p.Options.OrderBy(o => p.Goal == GateGoal.Maximize ? -(long)o.Value.Value
                : p.Goal == GateGoal.Minimize ? o.Value.Value : Math.Abs((long)o.Value.Value - p.TargetValue.Value)).First();
            Assert.That(best.IsCorrect, Is.True, context);
            Assert.That(p.Reward.Resource, Is.EqualTo(ResourceType.Health), context);
            Assert.That(p.Reward.FlatAmount, Is.GreaterThan(0), context);
        }

        private static void AssertOptionOracle(MathProblem p, string context)
        {
            if (p.Type == ProblemType.ResourceOperationComparison) return;
            var equation = p.Expression as EqualityNode;
            if (equation != null)
            {
                foreach (var option in p.Options)
                {
                    EvaluationResult left, right;
                    bool matches = !option.Value.Remainder.HasValue
                        && TryEvaluate(equation.Left, option.Value.Value, out left)
                        && TryEvaluate(equation.Right, option.Value.Value, out right) && left == right;
                    Assert.That(matches, Is.EqualTo(option.IsCorrect), context + " substituted option " + option.OptionIndex);
                }
                return;
            }

            EvaluationResult answer;
            Assert.That(TryEvaluate(p.Expression, 0, out answer), Is.True, context + " expression arithmetic");
            Assert.That(p.CorrectAnswer, Is.EqualTo(answer), context + " independent answer");
            foreach (var option in p.Options)
                Assert.That(option.Value == answer, Is.EqualTo(option.IsCorrect), context + " option truth " + option.OptionIndex);
        }

        private static bool TryEvaluate(ExpressionNode node, int unknown, out EvaluationResult result)
        {
            result = default(EvaluationResult);
            if (node is ConstantNode constant) { result = new EvaluationResult(constant.Value); return true; }
            if (node is UnknownNode) { result = new EvaluationResult(unknown); return true; }
            var binary = node as BinaryOperationNode;
            EvaluationResult left, right;
            if (binary == null || !TryEvaluate(binary.Left, unknown, out left) || !TryEvaluate(binary.Right, unknown, out right)
                || left.Remainder.HasValue || right.Remainder.HasValue) return false;
            long a = left.Value, b = right.Value, value;
            int? remainder = null;
            switch (binary.Operator)
            {
                case BinaryOperator.Addition: value = a + b; break;
                case BinaryOperator.Subtraction: value = a - b; break;
                case BinaryOperator.Multiplication: value = a * b; break;
                case BinaryOperator.Division:
                    if (b == 0 || a % b != 0) return false;
                    value = a / b; break;
                case BinaryOperator.Modulo:
                    if (b <= 0) return false;
                    value = (a % b + b) % b; break;
                case BinaryOperator.IntegerDivisionWithRemainder:
                    if (b == 0) return false;
                    remainder = (int)((a % Math.Abs(b) + Math.Abs(b)) % Math.Abs(b));
                    value = (a - remainder.Value) / b; break;
                default: return false;
            }
            if (value < int.MinValue || value > int.MaxValue) return false;
            result = new EvaluationResult((int)value, remainder);
            return true;
        }
    }
}
