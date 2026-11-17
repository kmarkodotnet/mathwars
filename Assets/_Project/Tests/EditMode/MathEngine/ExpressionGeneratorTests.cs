using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // Targeted scenarios for IExpressionGenerator/ExpressionGenerator (contract
    // math-engine.md 3., task F2-05): construction-based validity for Division/
    // Modulo, parenthesis handling, unknown insertion, determinism, and the
    // documented precondition/error paths. The 10 000-seeded invariant sweep
    // lives in ExpressionGeneratorFuzzTests.cs.
    [TestFixture]
    public class ExpressionGeneratorTests
    {
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

        private static ExpressionGenerationRequest BuildRequest(
            int seed,
            IReadOnlyCollection<BinaryOperator> allowedOperators,
            int minOperandCount = 2,
            int maxOperandCount = 2,
            int minOperandValue = 1,
            int maxOperandValue = 20,
            int maxResultValue = 100_000,
            bool allowParentheses = false,
            int maxParenthesisDepth = 0,
            bool requireParentheses = false,
            bool includeUnknown = false,
            UnknownPlacement unknownPlacement = UnknownPlacement.None,
            int maxAttempts = 64)
        {
            return new ExpressionGenerationRequest
            {
                Random = new DeterministicRandomSource(seed),
                AllowedOperators = allowedOperators,
                MinOperandValue = minOperandValue,
                MaxOperandValue = maxOperandValue,
                MaxResultValue = maxResultValue,
                MinOperandCount = minOperandCount,
                MaxOperandCount = maxOperandCount,
                AllowParentheses = allowParentheses,
                MaxParenthesisDepth = maxParenthesisDepth,
                RequireParentheses = requireParentheses,
                IncludeUnknown = includeUnknown,
                UnknownPlacement = unknownPlacement,
                MaxAttempts = maxAttempts,
            };
        }

        // --- Basic success / evaluability --------------------------------

        [Test]
        public void Generate_SimpleAddition_ProducesValidEvaluableTree()
        {
            var request = BuildRequest(seed: 1, allowedOperators: new[] { BinaryOperator.Addition });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var generated = result.Value;
            Assert.That(generated.Root, Is.Not.Null);
            Assert.That(_evaluator.CountUnknowns(generated.Root), Is.EqualTo(0));

            var eval = _evaluator.Evaluate(generated.Root, EvaluationOptions.Default);
            Assert.That(eval.IsSuccess, Is.True);
            Assert.That(eval.Value.Value, Is.EqualTo(generated.Value.Value));
            Assert.That(generated.AttemptsUsed, Is.GreaterThanOrEqualTo(1));
        }

        // --- Construction-based validity (rule 2/3) ----------------------

        [Test]
        public void Generate_DivisionOnly_IsAlwaysEvenlyDivisibleByConstruction()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var request = BuildRequest(seed, new[] { BinaryOperator.Division }, minOperandValue: 1, maxOperandValue: 15);

                var result = _generator.Generate(request);

                Assert.That(result.IsSuccess, Is.True);
                var binary = (BinaryOperationNode)result.Value.Root;
                Assert.That(binary.Operator, Is.EqualTo(BinaryOperator.Division));
                int dividend = ((ConstantNode)binary.Left).Value;
                int divisor = ((ConstantNode)binary.Right).Value;
                Assert.That(divisor, Is.Not.EqualTo(0));
                Assert.That(dividend % divisor, Is.EqualTo(0));
            }
        }

        [Test]
        public void Generate_ModuloOnly_ResultAlwaysNonNegative()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var request = BuildRequest(seed, new[] { BinaryOperator.Modulo }, minOperandValue: 1, maxOperandValue: 15);

                var result = _generator.Generate(request);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value.Value.Value, Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public void Generate_IntegerDivisionWithRemainder_OnlyAtRoot_AndRemainderPresent()
        {
            var request = BuildRequest(seed: 7, allowedOperators: new[] { BinaryOperator.IntegerDivisionWithRemainder }, minOperandValue: 1, maxOperandValue: 12);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Root, Is.InstanceOf<BinaryOperationNode>());
            var binary = (BinaryOperationNode)result.Value.Root;
            Assert.That(binary.Operator, Is.EqualTo(BinaryOperator.IntegerDivisionWithRemainder));
            Assert.That(result.Value.Value.Remainder.HasValue, Is.True);
            Assert.That(result.Value.Value.Remainder.Value, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Generate_DivisionAfterAddition_FollowingJoinDividesFixedValue()
        {
            // Exercises the "divisor chosen FROM the actual divisors of the
            // already-fixed left value" path (design note 1), not the
            // fresh-seed path.
            for (int seed = 0; seed < 50; seed++)
            {
                var request = BuildRequest(
                    seed,
                    new[] { BinaryOperator.Addition, BinaryOperator.Division },
                    minOperandCount: 3,
                    maxOperandCount: 4,
                    minOperandValue: 1,
                    maxOperandValue: 15,
                    maxAttempts: 200);

                var result = _generator.Generate(request);

                if (!result.IsSuccess)
                {
                    Assert.That(result.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
                    continue;
                }

                var eval = _evaluator.Evaluate(result.Value.Root, EvaluationOptions.Default);
                Assert.That(eval.IsSuccess, Is.True);
            }
        }

        // --- Parentheses (rule 4/5) ---------------------------------------

        [Test]
        public void Generate_RequireParentheses_ProducesForcedParenthesesWithinDepth()
        {
            var request = BuildRequest(
                seed: 3,
                allowedOperators: new[] { BinaryOperator.Addition, BinaryOperator.Multiplication },
                minOperandCount: 3,
                maxOperandCount: 4,
                allowParentheses: true,
                maxParenthesisDepth: 1,
                requireParentheses: true,
                maxAttempts: 200);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            int depth = _evaluator.MeasureParenthesisDepth(result.Value.Root);
            Assert.That(depth, Is.GreaterThanOrEqualTo(1));
            Assert.That(depth, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void Generate_AllowParenthesesFalse_NeverProducesParentheses()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var request = BuildRequest(
                    seed,
                    new[] { BinaryOperator.Addition, BinaryOperator.Multiplication, BinaryOperator.Subtraction },
                    minOperandCount: 2,
                    maxOperandCount: 5,
                    allowParentheses: false);

                var result = _generator.Generate(request);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(_evaluator.MeasureParenthesisDepth(result.Value.Root), Is.EqualTo(0));
            }
        }

        [Test]
        public void Generate_MaxParenthesisDepthTwo_NeverExceedsRequestedDepth()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var request = BuildRequest(
                    seed,
                    new[] { BinaryOperator.Addition, BinaryOperator.Multiplication, BinaryOperator.Subtraction },
                    minOperandCount: 4,
                    maxOperandCount: 5,
                    allowParentheses: true,
                    maxParenthesisDepth: 2);

                var result = _generator.Generate(request);

                Assert.That(result.IsSuccess, Is.True);
                Assert.That(_evaluator.MeasureParenthesisDepth(result.Value.Root), Is.LessThanOrEqualTo(2));
            }
        }

        // --- Unknown insertion (rule 6) ------------------------------------

        [Test]
        public void Generate_IncludeUnknownLeftOperand_ProducesSolvableEquation()
        {
            var request = BuildRequest(
                seed: 11,
                allowedOperators: new[] { BinaryOperator.Addition, BinaryOperator.Subtraction },
                minOperandCount: 2,
                maxOperandCount: 3,
                includeUnknown: true,
                unknownPlacement: UnknownPlacement.LeftOperand);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var generated = result.Value;
            Assert.That(generated.Root, Is.InstanceOf<EqualityNode>());
            var equation = (EqualityNode)generated.Root;
            Assert.That(_evaluator.CountUnknowns(equation), Is.EqualTo(1));
            Assert.That(generated.UnknownSolution.HasValue, Is.True);

            var check = _evaluator.CheckEquality(equation, generated.UnknownSolution.Value, EvaluationOptions.Default);
            Assert.That(check.IsSuccess, Is.True);
            Assert.That(check.Value, Is.True);
        }

        [Test]
        public void Generate_IncludeUnknownRightOperand_ProducesSolvableEquation()
        {
            var request = BuildRequest(
                seed: 23,
                allowedOperators: new[] { BinaryOperator.Addition, BinaryOperator.Multiplication },
                minOperandCount: 2,
                maxOperandCount: 3,
                includeUnknown: true,
                unknownPlacement: UnknownPlacement.RightOperand);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var generated = result.Value;
            Assert.That(generated.Root, Is.InstanceOf<EqualityNode>());
            var equation = (EqualityNode)generated.Root;
            Assert.That(_evaluator.CountUnknowns(equation), Is.EqualTo(1));

            var check = _evaluator.CheckEquality(equation, generated.UnknownSolution.Value, EvaluationOptions.Default);
            Assert.That(check.IsSuccess, Is.True);
            Assert.That(check.Value, Is.True);
        }

        [Test]
        public void Generate_IncludeUnknownEitherSide_ProducesExactlyOneUnknown()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var request = BuildRequest(
                    seed,
                    new[] { BinaryOperator.Addition, BinaryOperator.Division },
                    minOperandCount: 2,
                    maxOperandCount: 3,
                    includeUnknown: true,
                    unknownPlacement: UnknownPlacement.EitherSide,
                    maxAttempts: 200);

                var result = _generator.Generate(request);

                if (!result.IsSuccess)
                {
                    Assert.That(result.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
                    continue;
                }

                var equation = (EqualityNode)result.Value.Root;
                Assert.That(_evaluator.CountUnknowns(equation), Is.EqualTo(1));
            }
        }

        // --- Determinism (P2) ---------------------------------------------

        [Test]
        public void Generate_SameSeedAndRequest_IsBitwiseDeterministic()
        {
            var allowed = new[] { BinaryOperator.Addition, BinaryOperator.Multiplication, BinaryOperator.Subtraction };

            var requestA = BuildRequest(seed: 99, allowedOperators: allowed, minOperandCount: 2, maxOperandCount: 4,
                allowParentheses: true, maxParenthesisDepth: 1);
            var requestB = BuildRequest(seed: 99, allowedOperators: allowed, minOperandCount: 2, maxOperandCount: 4,
                allowParentheses: true, maxParenthesisDepth: 1);

            var resultA = _generator.Generate(requestA);
            var resultB = _generator.Generate(requestB);

            Assert.That(resultA.IsSuccess, Is.True);
            Assert.That(resultB.IsSuccess, Is.True);
            Assert.That(resultA.Value.Root.ToDisplayString(), Is.EqualTo(resultB.Value.Root.ToDisplayString()));
            Assert.That(resultA.Value.Value.Value, Is.EqualTo(resultB.Value.Value.Value));
            Assert.That(resultA.Value.AttemptsUsed, Is.EqualTo(resultB.Value.AttemptsUsed));
        }

        // --- MaxResultValue (rule 8) ---------------------------------------

        [Test]
        public void Generate_RespectsMaxResultValue()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var request = BuildRequest(
                    seed,
                    new[] { BinaryOperator.Addition, BinaryOperator.Multiplication },
                    minOperandCount: 2,
                    maxOperandCount: 4,
                    minOperandValue: 1,
                    maxOperandValue: 20,
                    maxResultValue: 50,
                    maxAttempts: 300);

                var result = _generator.Generate(request);

                if (!result.IsSuccess)
                {
                    Assert.That(result.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
                    continue;
                }

                Assert.That(result.Value.Value.Value, Is.LessThanOrEqualTo(50));
            }
        }

        // --- GENERATION_EXHAUSTED -------------------------------------------

        [Test]
        public void Generate_UnsatisfiableMaxResultValue_ReturnsGenerationExhausted()
        {
            // Every Addition of two positive operands exceeds a MaxResultValue
            // of 0 — the generator must exhaust rather than loop forever or throw.
            var request = BuildRequest(
                seed: 5,
                allowedOperators: new[] { BinaryOperator.Addition },
                minOperandValue: 1,
                maxOperandValue: 10,
                maxResultValue: 0,
                maxAttempts: 5);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("GENERATION_EXHAUSTED"));
        }

        // --- Precondition / contract-violation error paths -------------------

        [Test]
        public void Generate_NullRequest_Throws()
        {
            Assert.That(() => _generator.Generate(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Generate_NullRandom_Throws()
        {
            var request = new ExpressionGenerationRequest
            {
                Random = null,
                AllowedOperators = new[] { BinaryOperator.Addition },
                MinOperandValue = 1,
                MaxOperandValue = 10,
                MaxResultValue = 100,
                MinOperandCount = 2,
                MaxOperandCount = 2,
            };

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_EmptyAllowedOperators_Throws()
        {
            var request = BuildRequest(seed: 1, allowedOperators: Array.Empty<BinaryOperator>());

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_MinOperandCountBelowTwo_Throws()
        {
            var request = BuildRequest(seed: 1, allowedOperators: new[] { BinaryOperator.Addition }, minOperandCount: 1, maxOperandCount: 2);

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_MaxOperandCountAboveFive_Throws()
        {
            var request = BuildRequest(seed: 1, allowedOperators: new[] { BinaryOperator.Addition }, minOperandCount: 2, maxOperandCount: 6);

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_MaxOperandValueNotGreaterThanMin_Throws()
        {
            var request = BuildRequest(seed: 1, allowedOperators: new[] { BinaryOperator.Addition }, minOperandValue: 10, maxOperandValue: 10);

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_RequireParenthesesWithoutAllowParentheses_Throws()
        {
            var request = BuildRequest(seed: 1, allowedOperators: new[] { BinaryOperator.Addition },
                allowParentheses: false, requireParentheses: true);

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_IncludeUnknownWithPlacementNone_Throws()
        {
            var request = BuildRequest(seed: 1, allowedOperators: new[] { BinaryOperator.Addition },
                includeUnknown: true, unknownPlacement: UnknownPlacement.None);

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_MaxAttemptsNotPositive_Throws()
        {
            var request = BuildRequest(seed: 1, allowedOperators: new[] { BinaryOperator.Addition }, maxAttempts: 0);

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_NullEvaluator_Throws()
        {
            Assert.That(() => new ExpressionGenerator(null, _solver), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NullSolver_Throws()
        {
            Assert.That(() => new ExpressionGenerator(_evaluator, null), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
