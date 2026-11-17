namespace MathHorde.MathEngine
{
    /// <summary>
    /// Evaluation bounds and sign policy for <see cref="IExpressionEvaluator"/>
    /// (contract math-engine.md 1.). The MathEngine never reads
    /// ScriptableObject configuration directly (math-engine.md 0., P7) — every
    /// caller (ProblemGenerator, ProblemValidator, a future task's scope)
    /// builds this from the DifficultyProfile/request it already has.
    /// </summary>
    public sealed class EvaluationOptions
    {
        /// <summary>
        /// Whether a SUB-expression (any node that is not the overall
        /// evaluated root) may evaluate to a negative value. Checked at every
        /// non-root node that produces a numeric value.
        /// </summary>
        public bool AllowNegativeIntermediate { get; init; } = false;

        /// <summary>Whether the overall (root) evaluated result may be negative.</summary>
        public bool AllowNegativeResult { get; init; } = false;

        /// <summary>Inclusive lower bound for any node's value. Going below it fails with "OVERFLOW".</summary>
        public int MinValue { get; init; } = -9999;

        /// <summary>Inclusive upper bound for any node's value. Exceeding it fails with "OVERFLOW".</summary>
        public int MaxValue { get; init; } = 99999;

        /// <summary>The MVP default: no negative intermediates/results, range [-9999, 99999].</summary>
        public static EvaluationOptions Default { get; } = new EvaluationOptions();
    }
}
