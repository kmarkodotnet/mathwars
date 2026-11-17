using System;
using System.Collections.Generic;
using MathHorde.Core.Expressions;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Expressions
{
    // Exercises the ExpressionNode AST hierarchy (MathHorde.Core.Expressions,
    // contract core-domain.md 2., spec 6.2) — zero UnityEngine dependency,
    // compiles and runs both under the Unity Test Runner and the headless
    // tests/MathHorde.Tests.sln mirror (ADR-003).
    //
    // P1 (math-engine.md 0.): there is no string-parsing/eval path to test
    // here by construction — every expression under test is built as an
    // explicit tree.

    [TestFixture]
    public class ConstantNodeTests
    {
        [Test]
        public void ToDisplayString_UsesInvariantCultureDigits()
        {
            var node = new ConstantNode(42);

            Assert.That(node.ToDisplayString(), Is.EqualTo("42"));
        }

        [Test]
        public void ToDisplayString_NegativeValue_IncludesSign()
        {
            var node = new ConstantNode(-7);

            Assert.That(node.ToDisplayString(), Is.EqualTo("-7"));
        }

        [Test]
        public void Equals_SameValue_AreEqual()
        {
            var a = new ConstantNode(5);
            var b = new ConstantNode(5);

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Equals_DifferentValue_AreNotEqual()
        {
            var a = new ConstantNode(5);
            var b = new ConstantNode(6);

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_DifferentNodeType_AreNotEqual()
        {
            var constant = new ConstantNode(5);
            var unknown = new UnknownNode();

            Assert.That(constant.Equals(unknown), Is.False);
        }

        [Test]
        public void Accept_DispatchesToVisitConstant()
        {
            var node = new ConstantNode(5);
            var visitor = new RecordingVisitor();

            var result = node.Accept(visitor);

            Assert.That(result, Is.EqualTo("constant:5"));
        }

        [Test]
        public void Accept_NullVisitor_Throws()
        {
            var node = new ConstantNode(5);

            Assert.That(() => node.Accept<string>(null), Throws.TypeOf<ArgumentNullException>());
        }
    }

    [TestFixture]
    public class UnknownNodeTests
    {
        [Test]
        public void DefaultSymbol_IsQuestionMark()
        {
            var node = new UnknownNode();

            Assert.That(node.Symbol, Is.EqualTo("?"));
            Assert.That(node.ToDisplayString(), Is.EqualTo("?"));
        }

        [Test]
        public void CustomSymbol_IsUsedVerbatim()
        {
            var node = new UnknownNode("x");

            Assert.That(node.ToDisplayString(), Is.EqualTo("x"));
        }

        [Test]
        public void Constructor_EmptySymbol_Throws()
        {
            Assert.That(() => new UnknownNode(""), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_NullSymbol_Throws()
        {
            Assert.That(() => new UnknownNode(null), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Equals_SameSymbol_AreEqual()
        {
            Assert.That(new UnknownNode().Equals(new UnknownNode()), Is.True);
        }

        [Test]
        public void Equals_DifferentSymbol_AreNotEqual()
        {
            Assert.That(new UnknownNode("x").Equals(new UnknownNode("y")), Is.False);
        }

        [Test]
        public void Accept_DispatchesToVisitUnknown()
        {
            var node = new UnknownNode("x");
            var visitor = new RecordingVisitor();

            Assert.That(node.Accept(visitor), Is.EqualTo("unknown:x"));
        }
    }

    [TestFixture]
    public class BinaryOperationNodeTests
    {
        [Test]
        public void Constructor_NullLeft_Throws()
        {
            Assert.That(
                () => new BinaryOperationNode(BinaryOperator.Addition, null, new ConstantNode(1)),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NullRight_Throws()
        {
            Assert.That(
                () => new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_EqualityNodeAsLeftOperand_Throws()
        {
            var equality = new EqualityNode(new ConstantNode(1), new ConstantNode(1));

            Assert.That(
                () => new BinaryOperationNode(BinaryOperator.Addition, equality, new ConstantNode(1)),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_EqualityNodeAsRightOperand_Throws()
        {
            var equality = new EqualityNode(new ConstantNode(1), new ConstantNode(1));

            Assert.That(
                () => new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), equality),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_IntegerDivisionWithRemainderAsOperand_Throws()
        {
            // AST invariant 3: a (quotient, remainder) pair result cannot
            // feed into another operation as a plain operand.
            var divMod = new BinaryOperationNode(
                BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(29), new ConstantNode(4));

            Assert.That(
                () => new BinaryOperationNode(BinaryOperator.Addition, divMod, new ConstantNode(1)),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), divMod),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void IntegerDivisionWithRemainder_AsEqualityChild_IsAllowed()
        {
            // AST invariant 3 explicitly permits this shape — only nesting
            // inside ANOTHER BinaryOperationNode is forbidden.
            var divMod = new BinaryOperationNode(
                BinaryOperator.IntegerDivisionWithRemainder, new ConstantNode(29), new ConstantNode(4));

            Assert.That(() => new EqualityNode(divMod, new ConstantNode(7)), Throws.Nothing);
        }

        [TestCase(BinaryOperator.Addition, "+")]
        [TestCase(BinaryOperator.Subtraction, "−")]
        [TestCase(BinaryOperator.Multiplication, "×")]
        [TestCase(BinaryOperator.Division, "÷")]
        [TestCase(BinaryOperator.Modulo, "mod")]
        [TestCase(BinaryOperator.IntegerDivisionWithRemainder, "÷r")]
        public void ToDisplayString_UsesExpectedOperatorGlyph(BinaryOperator op, string symbol)
        {
            var node = new BinaryOperationNode(op, new ConstantNode(6), new ConstantNode(4));

            Assert.That(node.ToDisplayString(), Is.EqualTo("6 " + symbol + " 4"));
        }

        [Test]
        public void ToDisplayString_FlatAddition_NoParentheses()
        {
            // "37 + 28" (math-engine.md 1. smoke value) — no tree reason to
            // add parentheses anywhere.
            var node = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(37), new ConstantNode(28));

            Assert.That(node.ToDisplayString(), Is.EqualTo("37 + 28"));
        }

        [Test]
        public void ToDisplayString_MultiplicationOfParenthesizedAddition_AddsParens()
        {
            // (6 + 4) × 3 = 30 — math-engine.md 1. smoke value. The addition
            // binds weaker than the multiplication it feeds into, so its
            // parentheses are REQUIRED to preserve meaning, not cosmetic.
            var addition = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4));
            var multiplication = new BinaryOperationNode(BinaryOperator.Multiplication, addition, new ConstantNode(3));

            Assert.That(multiplication.ToDisplayString(), Is.EqualTo("(6 + 4) × 3"));
        }

        [Test]
        public void ToDisplayString_AdditionOfMultiplication_OmitsUnnecessaryParens()
        {
            // 8 + 4 × 6 − 7 = 25 — math-engine.md 1. smoke value. Here the
            // multiplication binds TIGHTER than its surrounding +/-, so no
            // parentheses must appear even though the subtree is nested.
            var multiplication =
                new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(4), new ConstantNode(6));
            var addition = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(8), multiplication);
            var subtraction = new BinaryOperationNode(BinaryOperator.Subtraction, addition, new ConstantNode(7));

            Assert.That(subtraction.ToDisplayString(), Is.EqualTo("8 + 4 × 6 − 7"));
        }

        [Test]
        public void ToDisplayString_LeftAssociativeChainAtSamePrecedence_NoParens()
        {
            // (a - b) - c reads naturally as "a - b - c": a left child at
            // the SAME precedence as its parent never needs parentheses.
            var inner = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(10), new ConstantNode(3));
            var outer = new BinaryOperationNode(BinaryOperator.Subtraction, inner, new ConstantNode(2));

            Assert.That(outer.ToDisplayString(), Is.EqualTo("10 − 3 − 2"));
        }

        [Test]
        public void ToDisplayString_RightChildAtSamePrecedence_RequiresParens()
        {
            // a - (b - c) is NOT the same value as (a - b) - c for a
            // non-associative, left-associative operator: the right child
            // at the parent's own precedence level MUST get parentheses, or
            // the printed string would silently claim a different tree.
            var inner = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(3), new ConstantNode(2));
            var outer = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(10), inner);

            Assert.That(outer.ToDisplayString(), Is.EqualTo("10 − (3 − 2)"));
        }

        [Test]
        public void ToDisplayString_RightChildSameLevelDivision_RequiresParens()
        {
            // a ÷ (b ÷ c): same-precedence right child on a left-associative
            // level-2 operator still needs parens, mirroring the +/- case.
            var inner = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(12), new ConstantNode(3));
            var outer = new BinaryOperationNode(BinaryOperator.Division, new ConstantNode(72), inner);

            Assert.That(outer.ToDisplayString(), Is.EqualTo("72 ÷ (12 ÷ 3)"));
        }

        [Test]
        public void ToDisplayString_ForceParentheses_WrapsEvenWhenNotRequired()
        {
            var node = new BinaryOperationNode(
                BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4), forceParentheses: true);

            Assert.That(node.ToDisplayString(), Is.EqualTo("(6 + 4)"));
        }

        [Test]
        public void ToDisplayString_ForcedChildParentheses_NotDoubled()
        {
            var forcedAddition = new BinaryOperationNode(
                BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4), forceParentheses: true);
            var multiplication =
                new BinaryOperationNode(BinaryOperator.Multiplication, forcedAddition, new ConstantNode(3));

            // Would need parens anyway by precedence; forcing must not
            // produce "((6 + 4))".
            Assert.That(multiplication.ToDisplayString(), Is.EqualTo("(6 + 4) × 3"));
        }

        [Test]
        public void Equals_SameShape_AreEqual()
        {
            var a = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2));
            var b = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2));

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Equals_DifferentOperator_AreNotEqual()
        {
            var a = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2));
            var b = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(1), new ConstantNode(2));

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_DifferentForceParentheses_StillStructurallyEqual()
        {
            // ForceParentheses is a display-only concern (core-domain.md 2.
            // docs this task's contract clarifies), not part of the tree's
            // meaning — two trees with the same operator/operands are the
            // same expression regardless of that flag.
            var a = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2));
            var b = new BinaryOperationNode(
                BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2), forceParentheses: true);

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Accept_DispatchesToVisitBinary()
        {
            var node = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2));
            var visitor = new RecordingVisitor();

            Assert.That(node.Accept(visitor), Is.EqualTo("binary:Addition"));
        }
    }

    [TestFixture]
    public class EqualityNodeTests
    {
        [Test]
        public void Constructor_NullLeft_Throws()
        {
            Assert.That(
                () => new EqualityNode(null, new ConstantNode(1)), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NullRight_Throws()
        {
            Assert.That(
                () => new EqualityNode(new ConstantNode(1), null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Constructor_NestedEqualityAsLeft_Throws()
        {
            var inner = new EqualityNode(new ConstantNode(1), new ConstantNode(1));

            Assert.That(
                () => new EqualityNode(inner, new ConstantNode(1)), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_NestedEqualityAsRight_Throws()
        {
            var inner = new EqualityNode(new ConstantNode(1), new ConstantNode(1));

            Assert.That(
                () => new EqualityNode(new ConstantNode(1), inner), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ToDisplayString_SimpleEquation_RendersBothSides()
        {
            // 7 + ? = 3 × 8 — math-engine.md 1./TESTING.md smoke value.
            var left = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new ConstantNode(8));
            var equation = new EqualityNode(left, right);

            Assert.That(equation.ToDisplayString(), Is.EqualTo("7 + ? = 3 × 8"));
        }

        [Test]
        public void ToDisplayString_EquationWithForcedParentheses_MatchesSpecExample()
        {
            // (? + 17) × 3 = 6 × (14 − 3) — math-engine.md 1./2. smoke value.
            var leftInner = new BinaryOperationNode(
                BinaryOperator.Addition, new UnknownNode(), new ConstantNode(17), forceParentheses: true);
            var left = new BinaryOperationNode(BinaryOperator.Multiplication, leftInner, new ConstantNode(3));

            var rightInner = new BinaryOperationNode(
                BinaryOperator.Subtraction, new ConstantNode(14), new ConstantNode(3), forceParentheses: true);
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(6), rightInner);

            var equation = new EqualityNode(left, right);

            Assert.That(
                equation.ToDisplayString(),
                Is.EqualTo("(? + 17) × 3 = 6 × (14 − 3)"));
        }

        [Test]
        public void Equals_SameShape_AreEqual()
        {
            var a = new EqualityNode(new ConstantNode(1), new ConstantNode(2));
            var b = new EqualityNode(new ConstantNode(1), new ConstantNode(2));

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Equals_DifferentSides_AreNotEqual()
        {
            var a = new EqualityNode(new ConstantNode(1), new ConstantNode(2));
            var b = new EqualityNode(new ConstantNode(1), new ConstantNode(3));

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Accept_DispatchesToVisitEquality()
        {
            var node = new EqualityNode(new ConstantNode(1), new ConstantNode(2));
            var visitor = new RecordingVisitor();

            Assert.That(node.Accept(visitor), Is.EqualTo("equality"));
        }
    }

    /// <summary>
    /// Minimal IExpressionVisitor used only to prove Accept dispatches to the
    /// matching Visit* overload for every node type — not a production type.
    /// </summary>
    internal sealed class RecordingVisitor : IExpressionVisitor<string>
    {
        public string VisitConstant(ConstantNode node) => "constant:" + node.Value;

        public string VisitUnknown(UnknownNode node) => "unknown:" + node.Symbol;

        public string VisitBinary(BinaryOperationNode node) => "binary:" + node.Operator;

        public string VisitEquality(EqualityNode node) => "equality";
    }
}
