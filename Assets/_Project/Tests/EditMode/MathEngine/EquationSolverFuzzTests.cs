using System;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // 10 000 seedelt feladat invariáns-ellenőrzése az IEquationSolver-ra
    // (math-horde.md 17.1, contract math-engine.md 9. "Tesztelhetőségi
    // szerződés", task F2-04) — az IExpressionGenerator még nem létezik
    // (F2-05+ feladatkör), ezért ez a fuzz-teszt magát az EQUATION SOLVERT
    // fuzzolja: lineáris inverz-láncokat épít (Addition/Subtraction/
    // Multiplication, véletlen mélység és véletlen ismeretlen-oldal minden
    // lépésnél), valamint minden kötelezően kizárt/különleges alakot
    // (Division/Modulo osztó pozícióban, Modulo osztandó pozícióban,
    // szorzás nullával, NO_UNKNOWN/MULTIPLE_UNKNOWNS). Minden iteráció
    // független, determinisztikus IRandomSource-ágat kap (ADR-006) — nincs
    // System.Random.
    [TestFixture]
    public class EquationSolverFuzzTests
    {
        private const int IterationCount = 10_000;
        private const int MasterSeed = 240417;
        private const int CategoryCount = 8;

        // Wide-open options: this fuzz test's job is to verify the inverse-chain
        // ALGEBRA against an independently tracked ground truth, not to
        // re-exercise EvaluationOptions' sign/range policy (already covered by
        // ExpressionEvaluatorFuzzTests) — so negative intermediates/results are
        // allowed and the range is generous.
        private static readonly EvaluationOptions PermissiveOptions = new EvaluationOptions
        {
            AllowNegativeIntermediate = true,
            AllowNegativeResult = true,
            MinValue = -1_000_000,
            MaxValue = 1_000_000,
        };

        [Test]
        public void Solve_TenThousandSeededEquations_MatchIndependentGroundTruthAndInvariants()
        {
            var master = new DeterministicRandomSource(MasterSeed);
            var solver = new EquationSolver(new ExpressionEvaluator());

            for (int i = 0; i < IterationCount; i++)
            {
                IRandomSource rng = master.Derive("fuzz-" + i);
                int category = i % CategoryCount;

                switch (category)
                {
                    case 0:
                        RunLinearChainCase(solver, rng, depth: rng.Next(1, 5), includeMultiplication: false);
                        break;
                    case 1:
                        RunLinearChainCase(solver, rng, depth: rng.Next(1, 4), includeMultiplication: true);
                        break;
                    case 2:
                        RunDivisionDividendCase(solver, rng);
                        break;
                    case 3:
                        RunDivisionDivisorUnsupportedCase(solver, rng);
                        break;
                    case 4:
                        RunModuloDivisorUnsupportedCase(solver, rng);
                        break;
                    case 5:
                        RunModuloDividendNotUniqueCase(solver, rng);
                        break;
                    case 6:
                        RunMultiplicationByZeroCase(solver, rng);
                        break;
                    case 7:
                        RunUnknownCountMismatchCase(solver, rng);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(category), category, "Ismeretlen fuzz-kategória.");
                }
            }
        }

        // --- Kategória 0/1: lineáris inverz-lánc (Addition/Subtraction/Multiplication) ---

        private static void RunLinearChainCase(EquationSolver solver, IRandomSource rng, int depth, bool includeMultiplication)
        {
            int unknown = rng.Next(-30, 31);
            ExpressionNode node = new UnknownNode();
            int currentValue = unknown;

            for (int step = 0; step < depth; step++)
            {
                int opCount = includeMultiplication ? 3 : 2;
                int opChoice = rng.Next(0, opCount);
                bool knownOnRight = rng.Next(0, 2) == 0;

                if (opChoice == 0)
                {
                    int c = rng.Next(-30, 31);
                    node = knownOnRight
                        ? new BinaryOperationNode(BinaryOperator.Addition, node, new ConstantNode(c))
                        : new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(c), node);
                    currentValue += c;
                }
                else if (opChoice == 1)
                {
                    int c = rng.Next(-30, 31);
                    if (knownOnRight)
                    {
                        node = new BinaryOperationNode(BinaryOperator.Subtraction, node, new ConstantNode(c));
                        currentValue -= c;
                    }
                    else
                    {
                        node = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(c), node);
                        currentValue = c - currentValue;
                    }
                }
                else
                {
                    int magnitude = rng.Next(1, 7);
                    int c = rng.Next(0, 2) == 0 ? magnitude : -magnitude;
                    node = knownOnRight
                        ? new BinaryOperationNode(BinaryOperator.Multiplication, node, new ConstantNode(c))
                        : new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(c), node);
                    currentValue *= c;
                }
            }

            // Exercise SolveForTarget directly (MissingOperand shape).
            var targetResult = solver.SolveForTarget(node, currentValue, PermissiveOptions);
            Assert.That(targetResult.IsSuccess, Is.True);
            Assert.That(targetResult.Value, Is.EqualTo(unknown));

            // Also exercise Solve via a full EqualityNode, randomly placed on
            // either side, to cover both branches of Solve's side-selection.
            var equation = rng.Next(0, 2) == 0
                ? new EqualityNode(node, new ConstantNode(currentValue))
                : new EqualityNode(new ConstantNode(currentValue), node);

            var equationResult = solver.Solve(equation, PermissiveOptions);
            Assert.That(equationResult.IsSuccess, Is.True);
            Assert.That(equationResult.Value, Is.EqualTo(unknown));
        }

        // --- Kategória 2: unknown a Division osztandó (dividend) pozíciójában ---

        private static void RunDivisionDividendCase(EquationSolver solver, IRandomSource rng)
        {
            int divisor = rng.Next(1, 13);
            int quotient = rng.Next(-30, 31);
            int dividend = divisor * quotient; // felépítésből biztosított oszthatóság

            var expr = new BinaryOperationNode(BinaryOperator.Division, new UnknownNode(), new ConstantNode(divisor));
            var equation = new EqualityNode(expr, new ConstantNode(quotient));

            var result = solver.Solve(equation, PermissiveOptions);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(dividend));
        }

        // --- Kategória 3: unknown a Division osztó pozíciójában -> UNSUPPORTED_SHAPE ---

        private static void RunDivisionDivisorUnsupportedCase(EquationSolver solver, IRandomSource rng)
        {
            int dividend = rng.Next(1, 100);
            int quotient = rng.Next(1, 20);

            var expr = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(dividend), new UnknownNode());
            var equation = new EqualityNode(expr, new ConstantNode(quotient));

            var result = solver.Solve(equation, PermissiveOptions);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_SHAPE"));
        }

        // --- Kategória 4: unknown a Modulo osztó (jobb) pozíciójában -> UNSUPPORTED_SHAPE ---

        private static void RunModuloDivisorUnsupportedCase(EquationSolver solver, IRandomSource rng)
        {
            int dividend = rng.Next(0, 100);
            int remainder = rng.Next(0, 20);

            var expr = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(dividend), new UnknownNode());
            var equation = new EqualityNode(expr, new ConstantNode(remainder));

            var result = solver.Solve(equation, PermissiveOptions);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_SHAPE"));
        }

        // --- Kategória 5: unknown a Modulo osztandó (bal) pozíciójában -> NOT_UNIQUE,
        //     plusz a HasUniqueIntegerSolution sávos ellenőrzésének fuzzolása ---

        private static void RunModuloDividendNotUniqueCase(EquationSolver solver, IRandomSource rng)
        {
            int b = rng.Next(2, 13);
            int r = rng.Next(0, b);

            var expr = new BinaryOperationNode(BinaryOperator.Modulo, new UnknownNode(), new ConstantNode(b));
            var equation = new EqualityNode(expr, new ConstantNode(r));

            var result = solver.Solve(equation, PermissiveOptions);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_UNIQUE"));

            int k0 = rng.Next(-3, 4);
            int aSolution = r + (k0 * b);

            bool exactlyOneInRange = solver.HasUniqueIntegerSolution(equation, aSolution, aSolution, PermissiveOptions);
            Assert.That(exactlyOneInRange, Is.True);

            bool twoInRange = solver.HasUniqueIntegerSolution(equation, aSolution, aSolution + b, PermissiveOptions);
            Assert.That(twoInRange, Is.False);
        }

        // --- Kategória 6: szorzás nullával -> NOT_UNIQUE / NO_INTEGER_SOLUTION,
        //     plusz a HasUniqueIntegerSolution sávos ellenőrzésének fuzzolása ---

        private static void RunMultiplicationByZeroCase(EquationSolver solver, IRandomSource rng)
        {
            bool zeroTarget = rng.Next(0, 2) == 0;
            int target = zeroTarget ? 0 : rng.Next(1, 50);

            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(0));
            var equation = new EqualityNode(expr, new ConstantNode(target));

            var result = solver.Solve(equation, PermissiveOptions);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(zeroTarget ? "NOT_UNIQUE" : "NO_INTEGER_SOLUTION"));

            int v = rng.Next(-1000, 1001);
            if (zeroTarget)
            {
                Assert.That(solver.HasUniqueIntegerSolution(equation, v, v, PermissiveOptions), Is.True);
                Assert.That(solver.HasUniqueIntegerSolution(equation, v, v + 1, PermissiveOptions), Is.False);
            }
            else
            {
                Assert.That(solver.HasUniqueIntegerSolution(equation, v, v + rng.Next(0, 5), PermissiveOptions), Is.False);
            }
        }

        // --- Kategória 7: NO_UNKNOWN / MULTIPLE_UNKNOWNS ---

        private static void RunUnknownCountMismatchCase(EquationSolver solver, IRandomSource rng)
        {
            if (rng.Next(0, 2) == 0)
            {
                int a = rng.Next(-100, 101);
                int b = rng.Next(-100, 101);
                var equation = new EqualityNode(new ConstantNode(a), new ConstantNode(b));

                var result = solver.Solve(equation, PermissiveOptions);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo("NO_UNKNOWN"));
            }
            else
            {
                int a = rng.Next(-100, 101);
                int b = rng.Next(-100, 101);
                var left = new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(a));
                var right = new BinaryOperationNode(BinaryOperator.Subtraction, new UnknownNode(), new ConstantNode(b));
                var equation = new EqualityNode(left, right);

                var result = solver.Solve(equation, PermissiveOptions);

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo("MULTIPLE_UNKNOWNS"));
            }
        }
    }
}
