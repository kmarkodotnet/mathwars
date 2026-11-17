using MathHorde.Core.Expressions;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Expressions
{
    // Table-driven ToDisplayString coverage (contract core-domain.md 2.
    // precedence table) using the exact literal examples named in the
    // F2-01 task brief. ExpressionNodeTests.cs already exercises the
    // underlying precedence RULES one case at a time; this file just pins
    // down the literal strings and a deeply nested tree. Zero UnityEngine
    // dependency — runs under Unity Test Runner and the dotnet mirror alike.
    [TestFixture]
    public class DisplayStringTests
    {
        [Test]
        public void MultiplicationOfParenthesizedAddition()
        {
            // (6 + 4) × 3
            var addition = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(6), new ConstantNode(4));
            var tree = new BinaryOperationNode(BinaryOperator.Multiplication, addition, new ConstantNode(3));

            Assert.That(tree.ToDisplayString(), Is.EqualTo("(6 + 4) × 3"));
        }

        [Test]
        public void AdditionOfMultiplication_NoParens()
        {
            // 6 + 4 × 3 — multiplication binds tighter, no parens needed.
            var multiplication = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(4), new ConstantNode(3));
            var tree = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(6), multiplication);

            Assert.That(tree.ToDisplayString(), Is.EqualTo("6 + 4 × 3"));
        }

        [Test]
        public void SubtractionOfSubtraction_RightChild_RequiresParens()
        {
            // 10 − (3 − 1)
            var inner = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(3), new ConstantNode(1));
            var tree = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(10), inner);

            Assert.That(tree.ToDisplayString(), Is.EqualTo("10 − (3 − 1)"));
        }

        [Test]
        public void SubtractionOfSubtraction_LeftChild_NoParens()
        {
            // 10 − 3 − 1 — left-associative chain at the same precedence.
            var inner = new BinaryOperationNode(BinaryOperator.Subtraction, new ConstantNode(10), new ConstantNode(3));
            var tree = new BinaryOperationNode(BinaryOperator.Subtraction, inner, new ConstantNode(1));

            Assert.That(tree.ToDisplayString(), Is.EqualTo("10 − 3 − 1"));
        }

        [Test]
        public void DivisionOfParenthesizedAddition()
        {
            // (2 + 3) ÷ 5
            var addition = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(2), new ConstantNode(3));
            var tree = new BinaryOperationNode(BinaryOperator.Division, addition, new ConstantNode(5));

            Assert.That(tree.ToDisplayString(), Is.EqualTo("(2 + 3) ÷ 5"));
        }

        [Test]
        public void Modulo_FlatPair()
        {
            // 17 mod 5
            var tree = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(17), new ConstantNode(5));

            Assert.That(tree.ToDisplayString(), Is.EqualTo("17 mod 5"));
        }

        [Test]
        public void MissingOperandEquation()
        {
            // 7 + ? = 3 × 8
            var left = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode());
            var right = new BinaryOperationNode(BinaryOperator.Multiplication, new ConstantNode(3), new ConstantNode(8));
            var equation = new EqualityNode(left, right);

            Assert.That(equation.ToDisplayString(), Is.EqualTo("7 + ? = 3 × 8"));
        }

        [Test]
        public void ForceParentheses_PrintsRedundantParens()
        {
            // A left child at the SAME precedence as its parent would print
            // with no parens by the precedence rule alone (see
            // ToDisplayString_LeftAssociativeChainAtSamePrecedence_NoParens
            // in ExpressionNodeTests.cs) — ForceParentheses overrides that
            // and prints the otherwise-redundant pair anyway.
            var forcedInner = new BinaryOperationNode(
                BinaryOperator.Multiplication, new ConstantNode(2), new ConstantNode(3), forceParentheses: true);
            var tree = new BinaryOperationNode(BinaryOperator.Multiplication, forcedInner, new ConstantNode(4));

            Assert.That(tree.ToDisplayString(), Is.EqualTo("(2 × 3) × 4"));
        }

        [Test]
        public void DeeplyNestedTree_RecursesCorrectlyThroughEveryLevel()
        {
            // (((2 + 3) × 4) − (7 mod 3)) ÷ 5
            // Built bottom-up so every level's parenthesization depends on
            // the previous level's result being correctly recursed into.
            var sum = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(2), new ConstantNode(3)); // 2 + 3
            var product = new BinaryOperationNode(BinaryOperator.Multiplication, sum, new ConstantNode(4)); // (2 + 3) × 4
            var remainder = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(7), new ConstantNode(3)); // 7 mod 3
            var difference = new BinaryOperationNode(BinaryOperator.Subtraction, product, remainder); // (2 + 3) × 4 − 7 mod 3
            var tree = new BinaryOperationNode(BinaryOperator.Division, difference, new ConstantNode(5)); // (... ) ÷ 5

            Assert.That(tree.ToDisplayString(), Is.EqualTo("((2 + 3) × 4 − 7 mod 3) ÷ 5"));
        }
    }
}
