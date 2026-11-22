using System;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    [TestFixture]
    public class ProblemValidatorFuzzTests
    {
        private const int Samples = 10000;

        [Test]
        public void Validate_TenThousandSeededGeneratedExpressions_ChecksAllProblemTypesAndMutations()
        {
            var evaluator = new ExpressionEvaluator();
            var generator = new ExpressionGenerator(evaluator, new EquationSolver(evaluator));
            var validator = new ProblemValidator();
            var master = new DeterministicRandomSource(208013);
            var counts = new int[5];
            for (int i = 0; i < Samples; i++)
            {
                var rng = master.Derive("case-" + i);
                var type = (ProblemType)(i % 5);
                bool unknown = type == ProblemType.MissingOperand || type == ProblemType.EquationSolving;
                int count = rng.Next(2, 5);
                var generated = generator.Generate(new ExpressionGenerationRequest
                {
                    Random = rng.Derive("expression"),
                    AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Subtraction, BinaryOperator.Multiplication, BinaryOperator.Division },
                    MinOperandValue = 1, MaxOperandValue = 20, MaxResultValue = 1000,
                    MinOperandCount = 2, MaxOperandCount = 4,
                    AllowParentheses = true, MaxParenthesisDepth = 2,
                    IncludeUnknown = unknown, UnknownPlacement = unknown ? UnknownPlacement.EitherSide : UnknownPlacement.None,
                    Evaluation = new EvaluationOptions { AllowNegativeIntermediate = true, AllowNegativeResult = false },
                });
                Assert.That(generated.IsSuccess, Is.True, "generation seed " + rng.Seed);
                ExpressionNode expr = generated.Value.Root;
                var answer = unknown ? new EvaluationResult(generated.Value.UnknownSolution.Value) : generated.Value.Value;
                int? capacity = null, target = null;
                var goal = GateGoal.Maximize;
                AnswerOption[] options;
                if (type == ProblemType.ResourceOperationComparison)
                {
                    goal = (GateGoal)rng.Next(0, 3);
                    int start = rng.Next(10, 100), gap = rng.Next(4, 15);
                    var values = Enumerable.Range(0, count).Select(j => start + gap * j).ToArray();
                    int winner = goal == GateGoal.Maximize ? count - 1 : 0;
                    if (goal == GateGoal.ReachTarget)
                    {
                        winner = rng.Next(0, count);
                        target = values[winner] + 1;
                    }

                    Shuffle(values, rng.Derive("values"));
                    int correctValue = start + gap * winner;
                    options = values.Select((value, lane) => new AnswerOption(lane, new LaneIndex(lane), new EvaluationResult(value),
                        ProblemValidatorTests.Binary(BinaryOperator.Addition, 1, value - 1), value == correctValue, null, "+" + (value - 1))).ToArray();
                    answer = new EvaluationResult(correctValue);
                    expr = options.Single(o => o.IsCorrect).GateOperation;
                    capacity = 1;
                }
                else
                {
                    int rank = rng.Next(0, count);
                    var values = Enumerable.Range(0, count).Select(j => answer.Value + j - rank).ToArray();
                    Shuffle(values, rng.Derive("lanes"));
                    options = values.Select((value, lane) => ProblemValidatorTests.Option(lane, value, value == answer.Value, answer.Remainder)).ToArray();
                    if (type == ProblemType.TargetValueSelection) target = answer.Value;
                }

                var profile = ProblemValidatorTests.Profile(count: count, unknowns: unknown ? 1 : 0,
                    maxOperand: 1000, maxResult: 1000, allowNegative: true);
                var problem = ProblemValidatorTests.Candidate(expr: expr, answer: answer, options: options,
                    type: type, capacity: capacity, goal: goal, target: target, seed: rng.Seed);
                var report = validator.Validate(problem, profile);
                string context = "seed " + rng.Seed + ": " + string.Join(",", ProblemValidatorTests.Codes(report));
                Assert.That(report.IsValid, Is.True, context);
                Assert.That(validator.IsValid(problem, profile), Is.EqualTo(report.IsValid), context);
                Assert.That(options.Count(o => o.IsCorrect), Is.EqualTo(1), context);
                Assert.That(options.Select(o => o.Value).Distinct().Count(), Is.EqualTo(count), context);
                counts[(int)type]++;

                var wrong = ProblemValidatorTests.Candidate(expr: expr, answer: new EvaluationResult(answer.Value + 1), options: options,
                    type: type, capacity: capacity, goal: goal, target: target);
                Assert.That(ProblemValidatorTests.Codes(validator.Validate(wrong, profile)), Does.Contain("ANSWER_MISMATCH"), context);
                Assert.That(validator.IsValid(wrong, profile), Is.False, context);
            }

            Assert.That(counts, Is.EqualTo(new[] { 2000, 2000, 2000, 2000, 2000 }));
        }

        [Test]
        public void Validate_TenThousandSeededGateContexts_MatchesIndependentArithmeticOracle()
        {
            var rng = new DeterministicRandomSource(208014);
            var validator = new ProblemValidator();
            for (int i = 0; i < Samples; i++)
            {
                int count = rng.Next(2, 5), capacity = rng.Next(1, 150), gap = rng.Next(1, 12);
                var values = Enumerable.Range(0, count).Select(_ => rng.Next(-100, 200)).ToArray();
                var goal = (GateGoal)rng.Next(0, 3);
                int? target = goal == GateGoal.ReachTarget ? rng.Next(-100, 200) : (int?)null;
                var effective = values;
                var rawScores = values.Select(v => Score(v, goal, target)).ToArray();
                var effectiveScores = effective.Select(v => Score(v, goal, target)).ToArray();
                bool rawUnique = rawScores.Count(s => s == rawScores.Min()) == 1;
                bool effectiveUnique = effectiveScores.Count(s => s == effectiveScores.Min()) == 1;
                bool tie = values.Distinct().Count() != count || effective.Distinct().Count() != count || !rawUnique || !effectiveUnique;
                bool close = values.SelectMany((_, a) => values.Skip(a + 1).Select((__, offset) => Math.Abs((long)effective[a] - effective[a + offset + 1]) < gap)).Any(v => v);
                bool inverted = rawUnique && effectiveUnique && Array.IndexOf(rawScores, rawScores.Min()) != Array.IndexOf(effectiveScores, effectiveScores.Min());
                var problem = ProblemValidatorTests.Gates(values, capacity, goal, target);
                var profile = ProblemValidatorTests.Profile(count: count, gap: gap, allowNegative: true, maxOperand: 1000);
                var report = validator.Validate(problem, profile);
                var codes = ProblemValidatorTests.Codes(report);
                string context = "iteration " + i;
                Assert.That(codes.Contains("GATE_TIE"), Is.EqualTo(tie), context);
                Assert.That(codes.Contains("GATE_GAP_TOO_SMALL"), Is.EqualTo(close), context);
                Assert.That(validator.IsValid(problem, profile), Is.EqualTo(report.IsValid), context);
            }
        }

        [Test]
        public void SeededCandidateBatch_CorrectLanesAndExtremeHeuristics_HaveNoWinningPattern()
        {
            // Tests the validator with independently assembled fixtures. The
            // production MathProblemGenerator's distribution gate belongs to F2-09.
            var master = new DeterministicRandomSource(208015);
            var validator = new ProblemValidator();
            for (int count = 2; count <= 4; count++)
            {
                var lanes = new int[count];
                int largestHits = 0, smallestHits = 0;
                for (int i = 0; i < Samples; i++)
                {
                    var rng = master.Derive(count + ":" + i);
                    int answer = rng.Next(20, 200), rank = rng.Next(0, count);
                    var values = Enumerable.Range(0, count).Select(j => answer + j - rank).ToArray();
                    Shuffle(values, rng);
                    int lane = Array.IndexOf(values, answer);
                    lanes[lane]++;
                    if (answer == values.Max()) largestHits++;
                    if (answer == values.Min()) smallestHits++;
                    var expr = ProblemValidatorTests.Binary(BinaryOperator.Addition, answer - 10, 10);
                    Assert.That(validator.IsValid(ProblemValidatorTests.Candidate(expr: expr, answer: new EvaluationResult(answer),
                        options: ProblemValidatorTests.Options(answer, lane, values)), ProblemValidatorTests.Profile(count: count, maxOperand: 200)), Is.True);
                }

                double expected = (double)Samples / count;
                double chiSquared = lanes.Sum(n => (n - expected) * (n - expected) / expected);
                double critical = count == 2 ? 6.635 : count == 3 ? 9.210 : 11.345; // p = .01, df = Count-1
                Assert.That(chiSquared, Is.LessThan(critical), "lane chi-square, count " + count);
                Assert.That((double)largestHits / Samples, Is.LessThanOrEqualTo(1.0 / count + .10));
                Assert.That((double)smallestHits / Samples, Is.LessThanOrEqualTo(1.0 / count + .10));
            }
        }

        private static long Score(int value, GateGoal goal, int? target)
            => goal == GateGoal.Maximize ? -(long)value : goal == GateGoal.Minimize ? value : Math.Abs((long)value - target.Value);

        private static void Shuffle<T>(T[] values, IRandomSource rng)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                T value = values[i]; values[i] = values[j]; values[j] = value;
            }
        }
    }
}
