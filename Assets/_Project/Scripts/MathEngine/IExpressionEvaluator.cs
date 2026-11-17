using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Recursive, exception-free evaluator over the Core expression tree
    /// (contract math-engine.md 1., spec 6.1/6.2). P1 (math-engine.md 0.):
    /// no string parsing, no eval — every result comes from walking the
    /// <see cref="ExpressionNode"/> tree built by Core. P4: business-rule
    /// failures (division by zero, overflow, ...) are reported as a
    /// <see cref="Result{T}"/> error code, never as a thrown exception;
    /// exceptions here are reserved for genuine contract violations by the
    /// caller (null argument, or an <see cref="EqualityNode"/> passed where
    /// the contract explicitly says it is not accepted).
    /// </summary>
    public interface IExpressionEvaluator
    {
        /// <summary>
        /// Evaluates a tree containing no <see cref="UnknownNode"/>. Does NOT
        /// accept an <see cref="EqualityNode"/> (use <see cref="CheckEquality"/>
        /// for that).
        /// Error codes: "DIVISION_BY_ZERO", "NON_INTEGER_RESULT", "OVERFLOW",
        /// "UNKNOWN_IN_EXPRESSION", "NEGATIVE_RESULT_NOT_ALLOWED",
        /// "MODULO_NON_POSITIVE_DIVISOR".
        /// </summary>
        Result<EvaluationResult> Evaluate(ExpressionNode expression, EvaluationOptions options);

        /// <summary>Substitutes <paramref name="unknownValue"/> for every UnknownNode in the tree, then evaluates.</summary>
        Result<EvaluationResult> EvaluateWith(ExpressionNode expression, int unknownValue, EvaluationOptions options);

        /// <summary>Whether the equation holds for the given unknown value.</summary>
        Result<bool> CheckEquality(EqualityNode equation, int unknownValue, EvaluationOptions options);

        /// <summary>How many UnknownNode instances occur in the tree (ProblemValidator's UNKNOWN_COUNT_MISMATCH check).</summary>
        int CountUnknowns(ExpressionNode expression);

        /// <summary>
        /// Maximum nesting depth of parentheses that would actually appear in
        /// the tree's <see cref="ExpressionNode.ToDisplayString"/> rendering
        /// (0 = none). Used by the PARENTHESIS_DEPTH_EXCEEDED check.
        /// </summary>
        int MeasureParenthesisDepth(ExpressionNode expression);
    }
}
