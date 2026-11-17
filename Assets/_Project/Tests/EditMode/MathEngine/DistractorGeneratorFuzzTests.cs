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
    // 10 000 seedelt feladat invariáns-ellenőrzése az IDistractorGenerator-ra
    // (math-horde.md 17.1, contract math-engine.md 9. "Tesztelhetőségi
    // szerződés", task F2-07). Minden iteráció egy, a master-seedből
    // determinisztikusan levezetett IRandomSource-ágat kap (ADR-006) — nincs
    // System.Random. A generálásra átadott Random MINDIG egy FRISSEN
    // konstruált DeterministicRandomSource ugyanazzal a (levezetett) seeddel,
    // soha nem az iteráció paraméter-választásához használt rng maga — így a
    // teszt-paraméterezés és a generátor saját determinizmusa nem
    // keveredik össze (lásd ExpressionGeneratorFuzzTests.cs precedens).
    [TestFixture]
    public class DistractorGeneratorFuzzTests
    {
        private const int IterationCount = 10_000;
        private const int MasterSeed = 270711;
        private const int CategoryCount = 10;

        private IExpressionEvaluator _evaluator;
        private DistractorGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new ExpressionEvaluator();
            _generator = new DistractorGenerator();
        }

        [Test]
        public void Generate_TenThousandSeededRequests_SatisfyAllInvariants()
        {
            var master = new DeterministicRandomSource(MasterSeed);

            for (int i = 0; i < IterationCount; i++)
            {
                IRandomSource paramRng = master.Derive("params-" + i);
                int genSeed = master.Derive("gen-" + i).Seed;

                int category = i % CategoryCount;
                string ctx = "category " + category + ", iteration " + i;

                var request = BuildRequestForCategory(category, paramRng, genSeed);
                var result = _generator.Generate(request);

                if (!result.IsSuccess)
                {
                    Assert.That(result.ErrorCode, Is.EqualTo("DISTRACTOR_EXHAUSTED"), ctx);
                    continue;
                }

                VerifyInvariants(request, result.Value, ctx);

                // Determinism (rule 5): regenerating with a FRESH source
                // carrying the SAME seed must reproduce the exact same list,
                // in the exact same order.
                var repeatRequest = CloneWithFreshRandom(request, genSeed);
                var repeatResult = _generator.Generate(repeatRequest);
                Assert.That(repeatResult.IsSuccess, Is.True, ctx + " (replay)");
                var original = result.Value.Select(d => (d.Value.Value, d.Value.Remainder, d.ErrorModel)).ToList();
                var replay = repeatResult.Value.Select(d => (d.Value.Value, d.Value.Remainder, d.ErrorModel)).ToList();
                Assert.That(replay, Is.EqualTo(original), ctx + " (replay determinism)");
            }
        }

        private static void VerifyInvariants(DistractorRequest request, IReadOnlyList<Distractor> distractors, string ctx)
        {
            Assert.That(distractors.Count, Is.EqualTo(request.Count), ctx);

            var seen = new HashSet<EvaluationResult> { request.CorrectAnswer };
            foreach (var distractor in distractors)
            {
                // Rule 1/2 (math-engine.md 4.): every element differs from
                // every other element AND from the correct answer.
                Assert.That(seen.Contains(distractor.Value), Is.False, ctx + " duplicate " + distractor.Value.Value);
                seen.Add(distractor.Value);

                Assert.That(distractor.Value.Value, Is.GreaterThanOrEqualTo(request.MinValue), ctx);
                Assert.That(distractor.Value.Value, Is.LessThanOrEqualTo(request.MaxValue), ctx);

                if (!request.AllowNegative)
                {
                    Assert.That(distractor.Value.Value, Is.GreaterThanOrEqualTo(0), ctx);
                }

                if (distractor.Value.Remainder.HasValue)
                {
                    Assert.That(distractor.Value.Remainder.Value, Is.GreaterThanOrEqualTo(0), ctx);
                }

                Assert.That(string.IsNullOrEmpty(distractor.ExplanationKey), Is.False, ctx);
            }
        }

        private static DistractorRequest CloneWithFreshRandom(DistractorRequest original, int genSeed) => new DistractorRequest
        {
            Random = new DeterministicRandomSource(genSeed),
            Expression = original.Expression,
            CorrectAnswer = original.CorrectAnswer,
            Count = original.Count,
            PreferredModels = original.PreferredModels,
            MinValue = original.MinValue,
            MaxValue = original.MaxValue,
            AllowNegative = original.AllowNegative,
        };

        private DistractorRequest BuildRequestForCategory(int category, IRandomSource paramRng, int genSeed)
        {
            var random = new DeterministicRandomSource(genSeed);
            int count = paramRng.Next(1, 4); // 1..3

            switch (category)
            {
                case 0: // Multiplication root — NeighbourInTimesTable-eligible
                {
                    int a = paramRng.Next(2, 13);
                    int b = paramRng.Next(2, 13);
                    var expression = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(a), new ConstantNode(b));
                    var correct = Evaluate(expression);
                    return WideRequest(random, expression, correct, count, paramRng);
                }

                case 1: // Addition root — CarryError/SignError-eligible
                {
                    int a = paramRng.Next(0, 90);
                    int b = paramRng.Next(0, 90);
                    var expression = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(a), new ConstantNode(b));
                    var correct = Evaluate(expression);
                    return WideRequest(random, expression, correct, count, paramRng);
                }

                case 2: // Subtraction root (a >= b, default options disallow negative) — SignError/ReverseCalculationError-eligible
                {
                    int a = paramRng.Next(10, 100);
                    int b = paramRng.Next(0, a + 1);
                    var expression = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(a), new ConstantNode(b));
                    var correct = Evaluate(expression);
                    return WideRequest(random, expression, correct, count, paramRng);
                }

                case 3: // Division root, constructed divisor-first (never "sorsol, majd eldob") — ReverseCalculationError-eligible when reversible
                {
                    int divisor = paramRng.Next(1, 13);
                    int quotient = paramRng.Next(1, 20);
                    int dividend = divisor * quotient;
                    var expression = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(dividend), new ConstantNode(divisor));
                    var correct = Evaluate(expression);
                    return WideRequest(random, expression, correct, count, paramRng);
                }

                case 4: // Modulo root — RemainderMisread-eligible
                {
                    int divisor = paramRng.Next(2, 13);
                    int dividend = paramRng.Next(1, 100);
                    var expression = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(dividend), new ConstantNode(divisor));
                    var correct = Evaluate(expression);
                    return WideRequest(random, expression, correct, count, paramRng);
                }

                case 5: // IntegerDivisionWithRemainder root — RemainderMisread-eligible (swapped pair)
                {
                    int divisor = paramRng.Next(2, 13);
                    int quotient = paramRng.Next(1, 20);
                    int remainder = paramRng.Next(0, divisor);
                    int dividend = divisor * quotient + remainder;
                    var expression = new BinaryOperationNode(BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(dividend), new ConstantNode(divisor));
                    var correct = Evaluate(expression);
                    return WideRequest(random, expression, correct, count, paramRng);
                }

                case 6: // No-parens multi-tier tree (a + b*c) — OperatorPrecedenceError-eligible
                {
                    int a = paramRng.Next(1, 30);
                    int b = paramRng.Next(1, 12);
                    int c = paramRng.Next(1, 12);
                    var expression = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(a),
                        new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(b), new ConstantNode(c)));
                    var correct = Evaluate(expression);
                    return WideRequest(random, expression, correct, count, paramRng);
                }

                case 7: // Forced-parentheses tree (a+b) * c — IgnoredParentheses-eligible
                {
                    int a = paramRng.Next(1, 20);
                    int b = paramRng.Next(1, 20);
                    int c = paramRng.Next(2, 12);
                    var expression = new BinaryOperationNode(BinaryOperator.Multiplication,
                        new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(a), new ConstantNode(b), forceParentheses: true),
                        new ConstantNode(c));
                    var correct = Evaluate(expression);
                    return WideRequest(random, expression, correct, count, paramRng);
                }

                case 8: // EqualityNode with the unknown on one side — MissingOperand shape, must not crash
                {
                    int b = paramRng.Next(1, 12);
                    int d = paramRng.Next(1, 12);
                    int product = b * d;
                    // a in [0, product-1] guarantees unknownSolution = product - a
                    // is always >= 1 (WideRequest assumes a non-negative correct
                    // answer unless the caller opts into AllowNegative, like
                    // category 9 does explicitly).
                    int a = paramRng.Next(0, product);
                    var equation = new EqualityNode(
                        new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(a), new UnknownNode()),
                        new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(b), new ConstantNode(d)));
                    int unknownSolution = product - a; // "a + ? = b * d" solved for "?"
                    return WideRequest(random, equation, new EvaluationResult(unknownSolution), count, paramRng);
                }

                case 9: // AllowNegative, Subtraction with b > a so the correct result is negative
                {
                    int a = paramRng.Next(0, 50);
                    int b = paramRng.Next(a + 1, a + 60);
                    var expression = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(a), new ConstantNode(b));
                    var options = new EvaluationOptions { AllowNegativeResult = true };
                    var correct = Evaluate(expression, options);
                    return new DistractorRequest
                    {
                        Random = random,
                        Expression = expression,
                        CorrectAnswer = correct,
                        Count = count,
                        MinValue = -9999,
                        MaxValue = 9999,
                        AllowNegative = true,
                    };
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "Ismeretlen fuzz-kategória.");
            }
        }

        private EvaluationResult Evaluate(ExpressionNode expression, EvaluationOptions options = null)
        {
            var result = _evaluator.Evaluate(expression, options ?? EvaluationOptions.Default);
            Assert.That(result.IsSuccess, Is.True, "test-fixture expression failed to evaluate: " + expression.ToDisplayString());
            return result.Value;
        }

        /// <summary>
        /// Wide window around the correct answer: 10% of iterations get a
        /// deliberately NARROW window (exercising the DISTRACTOR_EXHAUSTED
        /// path as an accepted outcome, mirroring ExpressionGeneratorFuzzTests'
        /// GENERATION_EXHAUSTED acceptance), the rest get a generous one.
        /// </summary>
        private static DistractorRequest WideRequest(
            IRandomSource random, ExpressionNode expression, EvaluationResult correct, int count, IRandomSource paramRng)
        {
            bool narrow = paramRng.NextDouble() < 0.10;
            int minValue = narrow ? Math.Max(0, correct.Value - 1) : 0;
            int maxValue = narrow ? correct.Value + 1 : correct.Value + 100_000;

            return new DistractorRequest
            {
                Random = random,
                Expression = expression,
                CorrectAnswer = correct,
                Count = count,
                MinValue = minValue,
                MaxValue = maxValue,
                AllowNegative = false,
            };
        }
    }
}
