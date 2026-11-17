using System;

namespace MathHorde.Core.Expressions
{
    /// <summary>
    /// Equation: Left = Right. The unknown may sit on either side (contract
    /// core-domain.md 2., spec 5.2 "7 + ? = 3 × 8"). Per AST invariant 2,
    /// only valid as the tree's ROOT — this type (like BinaryOperationNode)
    /// rejects an EqualityNode passed in as one of its own operands, so
    /// nesting one inside another is impossible to construct.
    /// </summary>
    public sealed class EqualityNode : ExpressionNode
    {
        public ExpressionNode Left { get; }
        public ExpressionNode Right { get; }

        public EqualityNode(ExpressionNode left, ExpressionNode right)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            if (left is EqualityNode || right is EqualityNode)
            {
                throw new ArgumentException(
                    "EqualityNode csak a fa gyökereként szerepelhet (AST-invariáns 2).");
            }

            Left = left;
            Right = right;
        }

        public override TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            return visitor.VisitEquality(this);
        }

        // Left/Right print their own forced/precedence parentheses; the
        // equation itself never wraps either side in an extra pair.
        public override string ToDisplayString() => Left.ToDisplayString() + " = " + Right.ToDisplayString();

        public override bool Equals(object obj) =>
            obj is EqualityNode other && Left.Equals(other.Left) && Right.Equals(other.Right);

        public override int GetHashCode() => HashCode.Combine(typeof(EqualityNode), Left, Right);
    }
}
