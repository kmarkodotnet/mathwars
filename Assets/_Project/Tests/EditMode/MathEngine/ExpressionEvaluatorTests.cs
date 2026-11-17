using System;
using MathHorde.Core.Expressions;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // Exercises IExpressionEvaluator/ExpressionEvaluator/EvaluationOptions
    // (contract math-engine.md 1., spec 6.1/6.2/17.1, task F2-03) — zero
    // UnityEngine dependency, runs both under the Unity Test Runner and the
    // headless tests/MathHorde.Tests.sln mirror (ADR-003).
    [TestFixture]
    public class ExpressionEvaluatorTests
    {
        private ExpressionEvaluator _evaluator;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new ExpressionEvaluator();
        }

        // --- Kötelező smoke-értékek (spec 17.1 / math-engine.md 1.) --------

        [Test]
        public void Evaluate_Multiplication_7x8_Is56()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(7), new ConstantNode(8));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(56));
            Assert.That(result.Value.Remainder, Is.Null);
        }

        [Test]
        public void Evaluate_Addition_37plus28_Is65()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(37), new ConstantNode(28));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(65));
        }

        [Test]
        public void Evaluate_Subtraction_83minus47_Is36()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(83), new ConstantNode(47));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(36));
        }

        [Test]
        public void Evaluate_Division_72div9_Is8()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(72), new ConstantNode(9));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(8));
        }

        [Test]
        public void Evaluate_IntegerDivisionWithRemainder_29divr4_Is7Remainder1()
        {
            var expr = new BinaryOperationNode(
                BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(29), new ConstantNode(4));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(7));
            Assert.That(result.Value.Remainder, Is.EqualTo(1));
            Assert.That(result.Value.ToDisplayString(), Is.EqualTo("7 maradék 1"));
        }

        [Test]
        public void Evaluate_Modulo_29mod4_Is1()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(29), new ConstantNode(4));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(1));
        }

        [Test]
        public void Evaluate_ForceParentheses_6plus4TimesThree_Is30()
        {
            var sum = new BinaryOperationNode(
                BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4), forceParentheses: true);
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, sum, new ConstantNode(3));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(expr.ToDisplayString(), Is.EqualTo("(6 + 4) × 3"));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(30));
        }

        [Test]
        public void Evaluate_OperatorPrecedence_8plus4times6minus7_Is25()
        {
            // 8 + 4 × 6 − 7  ==  8 + (4×6) − 7  ==  8 + 24 − 7  ==  25
            var multiplication = new BinaryOperationNode(
                BinaryOperator.Multiplication, new ConstantNode(4), new ConstantNode(6));
            var addition = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(8), multiplication);
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, addition, new ConstantNode(7));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(expr.ToDisplayString(), Is.EqualTo("8 + 4 × 6 − 7"));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(25));
        }

        // --- Hibaágak ------------------------------------------------------

        [Test]
        public void Evaluate_DivisionByZero_FailsWithDivisionByZero()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(5), new ConstantNode(0));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("DIVISION_BY_ZERO"));
        }

        [Test]
        public void Evaluate_IntegerDivisionWithRemainderByZero_FailsWithDivisionByZero()
        {
            var expr = new BinaryOperationNode(
                BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(5), new ConstantNode(0));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("DIVISION_BY_ZERO"));
        }

        [Test]
        public void Evaluate_DivisionWithNonIntegerResult_FailsWithNonIntegerResult()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(7), new ConstantNode(2));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NON_INTEGER_RESULT"));
        }

        [Test]
        public void Evaluate_ModuloWithZeroDivisor_FailsWithModuloNonPositiveDivisor()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(5), new ConstantNode(0));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MODULO_NON_POSITIVE_DIVISOR"));
        }

        [Test]
        public void Evaluate_ModuloWithNegativeDivisor_FailsWithModuloNonPositiveDivisor()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(5), new ConstantNode(-3));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MODULO_NON_POSITIVE_DIVISOR"));
        }

        [Test]
        public void Evaluate_ModuloWithNegativeDividend_IsAlwaysNonNegative()
        {
            // Spec: "eredmény mindig >= 0" even when the dividend is negative —
            // floor-mod, not C#'s truncating %. -7 mod 3 == 2 (not -1).
            var expr = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(-7), new ConstantNode(3));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(2));
        }

        [Test]
        public void Evaluate_IntegerDivisionWithRemainder_NegativeDividend_RemainderStaysNonNegative()
        {
            // -7 = (-3)*3 + 2 — floor division, remainder in [0, |b|-1].
            var expr = new BinaryOperationNode(
                BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(-7), new ConstantNode(3));
            var options = new EvaluationOptions { AllowNegativeIntermediate = true, AllowNegativeResult = true };

            var result = _evaluator.Evaluate(expr, options);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(-3));
            Assert.That(result.Value.Remainder, Is.EqualTo(2));
        }

        [Test]
        public void Evaluate_SubtractionNegativeResult_DefaultOptions_FailsWithNegativeResultNotAllowed()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(5), new ConstantNode(10));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NEGATIVE_RESULT_NOT_ALLOWED"));
        }

        [Test]
        public void Evaluate_SubtractionNegativeResult_AllowNegativeResult_Succeeds()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(5), new ConstantNode(10));
            var options = new EvaluationOptions { AllowNegativeResult = true };

            var result = _evaluator.Evaluate(expr, options);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(-5));
        }

        [Test]
        public void Evaluate_NegativeIntermediate_DefaultOptions_FailsEvenIfFinalResultIsPositive()
        {
            // (3 - 10) + 20 == 13 overall, but the (3-10) sub-expression is -7.
            var innerSubtraction = new BinaryOperationNode(
                BinaryOperator.Subtraction, new ConstantNode(3), new ConstantNode(10));
            var expr = new BinaryOperationNode(BinaryOperator.Addition, innerSubtraction, new ConstantNode(20));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NEGATIVE_RESULT_NOT_ALLOWED"));
        }

        [Test]
        public void Evaluate_NegativeIntermediate_AllowNegativeIntermediateOnly_SucceedsWithPositiveFinal()
        {
            var innerSubtraction = new BinaryOperationNode(
                BinaryOperator.Subtraction, new ConstantNode(3), new ConstantNode(10));
            var expr = new BinaryOperationNode(BinaryOperator.Addition, innerSubtraction, new ConstantNode(20));
            var options = new EvaluationOptions { AllowNegativeIntermediate = true, AllowNegativeResult = false };

            var result = _evaluator.Evaluate(expr, options);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(13));
        }

        [Test]
        public void Evaluate_ResultAboveMaxValue_FailsWithOverflow()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(60), new ConstantNode(60));
            var options = new EvaluationOptions { MinValue = -100, MaxValue = 100 };

            var result = _evaluator.Evaluate(expr, options);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("OVERFLOW"));
        }

        [Test]
        public void Evaluate_ResultBelowMinValue_FailsWithOverflow()
        {
            // The range check applies to a BinaryOperationNode's computed
            // result, not to a bare leaf (see ExpressionEvaluator's class
            // comment, note 3) — so this uses an actual subtraction whose
            // result falls below MinValue, rather than a raw constant.
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(5), new ConstantNode(20));
            var options = new EvaluationOptions { MinValue = -10, MaxValue = 100 };

            var result = _evaluator.Evaluate(expr, options);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("OVERFLOW"));
        }

        [Test]
        public void Evaluate_BareNegativeConstant_IsNotSubjectToNegativeResultCheck()
        {
            // A literal operand is data already in the tree, not a computed
            // "result" (see ExpressionEvaluator's class comment, note 3) —
            // unlike an operator's output, it is never rejected for being
            // negative, even under the default (AllowNegativeResult = false)
            // options, and even as the entire (root) expression.
            var result = _evaluator.Evaluate(new ConstantNode(-5), EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(-5));
        }

        [Test]
        public void Evaluate_TrueIntOverflow_FailsWithOverflow()
        {
            var expr = new BinaryOperationNode(
                BinaryOperator.Multiplication, new ConstantNode(1_500_000_000), new ConstantNode(2));
            var options = new EvaluationOptions { MinValue = int.MinValue, MaxValue = int.MaxValue };

            var result = _evaluator.Evaluate(expr, options);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("OVERFLOW"));
        }

        [Test]
        public void Evaluate_UnknownNodePresent_FailsWithUnknownInExpression()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(5));

            var result = _evaluator.Evaluate(expr, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNKNOWN_IN_EXPRESSION"));
        }

        // --- EvaluateWith ---------------------------------------------------

        [Test]
        public void EvaluateWith_SubstitutesUnknown_7plusUnknown_With10_Is17()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());

            var result = _evaluator.EvaluateWith(expr, 10, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Value, Is.EqualTo(17));
        }

        [Test]
        public void EvaluateWith_EqualityNode_Throws()
        {
            var equality = new EqualityNode(new ConstantNode(1), new ConstantNode(1));

            Assert.That(
                () => _evaluator.EvaluateWith(equality, 1, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentException>());
        }

        // --- CheckEquality ---------------------------------------------------

        [Test]
        public void CheckEquality_7plusUnknown_EqualsThreeTimesEight_UnknownIs17_IsTrue()
        {
            // 7 + ? = 3 × 8  ⇒  ? = 17
            var left = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new ConstantNode(8));
            var equation = new EqualityNode(left, right);

            var result = _evaluator.CheckEquality(equation, 17, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
        }

        [Test]
        public void CheckEquality_WrongUnknownValue_IsFalse()
        {
            var left = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new ConstantNode(8));
            var equation = new EqualityNode(left, right);

            var result = _evaluator.CheckEquality(equation, 5, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.False);
        }

        [Test]
        public void CheckEquality_NestedEquationExample_UnknownIs5_IsTrue()
        {
            // (? + 17) × 3 = 6 × (14 − 3)  ⇒  ? = 5
            var innerSum = new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(17));
            var left = new BinaryOperationNode(BinaryOperator.Multiplication, innerSum, new ConstantNode(3));
            var innerDiff = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(14), new ConstantNode(3));
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(6), innerDiff);
            var equation = new EqualityNode(left, right);

            var result = _evaluator.CheckEquality(equation, 5, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
        }

        [Test]
        public void CheckEquality_RemainderBearingBothSides_MatchingRemainder_IsTrue()
        {
            // AST invariant 3: IntegerDivisionWithRemainder may be a direct
            // EqualityNode child. 29 ÷r 4 = (7,1); ? ÷r 4 with ? = 29 also (7,1).
            var left = new BinaryOperationNode(
                BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(29), new ConstantNode(4));
            var right = new BinaryOperationNode(
                BinaryOperator.IntegerDivisionWithRemainder, new UnknownNode(), new ConstantNode(4));
            var equation = new EqualityNode(left, right);

            var trueResult = _evaluator.CheckEquality(equation, 29, EvaluationOptions.Default);
            var falseResult = _evaluator.CheckEquality(equation, 30, EvaluationOptions.Default);

            Assert.That(trueResult.IsSuccess, Is.True);
            Assert.That(trueResult.Value, Is.True);
            Assert.That(falseResult.IsSuccess, Is.True);
            Assert.That(falseResult.Value, Is.False);
        }

        [Test]
        public void CheckEquality_LeftSideFails_PropagatesErrorCode()
        {
            var left = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(5), new ConstantNode(0));
            var right = new ConstantNode(5);
            var equation = new EqualityNode(left, right);

            var result = _evaluator.CheckEquality(equation, 0, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("DIVISION_BY_ZERO"));
        }

        // --- CountUnknowns ---------------------------------------------------

        [Test]
        public void CountUnknowns_ConstantOnly_IsZero()
        {
            Assert.That(_evaluator.CountUnknowns(new ConstantNode(5)), Is.EqualTo(0));
        }

        [Test]
        public void CountUnknowns_SingleUnknownNode_IsOne()
        {
            Assert.That(_evaluator.CountUnknowns(new UnknownNode()), Is.EqualTo(1));
        }

        [Test]
        public void CountUnknowns_OneUnknownInBinaryTree_IsOne()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(5));

            Assert.That(_evaluator.CountUnknowns(expr), Is.EqualTo(1));
        }

        [Test]
        public void CountUnknowns_EqualityWithUnknownOnBothSides_IsTwo()
        {
            var equation = new EqualityNode(new UnknownNode(), new UnknownNode());

            Assert.That(_evaluator.CountUnknowns(equation), Is.EqualTo(2));
        }

        // --- MeasureParenthesisDepth ------------------------------------------

        [Test]
        public void MeasureParenthesisDepth_NoParentheses_IsZero()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(2), new ConstantNode(3));

            Assert.That(_evaluator.MeasureParenthesisDepth(expr), Is.EqualTo(0));
        }

        [Test]
        public void MeasureParenthesisDepth_ForceParentheses_IsOne()
        {
            var sum = new BinaryOperationNode(
                BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4), forceParentheses: true);
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, sum, new ConstantNode(3));

            Assert.That(_evaluator.MeasureParenthesisDepth(expr), Is.EqualTo(1));
        }

        [Test]
        public void MeasureParenthesisDepth_PrecedenceNecessitatedParens_IsOne()
        {
            // 10 − (5 − 2) — the right operand needs explicit parens to keep
            // its meaning under left-associativity, even with no ForceParentheses.
            var innerSubtraction = new BinaryOperationNode(
                BinaryOperator.Subtraction, new ConstantNode(5), new ConstantNode(2));
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(10), innerSubtraction);

            Assert.That(expr.ToDisplayString(), Is.EqualTo("10 − (5 − 2)"));
            Assert.That(_evaluator.MeasureParenthesisDepth(expr), Is.EqualTo(1));
        }

        [Test]
        public void MeasureParenthesisDepth_NestedForceParentheses_IsTwo()
        {
            var innerSum = new BinaryOperationNode(
                BinaryOperator.Addition, new ConstantNode(2), new ConstantNode(3), forceParentheses: true);
            var multiplication = new BinaryOperationNode(BinaryOperator.Multiplication, innerSum, new ConstantNode(4));
            var subtraction = new BinaryOperationNode(
                BinaryOperator.Subtraction, multiplication, new ConstantNode(1), forceParentheses: true);
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, subtraction, new ConstantNode(2));

            Assert.That(expr.ToDisplayString(), Is.EqualTo("((2 + 3) × 4 − 1) × 2"));
            Assert.That(_evaluator.MeasureParenthesisDepth(expr), Is.EqualTo(2));
        }

        [Test]
        public void MeasureParenthesisDepth_EqualityNode_UsesMaxOfBothSides()
        {
            var sum = new BinaryOperationNode(
                BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4), forceParentheses: true);
            var equation = new EqualityNode(sum, new ConstantNode(10));

            Assert.That(_evaluator.MeasureParenthesisDepth(equation), Is.EqualTo(1));
        }

        // --- Szerződésszegés (null / EqualityNode) -> kivétel ------------------

        [Test]
        public void Evaluate_NullExpression_Throws()
        {
            Assert.That(
                () => _evaluator.Evaluate(null, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Evaluate_NullOptions_Throws()
        {
            Assert.That(
                () => _evaluator.Evaluate(new ConstantNode(1), null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Evaluate_EqualityNode_Throws()
        {
            var equality = new EqualityNode(new ConstantNode(1), new ConstantNode(1));

            Assert.That(
                () => _evaluator.Evaluate(equality, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CheckEquality_NullEquation_Throws()
        {
            Assert.That(
                () => _evaluator.CheckEquality(null, 1, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void CountUnknowns_NullExpression_Throws()
        {
            Assert.That(() => _evaluator.CountUnknowns(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void MeasureParenthesisDepth_NullExpression_Throws()
        {
            Assert.That(() => _evaluator.MeasureParenthesisDepth(null), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
