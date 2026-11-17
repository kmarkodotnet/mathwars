using System;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // 10 000 seedelt feladat invariáns-ellenőrzése az IExpressionEvaluator-ra
    // (math-horde.md 17.1, contract math-engine.md 9. "Tesztelhetőségi
    // szerződés", task F2-03). A ProblemGenerator/ExpressionGenerator még nem
    // létezik (F2-04+ feladatkör), ezért ez a fuzz-teszt magát az
    // EXPRESSION EVALUATORT fuzzolja: mind a 6 BinaryOperator-t, véletlen
    // (de a math-engine.md 3. generálási szabályaihoz hasonlóan felépített,
    // ezért garantáltan kiértékelhető) operandusokkal, néhol SZÁNDÉKOSAN
    // érvénytelen bemenettel (nulla osztó / nem pozitív modulo-osztó) is,
    // hogy a dokumentált hibaágak is lefussanak. Minden iteráció független,
    // determinisztikus IRandomSource-ágat kap (ADR-006) — nincs System.Random.
    [TestFixture]
    public class ExpressionEvaluatorFuzzTests
    {
        private const int IterationCount = 10_000;
        private const int MasterSeed = 931027;

        [Test]
        public void Evaluate_TenThousandSeededExpressions_MatchIndependentComputationAndInvariants()
        {
            var master = new DeterministicRandomSource(MasterSeed);
            var evaluator = new ExpressionEvaluator();

            for (int i = 0; i < IterationCount; i++)
            {
                IRandomSource rng = master.Derive("fuzz-" + i);
                var op = (BinaryOperator)(i % 6);

                switch (op)
                {
                    case BinaryOperator.Addition:
                        RunAdditionCase(evaluator, rng);
                        break;
                    case BinaryOperator.Subtraction:
                        RunSubtractionCase(evaluator, rng);
                        break;
                    case BinaryOperator.Multiplication:
                        RunMultiplicationCase(evaluator, rng);
                        break;
                    case BinaryOperator.Division:
                        RunDivisionCase(evaluator, rng);
                        break;
                    case BinaryOperator.Modulo:
                        RunModuloCase(evaluator, rng);
                        break;
                    case BinaryOperator.IntegerDivisionWithRemainder:
                        RunIntegerDivisionWithRemainderCase(evaluator, rng);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(op), op, "Ismeretlen BinaryOperator a fuzz-tesztben.");
                }
            }
        }

        private static void RunAdditionCase(ExpressionEvaluator evaluator, IRandomSource rng)
        {
            int a = rng.Next(-500, 501);
            int b = rng.Next(-500, 501);
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(a), new ConstantNode(b));

            var result = evaluator.Evaluate(expr, EvaluationOptions.Default);

            AssertScalarRespectsNegativePolicy(result, a + b, EvaluationOptions.Default);
        }

        private static void RunSubtractionCase(ExpressionEvaluator evaluator, IRandomSource rng)
        {
            int a = rng.Next(-500, 501);
            int b = rng.Next(-500, 501);
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(a), new ConstantNode(b));

            var result = evaluator.Evaluate(expr, EvaluationOptions.Default);

            AssertScalarRespectsNegativePolicy(result, a - b, EvaluationOptions.Default);
        }

        private static void RunMultiplicationCase(ExpressionEvaluator evaluator, IRandomSource rng)
        {
            int a = rng.Next(-20, 21);
            int b = rng.Next(-20, 21);
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(a), new ConstantNode(b));

            var result = evaluator.Evaluate(expr, EvaluationOptions.Default);

            AssertScalarRespectsNegativePolicy(result, a * b, EvaluationOptions.Default);
        }

        private static void RunDivisionCase(ExpressionEvaluator evaluator, IRandomSource rng)
        {
            if (rng.Next(0, 10) == 0)
            {
                // 10% of cases: deliberately invalid input — divisor 0.
                int a = rng.Next(-100, 101);
                var expr = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(a), new ConstantNode(0));

                var result = evaluator.Evaluate(expr, EvaluationOptions.Default);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo("DIVISION_BY_ZERO"));
                return;
            }

            // Build from the quotient/divisor outward (math-engine.md 3. rule 2:
            // "soha nem sorsolunk, majd eldobunk") so the division is always exact.
            int divisor = rng.Next(1, 13);
            int quotient = rng.Next(-50, 51);
            int dividend = divisor * quotient;
            var validExpr = new BinaryOperationNode(
                BinaryOperator.Division, new ConstantNode(dividend), new ConstantNode(divisor));

            var validResult = evaluator.Evaluate(validExpr, EvaluationOptions.Default);

            AssertScalarRespectsNegativePolicy(validResult, quotient, EvaluationOptions.Default);
        }

        private static void RunModuloCase(ExpressionEvaluator evaluator, IRandomSource rng)
        {
            if (rng.Next(0, 10) == 0)
            {
                // 10% of cases: deliberately invalid input — non-positive divisor.
                int divisor = rng.Next(-5, 1);
                int a = rng.Next(0, 100);
                var expr = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(a), new ConstantNode(divisor));

                var invalidResult = evaluator.Evaluate(expr, EvaluationOptions.Default);

                Assert.That(invalidResult.IsSuccess, Is.False);
                Assert.That(invalidResult.ErrorCode, Is.EqualTo("MODULO_NON_POSITIVE_DIVISOR"));
                return;
            }

            int validDivisor = rng.Next(1, 13);
            int dividend = rng.Next(-200, 201);
            var validExpr = new BinaryOperationNode(
                BinaryOperator.Modulo, new ConstantNode(dividend), new ConstantNode(validDivisor));

            var result = evaluator.Evaluate(validExpr, EvaluationOptions.Default);

            // Spec: modulo result is always >= 0, so AllowNegativeResult never
            // matters here — it always succeeds, in [0, divisor-1].
            Assert.That(result.IsSuccess, Is.True);
            int value = result.Value.Value;
            Assert.That(value, Is.GreaterThanOrEqualTo(0));
            Assert.That(value, Is.LessThan(validDivisor));
            Assert.That((dividend - value) % validDivisor, Is.EqualTo(0));
        }

        private static void RunIntegerDivisionWithRemainderCase(ExpressionEvaluator evaluator, IRandomSource rng)
        {
            if (rng.Next(0, 10) == 0)
            {
                // 10% of cases: deliberately invalid input — divisor 0.
                int a = rng.Next(-100, 101);
                var expr = new BinaryOperationNode(
                    BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(a), new ConstantNode(0));

                var invalidResult = evaluator.Evaluate(expr, EvaluationOptions.Default);

                Assert.That(invalidResult.IsSuccess, Is.False);
                Assert.That(invalidResult.ErrorCode, Is.EqualTo("DIVISION_BY_ZERO"));
                return;
            }

            int divisor = rng.Next(-13, 13);
            if (divisor == 0)
            {
                divisor = 3;
            }

            int a2 = rng.Next(-300, 301);
            var validExpr = new BinaryOperationNode(
                BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(a2), new ConstantNode(divisor));
            var allowNegativeOptions = new EvaluationOptions { AllowNegativeIntermediate = true, AllowNegativeResult = true };

            var result = evaluator.Evaluate(validExpr, allowNegativeOptions);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Remainder.HasValue, Is.True);
            int remainder = result.Value.Remainder.Value;
            int quotient = result.Value.Value;
            Assert.That(remainder, Is.GreaterThanOrEqualTo(0));
            Assert.That(remainder, Is.LessThan(Math.Abs(divisor)));
            Assert.That(quotient * divisor + remainder, Is.EqualTo(a2));
        }

        private static void AssertScalarRespectsNegativePolicy(
            Result<EvaluationResult> result, int expected, EvaluationOptions options)
        {
            bool withinRange = expected >= options.MinValue && expected <= options.MaxValue;
            bool negativeOk = expected >= 0 || options.AllowNegativeResult;

            if (withinRange && negativeOk)
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value.Value, Is.EqualTo(expected));
                Assert.That(result.Value.Remainder, Is.Null);
            }
            else if (!withinRange)
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo("OVERFLOW"));
            }
            else
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo("NEGATIVE_RESULT_NOT_ALLOWED"));
            }
        }
    }
}
