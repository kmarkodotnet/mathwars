namespace MathHorde.Core.Expressions
{
    /// <summary>
    /// Generic visitor over the ExpressionNode hierarchy (contract
    /// core-domain.md 2.). Lets MathEngine (evaluator, display helpers,
    /// skill classifier, ...) and any future caller add new operations over
    /// the AST without modifying the node types themselves.
    /// </summary>
    public interface IExpressionVisitor<out TResult>
    {
        TResult VisitConstant(ConstantNode node);

        TResult VisitUnknown(UnknownNode node);

        TResult VisitBinary(BinaryOperationNode node);

        TResult VisitEquality(EqualityNode node);
    }
}
