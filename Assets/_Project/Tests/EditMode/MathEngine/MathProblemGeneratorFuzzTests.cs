using System;
using System.Linq;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    [TestFixture]
    public class MathProblemGeneratorFuzzTests
    {
        private const int Samples = 10000;

        [TestCase(ProblemType.ResourceOperationComparison)]
        [TestCase(ProblemType.AnswerLaneSelection)]
        [TestCase(ProblemType.MissingOperand)]
        [TestCase(ProblemType.EquationSolving)]
        [TestCase(ProblemType.TargetValueSelection)]
        public void Generate_TenThousandSeedsPerType_ValidReproducibleAndNoAnswerPositionStrategy(ProblemType type)
        {
            var generator = new MathProblemGenerator();
            var evaluator = new ExpressionEvaluator();
            var solver = new EquationSolver(evaluator);
            var validator = new ProblemValidator();
            var lanes = new[] { new int[2], new int[3], new int[4] };
            var largest = new int[3];
            var smallest = new int[3];
            var totals = new int[3];
            for (int i = 0; i < Samples; i++)
            {
                int count = 2 + i % 3;
                var goal = type == ProblemType.ResourceOperationComparison ? (GateGoal)(i / 3 % 3) : GateGoal.Maximize;
                var request = MathProblemGeneratorTests.Request(seed: 209000 + i, type: type, count: count, goal: goal,
                    capacity: type == ProblemType.ResourceOperationComparison ? 40 + i % 100 : 1000);
                var result = generator.Generate(request);
                string context = "type " + type + " seed " + request.Seed + " count " + count + " goal " + goal;
                Assert.That(result.IsSuccess, Is.True, context + ": " + result.ErrorCode);
                var p = result.Value;
                var report = validator.Validate(p, request.Difficulty);
                Assert.That(report.IsValid, Is.True, context + ": " + string.Join(",", report.Issues.Select(issue => issue.Code)));
                Assert.That(p.Options.Count, Is.EqualTo(count), context);
                Assert.That(p.Options.Count(o => o.IsCorrect), Is.EqualTo(1), context);
                Assert.That(p.Options.Select(o => o.Value).Distinct().Count(), Is.EqualTo(count), context);
                Assert.That(p.Options.Select(o => o.Lane).Distinct().Count(), Is.EqualTo(count), context);
                Assert.That(p.Options.All(o => o.Lane.Value >= 0 && o.Lane.Value < count), Is.True, context);
                Assert.That(p.Options.All(o => !o.IsCorrect || !o.DistractorErrorModel.HasValue), Is.True, context);
                Assert.That(p.Options.All(o => o.GateOperation != null || o.IsCorrect || o.DistractorErrorModel.HasValue), Is.True, context);
                var correct = p.Options.Single(o => o.IsCorrect);
                Assert.That(correct.Value, Is.EqualTo(p.CorrectAnswer), context);
                lanes[count - 2][correct.Lane.Value]++;
                totals[count - 2]++;
                if (p.CorrectAnswer.Value == p.Options.Max(o => o.Value.Value)) largest[count - 2]++;
                if (p.CorrectAnswer.Value == p.Options.Min(o => o.Value.Value)) smallest[count - 2]++;
                if (type == ProblemType.ResourceOperationComparison)
                {
                    AssertGateOracle(p, request.ResourceSnapshotValue.Value, request.Difficulty.MinGateGap, evaluator, context);
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

            for (int count = 2; count <= 4; count++)
            {
                double expected = (double)totals[count - 2] / count;
                double chiSquared = lanes[count - 2].Sum(n => (n - expected) * (n - expected) / expected);
                double critical = count == 2 ? 6.635 : count == 3 ? 9.210 : 11.345; // p > .01, df = count - 1
                Assert.That(chiSquared, Is.LessThan(critical), type + " lane chi-square, count " + count);
                if (type == ProblemType.ResourceOperationComparison) continue; // goal-based extrema are intentional
                Assert.That((double)largest[count - 2] / totals[count - 2], Is.LessThanOrEqualTo(1.0 / count + .10), type + " largest, count " + count);
                Assert.That((double)smallest[count - 2] / totals[count - 2], Is.LessThanOrEqualTo(1.0 / count + .10), type + " smallest, count " + count);
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

        private static void AssertGateOracle(MathProblem p, int snapshot, int gap, IExpressionEvaluator evaluator, string context)
        {
            var values = p.Options.Select(o => o.Value.Value).ToArray();
            var effective = values.Select(v => Math.Min(v, p.ResourceCapacity.Value)).ToArray();
            for (int i = 0; i < p.Options.Count; i++)
            {
                Assert.That(evaluator.EvaluateWith(p.Options[i].GateOperation, snapshot, EvaluationOptions.Default).Value, Is.EqualTo(p.Options[i].Value), context);
                for (int j = i + 1; j < p.Options.Count; j++)
                    Assert.That(Math.Abs((long)effective[i] - effective[j]), Is.GreaterThanOrEqualTo(gap), context);
            }
            Func<int, long> score = v => p.Goal == GateGoal.Maximize ? -(long)v
                : p.Goal == GateGoal.Minimize ? v : Math.Abs((long)v - p.TargetValue.Value);
            var rawScores = values.Select(score).ToArray();
            var effectiveScores = effective.Select(score).ToArray();
            int rawWinner = Array.IndexOf(rawScores, rawScores.Min());
            int effectiveWinner = Array.IndexOf(effectiveScores, effectiveScores.Min());
            Assert.That(rawScores.Count(v => v == rawScores.Min()), Is.EqualTo(1), context);
            Assert.That(effectiveScores.Count(v => v == effectiveScores.Min()), Is.EqualTo(1), context);
            Assert.That(rawWinner, Is.EqualTo(effectiveWinner), context);
            Assert.That(p.Options[rawWinner].IsCorrect, Is.True, context);
        }
    }
}
