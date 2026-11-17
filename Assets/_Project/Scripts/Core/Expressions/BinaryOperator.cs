namespace MathHorde.Core.Expressions
{
    /// <summary>
    /// Supported binary operators for a math expression tree (contract
    /// core-domain.md 2., spec 6.1/6.2). The MVP is integer-only arithmetic.
    ///
    /// Explicit numeric values per the enum rule (core-domain.md 0.): a
    /// BinaryOperator value is embedded in persisted MathProblem/QuestionAttempt
    /// trails (Core.Progress) written by the Infrastructure persistence layer,
    /// so reordering would silently reinterpret existing saves/exports. New
    /// values go ONLY at the end, with the next free number.
    /// </summary>
    public enum BinaryOperator
    {
        Addition = 0,
        Subtraction = 1,
        Multiplication = 2,

        /// <summary>
        /// Integer division; the generator/validator (MathEngine, a different
        /// task's scope) enforce that the dividend is evenly divisible by the
        /// divisor — this node type itself does not evaluate anything.
        /// </summary>
        Division = 3,

        /// <summary>a % b, with b > 0; result is always &gt;= 0.</summary>
        Modulo = 4,

        /// <summary>
        /// Integer division with remainder: result is a (quotient, remainder)
        /// pair, not a scalar (see EvaluationResult in core-domain.md 3.). Per
        /// AST invariant 3 (core-domain.md 2.), a node using this operator may
        /// only be the tree's root or a direct child of an EqualityNode —
        /// BinaryOperationNode rejects it as one of its own operands.
        /// </summary>
        IntegerDivisionWithRemainder = 5
    }
}
