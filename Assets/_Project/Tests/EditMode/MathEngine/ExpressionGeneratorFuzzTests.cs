using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // 10 000 seedelt feladat invariáns-ellenőrzése az IExpressionGenerator-ra
    // (math-horde.md 17.1, contract math-engine.md 9. "Tesztelhetőségi
    // szerződés", task F2-05). Minden iteráció egy, a master-seedből
    // determinisztikusan levezetett IRandomSource-ágat kap (ADR-006) — nincs
    // System.Random. A generálásra átadott Random MINDIG egy FRISSEN
    // konstruált DeterministicRandomSource ugyanazzal a (levezetett) seeddel,
    // soha nem az iteráció paraméter-választásához használt rng maga — így a
    // teszt-paraméterezés és a generátor saját determinizmusa nem
    // keveredik össze.
    [TestFixture]
    public class ExpressionGeneratorFuzzTests
    {
        private const int IterationCount = 10_000;
        private const int MasterSeed = 250511;
        private const int CategoryCount = 11;

        private IExpressionEvaluator _evaluator;
        private IEquationSolver _solver;
        private ExpressionGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new ExpressionEvaluator();
            _solver = new EquationSolver(_evaluator);
            _generator = new ExpressionGenerator(_evaluator, _solver);
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
                ExpressionGenerationRequest request = BuildRequestForCategory(category, paramRng, genSeed);

                var result = _generator.Generate(request);

                if (!result.IsSuccess)
                {
                    Assert.That(result.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"),
                        "category " + category + ", iteration " + i);
                    continue;
                }

                VerifyCommonInvariants(request, result.Value, category, i);

                // Determinism (P2): regenerating with a FRESH source carrying
                // the SAME seed must reproduce the exact same expression.
                var repeatRequest = BuildRequestForCategory(category, paramRng: BuildReplayRng(category, i, master), genSeed);
                var repeatResult = _generator.Generate(repeatRequest);
                Assert.That(repeatResult.IsSuccess, Is.True, "iteration " + i + " (replay)");
                Assert.That(repeatResult.Value.Root.ToDisplayString(), Is.EqualTo(result.Value.Root.ToDisplayString()),
                    "iteration " + i + " (replay determinism)");
                Assert.That(repeatResult.Value.Value.Value, Is.EqualTo(result.Value.Value.Value),
                    "iteration " + i + " (replay determinism)");
            }
        }

        /// <summary>
        /// Rebuilds the exact same param-selection sequence a second time
        /// (fresh "params-i" derivation) so the request's non-Random fields
        /// are bitwise identical to the first pass — only genSeed changes
        /// between construction calls, and we pass the SAME genSeed back in.
        /// </summary>
        private static IRandomSource BuildReplayRng(int category, int iteration, DeterministicRandomSource master) =>
            master.Derive("params-" + iteration);

        private static ExpressionGenerationRequest BuildRequestForCategory(int category, IRandomSource paramRng, int genSeed)
        {
            var random = new DeterministicRandomSource(genSeed);

            switch (category)
            {
                case 0: // Flat Addition/Subtraction chain
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Subtraction },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 50),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = paramRng.Next(2, 6),
                        MaxAttempts = 200,
                    };

                case 1: // Addition + Multiplication (precedence mix)
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Multiplication },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 20),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = paramRng.Next(2, 6),
                        MaxAttempts = 200,
                    };

                case 2: // Division-only, single join (fresh-seed construction path)
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Division },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 50),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = 2,
                        MaxAttempts = 200,
                    };

                case 3: // Modulo-only, single join
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Modulo },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 50),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = 2,
                        MaxAttempts = 200,
                    };

                case 4: // IntegerDivisionWithRemainder-only, single join (root-only operator)
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.IntegerDivisionWithRemainder },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 50),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = 2,
                        MaxAttempts = 200,
                    };

                case 5: // Addition + Division, exercising the "divides the already-fixed value" follow-up join
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Division },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 20),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 3,
                        MaxOperandCount = 4,
                        MaxAttempts = 300,
                    };

                case 6: // Required parentheses, depth 1
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Subtraction, BinaryOperator.Multiplication },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 20),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 3,
                        MaxOperandCount = 4,
                        AllowParentheses = true,
                        MaxParenthesisDepth = 1,
                        RequireParentheses = true,
                        MaxAttempts = 300,
                    };

                case 7: // Optional parentheses up to depth 2
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Subtraction, BinaryOperator.Multiplication },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 20),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 4,
                        MaxOperandCount = 5,
                        AllowParentheses = true,
                        MaxParenthesisDepth = 2,
                        MaxAttempts = 300,
                    };

                case 8: // IncludeUnknown, LeftOperand
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Subtraction },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 30),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = paramRng.Next(2, 5),
                        IncludeUnknown = true,
                        UnknownPlacement = UnknownPlacement.LeftOperand,
                        MaxAttempts = 300,
                    };

                case 9: // IncludeUnknown, RightOperand
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Multiplication },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 30),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = paramRng.Next(2, 5),
                        IncludeUnknown = true,
                        UnknownPlacement = UnknownPlacement.RightOperand,
                        MaxAttempts = 300,
                    };

                case 10: // IncludeUnknown, EitherSide, Division included (structurally infeasible placements must retry, not throw)
                    return new ExpressionGenerationRequest
                    {
                        Random = random,
                        AllowedOperators = new[] { BinaryOperator.Addition, BinaryOperator.Division },
                        MinOperandValue = 1,
                        MaxOperandValue = paramRng.Next(5, 20),
                        MaxResultValue = 1_000_000,
                        MinOperandCount = 2,
                        MaxOperandCount = 3,
                        IncludeUnknown = true,
                        UnknownPlacement = UnknownPlacement.EitherSide,
                        MaxAttempts = 300,
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "Ismeretlen fuzz-kategória.");
            }
        }

        private void VerifyCommonInvariants(ExpressionGenerationRequest request, GeneratedExpression generated, int category, int iteration)
        {
            string ctx = "category " + category + ", iteration " + iteration;

            Assert.That(generated.Root, Is.Not.Null, ctx);
            Assert.That(generated.AttemptsUsed, Is.GreaterThanOrEqualTo(1), ctx);
            Assert.That(generated.AttemptsUsed, Is.LessThanOrEqualTo(request.MaxAttempts), ctx);
            Assert.That(generated.Value.Value, Is.LessThanOrEqualTo(request.MaxResultValue), ctx);

            // Only AllowedOperators ever appear in the tree.
            var usedOperators = new List<BinaryOperator>();
            CollectOperators(generated.Root, usedOperators);
            foreach (var op in usedOperators)
            {
                Assert.That(Contains(request.AllowedOperators, op), Is.True, ctx + " used operator " + op);
            }

            int depth = _evaluator.MeasureParenthesisDepth(generated.Root);
            if (!request.AllowParentheses)
            {
                Assert.That(depth, Is.EqualTo(0), ctx);
            }
            else
            {
                Assert.That(depth, Is.LessThanOrEqualTo(request.MaxParenthesisDepth), ctx);
                if (request.RequireParentheses)
                {
                    Assert.That(depth, Is.GreaterThanOrEqualTo(1), ctx);
                }
            }

            if (!request.IncludeUnknown)
            {
                Assert.That(generated.Root, Is.Not.InstanceOf<EqualityNode>(), ctx);
                Assert.That(_evaluator.CountUnknowns(generated.Root), Is.EqualTo(0), ctx);
                Assert.That(generated.UnknownSolution.HasValue, Is.False, ctx);

                var eval = _evaluator.Evaluate(generated.Root, EvaluationOptions.Default);
                Assert.That(eval.IsSuccess, Is.True, ctx);
                Assert.That(eval.Value.Value, Is.EqualTo(generated.Value.Value), ctx);
                Assert.That(eval.Value.Remainder, Is.EqualTo(generated.Value.Remainder), ctx);
            }
            else
            {
                Assert.That(generated.Root, Is.InstanceOf<EqualityNode>(), ctx);
                var equation = (EqualityNode)generated.Root;
                Assert.That(_evaluator.CountUnknowns(equation), Is.EqualTo(1), ctx);
                Assert.That(generated.UnknownSolution.HasValue, Is.True, ctx);

                var check = _evaluator.CheckEquality(equation, generated.UnknownSolution.Value, EvaluationOptions.Default);
                Assert.That(check.IsSuccess, Is.True, ctx);
                Assert.That(check.Value, Is.True, ctx);

                if (request.UnknownPlacement != UnknownPlacement.EitherSide)
                {
                    VerifyUnknownPlacement(equation, request.UnknownPlacement, ctx);
                }
            }
        }

        private static void VerifyUnknownPlacement(EqualityNode equation, UnknownPlacement placement, string ctx)
        {
            ExpressionNode sideWithUnknown = ContainsUnknown(equation.Left) ? equation.Left : equation.Right;
            int leafCount = CountLeaves(sideWithUnknown);
            int unknownIndex = FindUnknownIndex(sideWithUnknown, new int[] { 0 });

            if (placement == UnknownPlacement.LeftOperand)
            {
                Assert.That(unknownIndex, Is.EqualTo(0), ctx);
            }
            else if (placement == UnknownPlacement.RightOperand)
            {
                Assert.That(unknownIndex, Is.EqualTo(leafCount - 1), ctx);
            }
        }

        private static bool ContainsUnknown(ExpressionNode node)
        {
            switch (node)
            {
                case UnknownNode:
                    return true;
                case ConstantNode:
                    return false;
                case BinaryOperationNode binary:
                    return ContainsUnknown(binary.Left) || ContainsUnknown(binary.Right);
                default:
                    return false;
            }
        }

        private static int CountLeaves(ExpressionNode node)
        {
            switch (node)
            {
                case UnknownNode:
                case ConstantNode:
                    return 1;
                case BinaryOperationNode binary:
                    return CountLeaves(binary.Left) + CountLeaves(binary.Right);
                default:
                    return 0;
            }
        }

        private static int FindUnknownIndex(ExpressionNode node, int[] counter)
        {
            switch (node)
            {
                case UnknownNode:
                    return counter[0];
                case ConstantNode:
                    counter[0]++;
                    return -1;
                case BinaryOperationNode binary:
                    int left = FindUnknownIndex(binary.Left, counter);
                    if (left >= 0)
                    {
                        return left;
                    }

                    return FindUnknownIndex(binary.Right, counter);
                default:
                    return -1;
            }
        }

        private static void CollectOperators(ExpressionNode node, List<BinaryOperator> into)
        {
            switch (node)
            {
                case ConstantNode:
                case UnknownNode:
                    return;
                case BinaryOperationNode binary:
                    into.Add(binary.Operator);
                    CollectOperators(binary.Left, into);
                    CollectOperators(binary.Right, into);
                    return;
                case EqualityNode equality:
                    CollectOperators(equality.Left, into);
                    CollectOperators(equality.Right, into);
                    return;
            }
        }

        private static bool Contains(IReadOnlyCollection<BinaryOperator> collection, BinaryOperator value)
        {
            foreach (var item in collection)
            {
                if (item == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
