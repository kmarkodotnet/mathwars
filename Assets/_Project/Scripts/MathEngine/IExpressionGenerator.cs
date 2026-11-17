using MathHorde.Core.Common;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Builds a valid, evaluable <see cref="Core.Expressions.ExpressionNode"/>
    /// tree from scratch under the given constraints (contract
    /// math-engine.md 3., spec 6.1/6.2, task F2-05).
    ///
    /// P5 (math-engine.md 0., "Validálás kötelező"): the result is
    /// GUARANTEED evaluable — no division by zero, no non-integer
    /// division/modulo result, no overflow, and (when
    /// <see cref="ExpressionGenerationRequest.IncludeUnknown"/>) a verified
    /// unique integer solution. This is achieved by CONSTRUCTION (rule 2 of
    /// the contract: divisor/quotient are chosen first, the dividend is
    /// derived from them — "soha nem sorsolunk, majd eldobunk"), not by
    /// generate-then-reject on a single random guess; the only form of
    /// "retry" this interface performs is retrying an ENTIRE candidate
    /// expression up to <see cref="ExpressionGenerationRequest.MaxAttempts"/>
    /// times when the fully-built candidate still violates a RANGE/
    /// uniqueness constraint that could only be known after the whole tree
    /// was assembled (e.g. <see cref="ExpressionGenerationRequest.MaxResultValue"/>,
    /// or an UnknownNode placement that the <see cref="IEquationSolver"/>
    /// cannot uniquely solve).
    /// </summary>
    public interface IExpressionGenerator
    {
        /// <summary>
        /// Generates one <see cref="GeneratedExpression"/> matching
        /// <paramref name="request"/>. Error codes: "GENERATION_EXHAUSTED"
        /// (every attempt up to <see cref="ExpressionGenerationRequest.MaxAttempts"/>
        /// failed a post-construction check).
        /// </summary>
        Result<GeneratedExpression> Generate(ExpressionGenerationRequest request);
    }
}
