using System;

namespace MathHorde.Core.Expressions
{
    /// <summary>
    /// The searched-for unknown ("?") — contract core-domain.md 2., spec 5.2
    /// ("7 + ? = 3 × 8"). AST invariant 4 (core-domain.md 2.) limits a tree to
    /// at most one UnknownNode; per the contract that multiplicity check is
    /// the ProblemValidator's job (MathEngine, a different task's scope), not
    /// this node type's — a single UnknownNode on its own is always valid in
    /// isolation, it is the surrounding tree that may or may not be.
    /// </summary>
    public sealed class UnknownNode : ExpressionNode
    {
        public string Symbol { get; }

        public UnknownNode(string symbol = "?")
        {
            if (string.IsNullOrEmpty(symbol))
            {
                throw new ArgumentException("Symbol nem lehet üres.", nameof(symbol));
            }

            Symbol = symbol;
        }

        public override TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            return visitor.VisitUnknown(this);
        }

        public override string ToDisplayString() => Symbol;

        public override bool Equals(object obj) => obj is UnknownNode other && Symbol == other.Symbol;

        public override int GetHashCode() => HashCode.Combine(typeof(UnknownNode), Symbol);
    }
}
