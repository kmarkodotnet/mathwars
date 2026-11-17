using MathHorde.Core.Expressions;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Expressions
{
    // Structural equality coverage for the ExpressionNode hierarchy
    // (contract core-domain.md 2.: "Strukturális egyenlőség, nem
    // referencia") that goes beyond the single-level cases already in
    // ExpressionNodeTests.cs — deep trees, cross-type comparisons, null/
    // unrelated-type handling, and operand-order sensitivity. Zero
    // UnityEngine dependency.
    [TestFixture]
    public class ExpressionEqualityTests
    {
        private static ExpressionNode BuildSampleTree(int rightLeafValue)
        {
            // (2 + 3) × (7 mod rightLeafValue)
            var sum = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(2), new ConstantNode(3));
            var modulo = new BinaryOperationNode(BinaryOperator.Modulo, new ConstantNode(7), new ConstantNode(rightLeafValue));
            return new BinaryOperationNode(BinaryOperator.Multiplication, sum, modulo);
        }

        [Test]
        public void DeepTree_SameShapeAndValues_AreEqualAndHashMatches()
        {
            var a = BuildSampleTree(4);
            var b = BuildSampleTree(4);

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void DeepTree_DifferentDeeplyNestedLeaf_AreNotEqual()
        {
            // Only a leaf four levels down differs — equality must recurse
            // all the way down, not stop at the top-level shape.
            var a = BuildSampleTree(4);
            var b = BuildSampleTree(5);

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void BinaryOperationNode_OperandOrderMatters_EvenForSameLeafValues()
        {
            // Structural equality is NOT the same as mathematical/commutative
            // equivalence: Add(1, 2) and Add(2, 1) are different trees.
            var a = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2));
            var b = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(2), new ConstantNode(1));

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void EqualityNode_SideOrderMatters()
        {
            var a = new EqualityNode(new ConstantNode(1), new ConstantNode(2));
            var b = new EqualityNode(new ConstantNode(2), new ConstantNode(1));

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void EqualityNode_VsBinaryOperationNode_SameOperands_AreNotEqual()
        {
            // Different node TYPES must never compare equal, regardless of
            // how similar their operands are.
            var equality = new EqualityNode(new ConstantNode(1), new ConstantNode(2));
            var binary = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2));

            Assert.That(equality.Equals(binary), Is.False);
            Assert.That(binary.Equals(equality), Is.False);
        }

        [TestCase]
        public void Equals_Null_ReturnsFalse_ForEveryNodeType()
        {
            Assert.That(new ConstantNode(1).Equals(null), Is.False);
            Assert.That(new UnknownNode().Equals(null), Is.False);
            Assert.That(
                new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2)).Equals(null),
                Is.False);
            Assert.That(new EqualityNode(new ConstantNode(1), new ConstantNode(2)).Equals(null), Is.False);
        }

        [Test]
        public void Equals_UnrelatedType_ReturnsFalse()
        {
            Assert.That(new ConstantNode(1).Equals("1"), Is.False);
            Assert.That(
                new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(1), new ConstantNode(2)).Equals(42),
                Is.False);
        }

        [Test]
        public void ForceParentheses_NeverAffectsEqualityEvenDeepInATree()
        {
            // The ForceParentheses flag set anywhere inside a tree must not
            // change the outer tree's structural equality against an
            // otherwise-identical tree without it (core-domain.md 2.: it is
            // a display-only property, see BinaryOperationNode.Equals).
            var leftSum = new BinaryOperationNode(
                BinaryOperator.Addition, new ConstantNode(2), new ConstantNode(3), forceParentheses: true);
            var a = new BinaryOperationNode(BinaryOperator.Multiplication, leftSum, new ConstantNode(4));

            var rightSum = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(2), new ConstantNode(3));
            var b = new BinaryOperationNode(BinaryOperator.Multiplication, rightSum, new ConstantNode(4));

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }
}
