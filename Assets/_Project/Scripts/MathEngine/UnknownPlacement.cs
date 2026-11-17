namespace MathHorde.MathEngine
{
    /// <summary>
    /// Where <see cref="IExpressionGenerator"/> may place the single
    /// <see cref="Core.Expressions.UnknownNode"/> it introduces when
    /// <see cref="ExpressionGenerationRequest.IncludeUnknown"/> is true
    /// (contract math-engine.md 3.). "Left"/"Right" refer to the position
    /// of the leaf that becomes the unknown within the generated tree, read
    /// left-to-right in construction order — not to Core's AST-level
    /// Left/Right child naming of a single node.
    ///
    /// Not a persisted enum (it lives only in a transient generation
    /// request, never in a saved/exported file), so — unlike the
    /// persisted enums in core-domain.md 0. — explicit numeric values here
    /// are not a save-compatibility requirement. They are still spelled
    /// out because the contract (math-engine.md 3.) gives them literally.
    /// </summary>
    public enum UnknownPlacement
    {
        None = 0,
        LeftOperand = 1,
        RightOperand = 2,
        EitherSide = 3
    }
}
