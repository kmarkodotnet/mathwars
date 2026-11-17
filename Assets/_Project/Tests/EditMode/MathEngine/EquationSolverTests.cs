using System;
using MathHorde.Core.Expressions;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    // Exercises IEquationSolver/EquationSolver (contract math-engine.md 2.,
    // spec 5.2, task F2-04) — zero UnityEngine dependency, runs both under
    // the Unity Test Runner and the headless tests/MathHorde.Tests.sln
    // mirror (ADR-003).
    [TestFixture]
    public class EquationSolverTests
    {
        private EquationSolver _solver;

        [SetUp]
        public void SetUp()
        {
            _solver = new EquationSolver(new ExpressionEvaluator());
        }

        // --- Kötelező smoke-értékek (math-engine.md 2.) ------------------------

        [Test]
        public void Solve_7plusUnknown_EqualsThreeTimesEight_Is17()
        {
            // 7 + ? = 3 × 8  ⇒  17
            var left = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new ConstantNode(8));
            var equation = new EqualityNode(left, right);

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(17));
        }

        [Test]
        public void Solve_NestedEquationExample_Is5()
        {
            // (? + 17) × 3 = 6 × (14 − 3)  ⇒  5
            var innerSum = new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(17));
            var left = new BinaryOperationNode(BinaryOperator.Multiplication, innerSum, new ConstantNode(3));
            var innerDiff = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(14), new ConstantNode(3));
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(6), innerDiff);
            var equation = new EqualityNode(left, right);

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(5));
        }

        [Test]
        public void SolveForTarget_MissingOperand_ThreeTimesUnknown_Is24_Is8()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new UnknownNode());

            var result = _solver.SolveForTarget(expr, 24, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(8));
        }

        [Test]
        public void SolveForTarget_UnknownOnLeft_UnknownMinus9_Is3_Is12()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Subtraction, new UnknownNode(), new ConstantNode(9));

            var result = _solver.SolveForTarget(expr, 3, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(12));
        }

        [Test]
        public void SolveForTarget_UnknownAsDividend_UnknownDiv4_Is7_Is28()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Division, new UnknownNode(), new ConstantNode(4));

            var result = _solver.SolveForTarget(expr, 7, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(28));
        }

        [Test]
        public void Solve_UnknownOnRightSideOfEquation_IsSolvedCorrectly()
        {
            // 3 × 8 = 7 + ?  ⇒  17 (mirror of the contract example, unknown on the right)
            var left = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new ConstantNode(8));
            var right = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());
            var equation = new EqualityNode(left, right);

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo(17));
        }

        // --- Hibaágak (mind az 5 dokumentált kód, math-engine.md 2.) ----------

        [Test]
        public void Solve_NoUnknown_FailsWithNoUnknown()
        {
            var equation = new EqualityNode(new ConstantNode(5), new ConstantNode(5));

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NO_UNKNOWN"));
        }

        [Test]
        public void Solve_MultipleUnknowns_FailsWithMultipleUnknowns()
        {
            var left = new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(1));
            var right = new BinaryOperationNode(BinaryOperator.Subtraction, new UnknownNode(), new ConstantNode(2));
            var equation = new EqualityNode(left, right);

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MULTIPLE_UNKNOWNS"));
        }

        [Test]
        public void Solve_MultiplicationByZero_NonZeroTarget_FailsWithNoIntegerSolution()
        {
            var left = new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(0));
            var equation = new EqualityNode(left, new ConstantNode(5));

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NO_INTEGER_SOLUTION"));
        }

        [Test]
        public void SolveForTarget_MultiplicationByTwo_OddTarget_FailsWithNoIntegerSolution()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(2));

            var result = _solver.SolveForTarget(expr, 7, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NO_INTEGER_SOLUTION"));
        }

        [Test]
        public void Solve_MultiplicationByZero_ZeroTarget_FailsWithNotUnique()
        {
            var left = new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(0));
            var equation = new EqualityNode(left, new ConstantNode(0));

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_UNIQUE"));
        }

        [Test]
        public void Solve_UnknownAsModuloDividend_FailsWithNotUnique()
        {
            // ? mod 4 = 1 — infinitely many integers are congruent to 1 mod 4.
            var left = new BinaryOperationNode(BinaryOperator.Modulo, new UnknownNode(), new ConstantNode(4));
            var equation = new EqualityNode(left, new ConstantNode(1));

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_UNIQUE"));
        }

        [Test]
        public void Solve_UnknownAsDivisionDivisor_FailsWithUnsupportedShape()
        {
            // 20 / ? = 4 — unknown in a divisor position, contract-excluded.
            var left = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(20), new UnknownNode());
            var equation = new EqualityNode(left, new ConstantNode(4));

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_SHAPE"));
        }

        [Test]
        public void Solve_UnknownAsModuloDivisor_FailsWithUnsupportedShape()
        {
            // 20 mod ? = 2 — unknown on Modulo's right (divisor) side, contract-excluded.
            var left = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(20), new UnknownNode());
            var equation = new EqualityNode(left, new ConstantNode(2));

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_SHAPE"));
        }

        [Test]
        public void Solve_UnknownUnderIntegerDivisionWithRemainder_FailsWithUnsupportedShape()
        {
            var left = new BinaryOperationNode(BinaryOperator.IntegerDivisionWithRemainder, new UnknownNode(), new ConstantNode(4));
            var equation = new EqualityNode(left, new ConstantNode(7));

            var result = _solver.Solve(equation, EvaluationOptions.Default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_SHAPE"));
        }

        // --- HasUniqueIntegerSolution -----------------------------------------

        [Test]
        public void HasUniqueIntegerSolution_UniqueGlobalSolutionInRange_IsTrue()
        {
            var left = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new ConstantNode(8));
            var equation = new EqualityNode(left, right); // ? = 17

            Assert.That(_solver.HasUniqueIntegerSolution(equation, 0, 100, EvaluationOptions.Default), Is.True);
        }

        [Test]
        public void HasUniqueIntegerSolution_UniqueGlobalSolutionOutsideRange_IsFalse()
        {
            var left = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new ConstantNode(8));
            var equation = new EqualityNode(left, right); // ? = 17

            Assert.That(_solver.HasUniqueIntegerSolution(equation, 20, 30, EvaluationOptions.Default), Is.False);
        }

        [Test]
        public void HasUniqueIntegerSolution_NoUnknown_IsFalse()
        {
            var equation = new EqualityNode(new ConstantNode(5), new ConstantNode(5));

            Assert.That(_solver.HasUniqueIntegerSolution(equation, -10, 10, EvaluationOptions.Default), Is.False);
        }

        [Test]
        public void HasUniqueIntegerSolution_ModuloDividend_ExactlyOneInRange_IsTrue()
        {
            // ? mod 4 = 1 — within [0, 3] only "1" satisfies.
            var left = new BinaryOperationNode(BinaryOperator.Modulo, new UnknownNode(), new ConstantNode(4));
            var equation = new EqualityNode(left, new ConstantNode(1));

            Assert.That(_solver.HasUniqueIntegerSolution(equation, 0, 3, EvaluationOptions.Default), Is.True);
        }

        [Test]
        public void HasUniqueIntegerSolution_ModuloDividend_TwoInRange_IsFalse()
        {
            // ? mod 4 = 1 — within [0, 7] both "1" and "5" satisfy.
            var left = new BinaryOperationNode(BinaryOperator.Modulo, new UnknownNode(), new ConstantNode(4));
            var equation = new EqualityNode(left, new ConstantNode(1));

            Assert.That(_solver.HasUniqueIntegerSolution(equation, 0, 7, EvaluationOptions.Default), Is.False);
        }

        [Test]
        public void HasUniqueIntegerSolution_MultiplicationByZero_ZeroTarget_SinglePointRange_IsTrue()
        {
            var left = new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(0));
            var equation = new EqualityNode(left, new ConstantNode(0));

            Assert.That(_solver.HasUniqueIntegerSolution(equation, 5, 5, EvaluationOptions.Default), Is.True);
        }

        [Test]
        public void HasUniqueIntegerSolution_MultiplicationByZero_ZeroTarget_WiderRange_IsFalse()
        {
            var left = new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(0));
            var equation = new EqualityNode(left, new ConstantNode(0));

            Assert.That(_solver.HasUniqueIntegerSolution(equation, 5, 6, EvaluationOptions.Default), Is.False);
        }

        [Test]
        public void HasUniqueIntegerSolution_SearchMinGreaterThanSearchMax_Throws()
        {
            var equation = new EqualityNode(new UnknownNode(), new ConstantNode(5));

            Assert.That(
                () => _solver.HasUniqueIntegerSolution(equation, 10, 0, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentException>());
        }

        // --- Szerződésszegés (null / EqualityNode) -> kivétel ------------------

        [Test]
        public void Solve_NullEquation_Throws()
        {
            Assert.That(
                () => _solver.Solve(null, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Solve_NullOptions_Throws()
        {
            var equation = new EqualityNode(new UnknownNode(), new ConstantNode(1));

            Assert.That(
                () => _solver.Solve(equation, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void SolveForTarget_NullExpression_Throws()
        {
            Assert.That(
                () => _solver.SolveForTarget(null, 1, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void SolveForTarget_EqualityNode_Throws()
        {
            var equality = new EqualityNode(new UnknownNode(), new ConstantNode(1));

            Assert.That(
                () => _solver.SolveForTarget(equality, 1, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void HasUniqueIntegerSolution_NullEquation_Throws()
        {
            Assert.That(
                () => _solver.HasUniqueIntegerSolution(null, 0, 10, EvaluationOptions.Default),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NullEvaluator_Throws()
        {
            Assert.That(() => new EquationSolver(null), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
