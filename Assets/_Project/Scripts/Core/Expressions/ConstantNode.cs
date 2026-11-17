using System;
using System.Globalization;

namespace MathHorde.Core.Expressions
{
    /// <summary>
    /// A literal integer leaf (contract core-domain.md 2.). The MVP is
    /// integer-only arithmetic (spec 6.2) — no fractional/decimal literal
    /// exists in this hierarchy.
    /// </summary>
    public sealed class ConstantNode : ExpressionNode
    {
        public int Value { get; }

        public ConstantNode(int value)
        {
            Value = value;
        }

        public override TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            return visitor.VisitConstant(this);
        }

        public override string ToDisplayString() => Value.ToString(CultureInfo.InvariantCulture);

        public override bool Equals(object obj) => obj is ConstantNode other && Value == other.Value;

        public override int GetHashCode() => HashCode.Combine(typeof(ConstantNode), Value);
    }
}
