using MathHorde.Core.Common;
using MathHorde.Core.Expressions;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Solves an equation that contains exactly one <see cref="UnknownNode"/>
    /// for an integer value (contract math-engine.md 2., spec 5.2
    /// "7 + ? = 3 × 8", EquationSolving/MissingOperand problem types).
    ///
    /// MVP scope (contract math-engine.md 2., "Megvalósítási elvárás"): the
    /// unknown occurs LINEARLY — exactly once, never on the right-hand
    /// (divisor) side of <see cref="BinaryOperator.Modulo"/>, never in a
    /// divisor position. Such shapes are solved with a symbolic inverse
    /// chain (inverting the outermost operator and stepping inward), then
    /// the found value is substituted back and verified with
    /// <see cref="IExpressionEvaluator.CheckEquality"/>/<see cref="IExpressionEvaluator.EvaluateWith"/>
    /// — a candidate is only ever returned once that verification succeeds.
    /// </summary>
    public interface IEquationSolver
    {
        /// <summary>
        /// Solves the equation for its single unknown.
        /// E.g. 7 + ? = 3 × 8 ⇒ 17; (? + 17) × 3 = 6 × (14 − 3) ⇒ 5.
        /// Error codes: "NO_UNKNOWN", "MULTIPLE_UNKNOWNS", "NO_INTEGER_SOLUTION",
        ///              "NOT_UNIQUE", "UNSUPPORTED_SHAPE".
        /// </summary>
        Result<int> Solve(EqualityNode equation, EvaluationOptions options);

        /// <summary>
        /// Expression + target value form (expr = target), for the
        /// MissingOperand problem type. Same error codes as <see cref="Solve"/>.
        /// </summary>
        Result<int> SolveForTarget(ExpressionNode expressionWithUnknown, int targetValue, EvaluationOptions options);

        /// <summary>
        /// Uniqueness check: is there EXACTLY one integer solution in the
        /// given (inclusive) range? Implementation: verifies the solver's
        /// result, falling back to a bounded band-check for shapes whose
        /// inverse is not a bijection (e.g. multiplication by zero, or the
        /// unknown as a Modulo dividend) per math-engine.md 2.
        /// </summary>
        bool HasUniqueIntegerSolution(EqualityNode equation, int searchMin, int searchMax, EvaluationOptions options);
    }
}
