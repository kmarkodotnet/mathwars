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
    // Targeted scenarios for IDistractorGenerator/DistractorGenerator
    // (contract math-engine.md 4., spec 6.4, task F2-07): one happy-path per
    // error model, the correct±k fallback, the cross-cutting invariants
    // (rule 1/2: distinct from each other and from the correct answer), and
    // the documented precondition/error paths. The 10 000-seeded invariant
    // sweep lives in DistractorGeneratorFuzzTests.cs.
    [TestFixture]
    public class DistractorGeneratorTests
    {
        private DistractorGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            _generator = new DistractorGenerator();
        }

        private static DistractorRequest BuildRequest(
            int seed,
            ExpressionNode expression,
            EvaluationResult correctAnswer,
            int count = 3,
            IReadOnlyCollection<ErrorCategory> preferredModels = null,
            int minValue = -9999,
            int maxValue = 99999,
            bool allowNegative = false)
        {
            return new DistractorRequest
            {
                Random = new DeterministicRandomSource(seed),
                Expression = expression,
                CorrectAnswer = correctAnswer,
                Count = count,
                PreferredModels = preferredModels ?? Array.Empty<ErrorCategory>(),
                MinValue = minValue,
                MaxValue = maxValue,
                AllowNegative = allowNegative,
            };
        }

        private static BinaryOperationNode Bin(BinaryOperator op, int a, int b, bool force = false) =>
            new BinaryOperationNode(op, new ConstantNode(a), new ConstantNode(b), force);

        // --- Cross-cutting invariants (spec 6.4/2-3) -------------------------

        [Test]
        public void Generate_SevenTimesEight_ProducesThreeDistinctNonCorrectDistractors()
        {
            var expression = Bin(BinaryOperator.Multiplication, 7, 8);
            var request = BuildRequest(seed: 1, expression, new EvaluationResult(56), count: 3);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractors = result.Value;
            Assert.That(distractors.Count, Is.EqualTo(3));

            var values = distractors.Select(d => d.Value).ToList();
            Assert.That(values.Distinct().Count(), Is.EqualTo(3));
            foreach (var d in distractors)
            {
                Assert.That(d.Value.Value, Is.Not.EqualTo(56));
                Assert.That(d.ErrorModel, Is.Not.EqualTo(ErrorCategory.None));
            }
        }

        [Test]
        public void Generate_SameSeedAndRequest_IsDeterministic()
        {
            var expression = Bin(BinaryOperator.Multiplication, 7, 8);
            var requestA = BuildRequest(seed: 42, expression, new EvaluationResult(56), count: 3);
            var requestB = BuildRequest(seed: 42, expression, new EvaluationResult(56), count: 3);

            var resultA = _generator.Generate(requestA);
            var resultB = _generator.Generate(requestB);

            Assert.That(resultA.IsSuccess, Is.True);
            Assert.That(resultB.IsSuccess, Is.True);

            var valuesA = resultA.Value.Select(d => (d.Value.Value, d.ErrorModel)).ToList();
            var valuesB = resultB.Value.Select(d => (d.Value.Value, d.ErrorModel)).ToList();
            Assert.That(valuesA, Is.EqualTo(valuesB));
        }

        // --- One happy path per error model (math-engine.md 4. table) -------

        [Test]
        public void Generate_MultiplicationRoot_OffersNeighbourInTimesTable()
        {
            var expression = Bin(BinaryOperator.Multiplication, 7, 8);
            var request = BuildRequest(seed: 2, expression, new EvaluationResult(56), count: 1,
                preferredModels: new[] { ErrorCategory.NeighbourInTimesTable });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.NeighbourInTimesTable));
            var candidates = new[] { 7 * 7, 7 * 9, 6 * 8, 8 * 8 };
            Assert.That(candidates.Contains(distractor.Value.Value), Is.True);
        }

        [Test]
        public void Generate_AnyScalarResult_OffersOnesDigitErrorWithinSameTensGroup()
        {
            var expression = Bin(BinaryOperator.Addition, 23, 10);
            var request = BuildRequest(seed: 3, expression, new EvaluationResult(33), count: 1,
                preferredModels: new[] { ErrorCategory.OnesDigitError });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.OnesDigitError));
            Assert.That(distractor.Value.Value / 10, Is.EqualTo(3)); // same tens group as 33
            Assert.That(distractor.Value.Value, Is.Not.EqualTo(33));
        }

        [Test]
        public void Generate_ResultAtLeastTen_OffersTensDigitErrorOfPlusOrMinusTen()
        {
            var expression = Bin(BinaryOperator.Addition, 23, 10);
            var request = BuildRequest(seed: 4, expression, new EvaluationResult(33), count: 1,
                preferredModels: new[] { ErrorCategory.TensDigitError });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.TensDigitError));
            Assert.That(Math.Abs(distractor.Value.Value - 33), Is.EqualTo(10));
        }

        [Test]
        public void Generate_AdditionWithCarry_OffersCarryErrorAsDigitwiseSumWithoutCarry()
        {
            // 37 + 28 = 65; digitwise without carry: ones (7+8)%10=5, tens (3+2)%10=5 => 55.
            var expression = Bin(BinaryOperator.Addition, 37, 28);
            var request = BuildRequest(seed: 5, expression, new EvaluationResult(65), count: 1,
                preferredModels: new[] { ErrorCategory.CarryError });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.CarryError));
            Assert.That(distractor.Value.Value, Is.EqualTo(55));
        }

        [Test]
        public void Generate_NoParensMultiplePrecedenceTiers_OffersNaiveOperatorPrecedenceError()
        {
            // 8 + 4 × 6 − 7 = 25 correctly; naive left-to-right = ((8+4)×6)−7 = 65
            // (canonical spec 17.1 example).
            var tree = new BinaryOperationNode(
                BinaryOperator.Subtraction,
                new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(8),
                    new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(4), new ConstantNode(6))),
                new ConstantNode(7));
            var request = BuildRequest(seed: 6, tree, new EvaluationResult(25), count: 1,
                preferredModels: new[] { ErrorCategory.OperatorPrecedenceError });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.OperatorPrecedenceError));
            Assert.That(distractor.Value.Value, Is.EqualTo(65));
        }

        [Test]
        public void Generate_ForcedParentheses_OffersIgnoredParenthesesRespectingPrecedence()
        {
            // (6 + 4) × 3 = 30 correctly; dropping the visible group but
            // keeping × before + on "6 + 4 × 3" = 6 + 12 = 18.
            var tree = new BinaryOperationNode(
                BinaryOperator.Multiplication,
                new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4), forceParentheses: true),
                new ConstantNode(3));
            var request = BuildRequest(seed: 7, tree, new EvaluationResult(30), count: 1,
                preferredModels: new[] { ErrorCategory.IgnoredParentheses });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.IgnoredParentheses));
            Assert.That(distractor.Value.Value, Is.EqualTo(18));
        }

        [Test]
        public void Generate_IntegerDivisionWithRemainder_OffersRemainderMisreadAsSwappedPair()
        {
            // 29 ÷r 4 = (7, 1) — the canonical smoke value (math-engine.md 1.).
            var tree = Bin(BinaryOperator.IntegerDivisionWithRemainder, 29, 4);
            var request = BuildRequest(seed: 8, tree, new EvaluationResult(7, 1), count: 1,
                preferredModels: new[] { ErrorCategory.RemainderMisread });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.RemainderMisread));
            Assert.That(distractor.Value.Value, Is.EqualTo(1));
            Assert.That(distractor.Value.Remainder, Is.EqualTo(7));
        }

        [Test]
        public void Generate_ModuloRoot_OffersRemainderMisreadAsQuotient()
        {
            // 29 mod 4 = 1; the quotient 29/4 = 7 is the "misread" value.
            var tree = Bin(BinaryOperator.Modulo, 29, 4);
            var request = BuildRequest(seed: 9, tree, new EvaluationResult(1), count: 1,
                preferredModels: new[] { ErrorCategory.RemainderMisread });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.RemainderMisread));
            Assert.That(distractor.Value.Value, Is.EqualTo(7));
        }

        [Test]
        public void Generate_AdditionRoot_OffersSignErrorAsSubtraction()
        {
            var expression = Bin(BinaryOperator.Addition, 37, 28);
            var request = BuildRequest(seed: 10, expression, new EvaluationResult(65), count: 1,
                preferredModels: new[] { ErrorCategory.SignError });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.SignError));
            Assert.That(distractor.Value.Value, Is.EqualTo(37 - 28));
        }

        [Test]
        public void Generate_SubtractionRoot_OffersReverseCalculationErrorAsSwappedOperands()
        {
            // b-a (47-83 = -36) is negative whenever the root was built with
            // a >= b (the usual case for a non-negative "correct" subtraction
            // result) — rule 4 (math-engine.md 4.) only allows a negative
            // candidate through when AllowNegative is true, so this is the
            // one model that genuinely needs it enabled to be observable.
            var expression = Bin(BinaryOperator.Subtraction, 83, 47);
            var request = BuildRequest(seed: 11, expression, new EvaluationResult(36), count: 1,
                preferredModels: new[] { ErrorCategory.ReverseCalculationError },
                minValue: -100, maxValue: 100, allowNegative: true);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            var distractor = result.Value[0];
            Assert.That(distractor.ErrorModel, Is.EqualTo(ErrorCategory.ReverseCalculationError));
            Assert.That(distractor.Value.Value, Is.EqualTo(47 - 83));
        }

        [Test]
        public void Generate_SubtractionRoot_ReverseCalculationErrorNegativeAndNotAllowed_FallsThroughToOtherModel()
        {
            // The mirror case without AllowNegative: rule 4 must filter the
            // negative candidate out, falling through to a different model
            // rather than ever surfacing a negative value.
            var expression = Bin(BinaryOperator.Subtraction, 83, 47);
            var request = BuildRequest(seed: 19, expression, new EvaluationResult(36), count: 1,
                preferredModels: new[] { ErrorCategory.ReverseCalculationError },
                allowNegative: false);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value[0].ErrorModel, Is.Not.EqualTo(ErrorCategory.ReverseCalculationError));
            Assert.That(result.Value[0].Value.Value, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Generate_DivisionRootNotEvenlyReversible_SkipsReverseCalculationError()
        {
            // 2 / 8 is not even an integer quotient the way the generator
            // would normally produce, and more importantly 8 % 2 == 0 but
            // 2 % 8 != 0 — the REVERSED division (8/2) is not an integer, so
            // "ha egész" (math-engine.md 4.) rules this model out; the
            // engine must fall through to another applicable model instead
            // of asserting a wrong value or throwing.
            var expression = Bin(BinaryOperator.Division, 2, 8);
            var request = BuildRequest(seed: 12, expression, new EvaluationResult(0), count: 1,
                preferredModels: new[] { ErrorCategory.ReverseCalculationError });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value[0].ErrorModel, Is.Not.EqualTo(ErrorCategory.ReverseCalculationError));
        }

        // --- Fallback (rule 1: correct ± k) ----------------------------------

        [Test]
        public void Generate_NoApplicableModelsLeft_FallsBackToCorrectPlusMinusK()
        {
            // A bare constant has no root operator at all — every shape-based
            // model is inapplicable; only OnesDigitError (always applicable)
            // plus the fallback can supply values.
            var expression = new ConstantNode(5);
            var request = BuildRequest(seed: 13, expression, new EvaluationResult(5), count: 3,
                minValue: 0, maxValue: 100);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Count, Is.EqualTo(3));
            var values = result.Value.Select(d => d.Value.Value).ToList();
            Assert.That(values.Distinct().Count(), Is.EqualTo(3));
            Assert.That(values.Any(v => v == 5), Is.False);
        }

        [Test]
        public void Generate_FallbackDistractor_IsLabelledUnclassified()
        {
            // minValue == maxValue == 105 sits a full tens-decade away from
            // correct == 5, so EVERY OnesDigitError candidate (which only
            // ever lands in the SAME decade, 0..9) is out of range — only
            // the correct±k fallback (k must grow to 100) can reach 105.
            var expression = new ConstantNode(5);
            var request = BuildRequest(seed: 14, expression, new EvaluationResult(5), count: 1,
                minValue: 105, maxValue: 105);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value[0].ErrorModel, Is.EqualTo(ErrorCategory.Unclassified));
            Assert.That(result.Value[0].Value.Value, Is.EqualTo(105));
        }

        [Test]
        public void Generate_NarrowRangeExhaustsEveryOption_FailsWithDistractorExhausted()
        {
            // [0, 1] can only ever hold {0, 1}; correct = 0 leaves only the
            // single value 1 available — asking for 2 distractors must fail.
            var expression = new ConstantNode(0);
            var request = BuildRequest(seed: 15, expression, new EvaluationResult(0), count: 2,
                minValue: 0, maxValue: 1, allowNegative: false);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("DISTRACTOR_EXHAUSTED"));
        }

        [Test]
        public void Generate_NegativeCandidateWithoutAllowNegative_IsRejectedInFavourOfFallback()
        {
            var expression = Bin(BinaryOperator.Subtraction, 5, 3);
            var request = BuildRequest(seed: 16, expression, new EvaluationResult(2), count: 1,
                preferredModels: new[] { ErrorCategory.ReverseCalculationError }, // would be 3-5=-2
                minValue: 0, maxValue: 100, allowNegative: false);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value[0].Value.Value, Is.GreaterThanOrEqualTo(0));
            // The preferred ReverseCalculationError candidate (3-5 = -2) is
            // negative and AllowNegative is false, so it must have been
            // skipped in favour of a different, non-negative model.
            Assert.That(result.Value[0].ErrorModel, Is.Not.EqualTo(ErrorCategory.ReverseCalculationError));
        }

        // --- Preferred models (contract rule: preferált, nem garancia) ------

        [Test]
        public void Generate_PreferredModelNotApplicable_FallsThroughToOtherApplicableModels()
        {
            // Root is Addition: NeighbourInTimesTable (needs Multiplication)
            // cannot apply, so the engine must fall through to whatever else
            // the shape offers rather than throwing or stalling.
            var expression = Bin(BinaryOperator.Addition, 10, 5);
            var request = BuildRequest(seed: 17, expression, new EvaluationResult(15), count: 1,
                preferredModels: new[] { ErrorCategory.NeighbourInTimesTable });

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value[0].ErrorModel, Is.Not.EqualTo(ErrorCategory.NeighbourInTimesTable));
        }

        // --- Precondition / caller-contract violations (P4) -----------------

        [Test]
        public void Generate_NullRequest_Throws()
        {
            Assert.That(() => _generator.Generate(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Generate_NullRandom_Throws()
        {
            var request = new DistractorRequest
            {
                Random = null,
                Expression = new ConstantNode(1),
                CorrectAnswer = new EvaluationResult(1),
                Count = 1,
                MinValue = 0,
                MaxValue = 100,
            };

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Generate_NullExpression_Throws()
        {
            var request = new DistractorRequest
            {
                Random = new DeterministicRandomSource(1),
                Expression = null,
                CorrectAnswer = new EvaluationResult(1),
                Count = 1,
                MinValue = 0,
                MaxValue = 100,
            };

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Generate_CountOutOfRange_Throws(int count)
        {
            var request = BuildRequest(seed: 1, new ConstantNode(1), new EvaluationResult(1), count: count);

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Generate_MinValueGreaterThanMaxValue_Throws()
        {
            var request = BuildRequest(seed: 1, new ConstantNode(1), new EvaluationResult(1), count: 1,
                minValue: 100, maxValue: 0);

            Assert.That(() => _generator.Generate(request), Throws.TypeOf<ArgumentException>());
        }

        // --- Missing operand / equation shape --------------------------------

        [Test]
        public void Generate_EqualityWithUnknownOnLeft_UsesKnownSideAsEffectiveRoot()
        {
            // "? + 7 = 3 × 8" — the unknown's solved value is 17; distractors
            // should be derived from the known side "3 × 8", not crash on
            // the equation shape.
            var equation = new EqualityNode(
                new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(7)),
                Bin(BinaryOperator.Multiplication, 3, 8));
            var request = BuildRequest(seed: 18, equation, new EvaluationResult(17), count: 2);

            var result = _generator.Generate(request);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Count, Is.EqualTo(2));
            foreach (var d in result.Value)
            {
                Assert.That(d.Value.Value, Is.Not.EqualTo(17));
            }
        }
    }
}
