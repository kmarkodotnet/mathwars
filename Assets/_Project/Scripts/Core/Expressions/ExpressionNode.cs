namespace MathHorde.Core.Expressions
{
    /// <summary>
    /// Base type for every node in a math expression tree (spec 6.2, contract
    /// core-domain.md 2.).
    ///
    /// P1 (math-engine.md 0., KÖTELEZŐ): math expressions are NEVER plain
    /// strings and are NEVER evaluated via `eval`-style parsing — every
    /// expression the game generates, displays, or checks is a tree built
    /// from these node types, walked recursively (Accept/ToDisplayString and,
    /// in MathEngine, IExpressionEvaluator).
    ///
    /// Every node is immutable; "changing" an expression means building a new
    /// tree, never mutating an existing one in place (determinism, P3 in
    /// math-engine.md 0.).
    /// </summary>
    public abstract class ExpressionNode
    {
        /// <summary>Dispatches to the matching Visit* method — the only way to add a new operation over the tree without editing the node types.</summary>
        public abstract TResult Accept<TResult>(IExpressionVisitor<TResult> visitor);

        /// <summary>
        /// Precedence- and parenthesis-correct, human-readable form (e.g.
        /// "(6 + 4) × 3", not "6 + (4 × 3)" — a redundant/missing parenthesis
        /// here would mislead the child reading it, so this is never
        /// cosmetic). Unicode operator glyphs: + − × ÷ mod ÷r. Numbers use
        /// invariant culture formatting.
        /// </summary>
        public abstract string ToDisplayString();

        /// <summary>Structural equality (same shape/values), not reference equality.</summary>
        public abstract override bool Equals(object obj);

        public abstract override int GetHashCode();
    }
}
