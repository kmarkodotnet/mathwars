using System;

namespace MathHorde.Core.Expressions
{
    /// <summary>
    /// A binary operation node (contract core-domain.md 2.). ToDisplayString
    /// only parenthesizes a child when the precedence table actually
    /// requires it for the result to read correctly — "(6 + 4) × 3" needs the
    /// parens, "6 + 4 × 3" must NOT get "6 + (4 × 3)" (the extra parens would
    /// misrepresent the expression as something other than what the tree
    /// says, even though the numeric result happens to match).
    /// </summary>
    public sealed class BinaryOperationNode : ExpressionNode
    {
        public BinaryOperator Operator { get; }
        public ExpressionNode Left { get; }
        public ExpressionNode Right { get; }

        /// <summary>
        /// True when the problem explicitly wants a visible, otherwise
        /// precedence-unnecessary parenthesis around THIS node in the
        /// display (e.g. a "zárójel-feladat" per math-engine.md 3. rule 5).
        /// ToDisplayString always honours it.
        /// </summary>
        public bool ForceParentheses { get; }

        public BinaryOperationNode(BinaryOperator op, ExpressionNode left, ExpressionNode right,
            bool forceParentheses = false)
        {
            if (left == null)
            {
                throw new ArgumentNullException(nameof(left));
            }

            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            // AST invariant 2 (core-domain.md 2.): EqualityNode may only be
            // the tree's root, never an operand of a BinaryOperationNode.
            if (left is EqualityNode || right is EqualityNode)
            {
                throw new ArgumentException(
                    "EqualityNode csak a fa gyökereként szerepelhet (AST-invariáns 2).");
            }

            // AST invariant 3 (core-domain.md 2.): an IntegerDivisionWithRemainder
            // result is a (quotient, remainder) pair, not a scalar, so it can
            // never feed into another operation as one of its operands.
            if (IsIntegerDivisionWithRemainder(left) || IsIntegerDivisionWithRemainder(right))
            {
                throw new ArgumentException(
                    "IntegerDivisionWithRemainder csak gyökér vagy EqualityNode közvetlen gyermeke lehet (AST-invariáns 3).");
            }

            Operator = op;
            Left = left;
            Right = right;
            ForceParentheses = forceParentheses;
        }

        private static bool IsIntegerDivisionWithRemainder(ExpressionNode node) =>
            node is BinaryOperationNode binaryNode
            && binaryNode.Operator == BinaryOperator.IntegerDivisionWithRemainder;

        public override TResult Accept<TResult>(IExpressionVisitor<TResult> visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            return visitor.VisitBinary(this);
        }

        public override string ToDisplayString()
        {
            var ownPrecedence = BinaryOperatorNotation.GetPrecedence(Operator);
            var left = FormatOperand(Left, ownPrecedence, isRightOperand: false);
            var right = FormatOperand(Right, ownPrecedence, isRightOperand: true);
            var body = left + " " + BinaryOperatorNotation.GetSymbol(Operator) + " " + right;
            return ForceParentheses ? "(" + body + ")" : body;
        }

        /// <summary>
        /// Parenthesizes a child's display text exactly when the precedence
        /// table requires it to preserve the tree's meaning, given both
        /// operator levels are left-associative (core-domain.md 2.
        /// precedence table):
        /// - left operand: needs parens only if it binds STRICTLY weaker
        ///   than the parent (same-precedence nests naturally to the left,
        ///   e.g. "a + b - c" already reads as "(a + b) - c").
        /// - right operand: needs parens if it binds weaker OR EQUAL,
        ///   because left-associativity means "a - b - c" is read as
        ///   "(a - b) - c" — if the tree actually means "a - (b - c)", the
        ///   parens are required to say so.
        /// A child that already forces its own parentheses prints them
        /// itself, so no double-wrapping is needed here.
        /// </summary>
        private static string FormatOperand(ExpressionNode operand, int parentPrecedence, bool isRightOperand)
        {
            var text = operand.ToDisplayString();

            if (!(operand is BinaryOperationNode binaryOperand) || binaryOperand.ForceParentheses)
            {
                return text;
            }

            // Lower GetPrecedence number = binds TIGHTER (× ÷ mod ÷r = 2 is
            // stronger than + − = 3), so "weaker" means a HIGHER number here.
            var operandPrecedence = BinaryOperatorNotation.GetPrecedence(binaryOperand.Operator);
            var needsParentheses = isRightOperand
                ? operandPrecedence >= parentPrecedence
                : operandPrecedence > parentPrecedence;

            return needsParentheses ? "(" + text + ")" : text;
        }

        // ForceParentheses is a DISPLAY-only concern (whether a problem wants
        // to show otherwise-redundant parens), not part of the expression's
        // meaning — two trees with the same operator/operands represent the
        // same math regardless of that flag, so it is deliberately excluded
        // from structural equality/hashing.
        public override bool Equals(object obj) =>
            obj is BinaryOperationNode other
            && Operator == other.Operator
            && Left.Equals(other.Left)
            && Right.Equals(other.Right);

        public override int GetHashCode() =>
            HashCode.Combine(typeof(BinaryOperationNode), Operator, Left, Right);
    }
}
