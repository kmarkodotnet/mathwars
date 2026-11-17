using System;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Default <see cref="IExpressionEvaluator"/> implementation (contract
    /// math-engine.md 1., spec 6.1/6.2/17.1). Stateless (P3, math-engine.md
    /// 0.) — every input is a method parameter, so instances are trivially
    /// thread-safe and reusable.
    ///
    /// DESIGN NOTES (flagged for the architect/contract owner rather than
    /// silently decided, per the unity-csharp skill's "contract-szabály"):
    ///
    /// 1. The contract says Evaluate/EvaluateWith "EqualityNode-ot NEM fogad
    ///    el" but does not list a Result error code for that case. Core
    ///    already establishes the precedent that violating a structural AST
    ///    rule (e.g. nesting an EqualityNode as an operand, AST invariant 2)
    ///    throws ArgumentException rather than returning a business Result —
    ///    see BinaryOperationNode's/EqualityNode's constructors. This is
    ///    treated the same way here: passing an EqualityNode to Evaluate or
    ///    EvaluateWith is a caller/contract violation (P4, math-engine.md 0.:
    ///    "Kivétel csak szerződésszegésnél"), not a business-rule failure, so
    ///    it throws ArgumentException instead of using an invented error
    ///    code.
    ///
    /// 2. MeasureParenthesisDepth is implemented by scanning the already
    ///    contract-correct ExpressionNode.ToDisplayString() output for '('/')'
    ///    nesting, rather than re-deriving BinaryOperator precedence here.
    ///    The precedence table is internal to MathHorde.Core.Expressions
    ///    (BinaryOperatorNotation) and MathEngine has no InternalsVisibleTo
    ///    access to it; re-typing the same precedence table a second time in
    ///    this assembly would duplicate contract knowledge that could drift
    ///    out of sync with Core's actual rendering. Scanning the rendered
    ///    string instead reuses the single already-tested source of truth for
    ///    "where do parentheses actually appear" (both ForceParentheses- and
    ///    precedence-driven ones count, matching math-engine.md 3. rule 5).
    ///
    /// 3. The negative-value and range checks (EvaluationOptions) apply only
    ///    to the VALUE A BINARY OPERATION PRODUCES, never to a bare
    ///    ConstantNode/substituted UnknownNode leaf. This was tightened after
    ///    the 10 000-case seeded fuzz test (spec 17.1) caught the opposite,
    ///    overly strict design failing honestly-specified cases: e.g.
    ///    Modulo(5, -3) under default options was rejected with
    ///    "NEGATIVE_RESULT_NOT_ALLOWED" for the literal -3 OPERAND before the
    ///    Modulo-specific "MODULO_NON_POSITIVE_DIVISOR" rule ever ran, and
    ///    Modulo(-7, 3) — whose floor-mod result is +2, per spec always
    ///    non-negative — failed for the same reason despite its actual
    ///    result being perfectly valid. A literal operand is data already
    ///    present in the tree, not a "result" the contract's operator rule
    ///    table (math-engine.md 1.) is describing; only the table's listed
    ///    operator outputs are checked. AllowNegativeResult governs the
    ///    computed value of the outermost BinaryOperationNode passed to
    ///    Evaluate/EvaluateWith, or of either EqualityNode side in
    ///    CheckEquality; AllowNegativeIntermediate governs every
    ///    BinaryOperationNode result beneath that.
    /// </summary>
    public sealed class ExpressionEvaluator : IExpressionEvaluator
    {
        public Result<EvaluationResult> Evaluate(ExpressionNode expression, EvaluationOptions options)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (expression is EqualityNode)
            {
                throw new ArgumentException(
                    "EqualityNode-ot az Evaluate nem fogad el; használd a CheckEquality-t.",
                    nameof(expression));
            }

            return EvaluateNode(expression, unknownValue: null, options, isRoot: true);
        }

        public Result<EvaluationResult> EvaluateWith(ExpressionNode expression, int unknownValue, EvaluationOptions options)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (expression is EqualityNode)
            {
                throw new ArgumentException(
                    "EqualityNode-ot az EvaluateWith nem fogad el; használd a CheckEquality-t.",
                    nameof(expression));
            }

            return EvaluateNode(expression, unknownValue, options, isRoot: true);
        }

        public Result<bool> CheckEquality(EqualityNode equation, int unknownValue, EvaluationOptions options)
        {
            if (equation == null)
            {
                throw new ArgumentNullException(nameof(equation));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var leftResult = EvaluateNode(equation.Left, unknownValue, options, isRoot: true);
            if (!leftResult.IsSuccess)
            {
                return Result<bool>.Fail(leftResult.ErrorCode);
            }

            var rightResult = EvaluateNode(equation.Right, unknownValue, options, isRoot: true);
            if (!rightResult.IsSuccess)
            {
                return Result<bool>.Fail(rightResult.ErrorCode);
            }

            return Result<bool>.Ok(leftResult.Value.Equals(rightResult.Value));
        }

        public int CountUnknowns(ExpressionNode expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return CountUnknownsRecursive(expression);
        }

        public int MeasureParenthesisDepth(ExpressionNode expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            return MeasureDepthFromText(expression.ToDisplayString());
        }

        // --- Evaluate --------------------------------------------------

        private static Result<EvaluationResult> EvaluateNode(
            ExpressionNode node, int? unknownValue, EvaluationOptions options, bool isRoot)
        {
            switch (node)
            {
                case ConstantNode constant:
                    // A literal operand is data already present in the tree, not a
                    // computed "result" — EvaluationOptions' negative/range rules
                    // (see the class comment, note 3) apply only to the value a
                    // BinaryOperationNode itself produces, so a leaf passes through
                    // unchecked even when it is the entire tree (isRoot and all).
                    return Result<EvaluationResult>.Ok(new EvaluationResult(constant.Value));

                case UnknownNode:
                    if (unknownValue == null)
                    {
                        return Result<EvaluationResult>.Fail("UNKNOWN_IN_EXPRESSION");
                    }

                    return Result<EvaluationResult>.Ok(new EvaluationResult(unknownValue.Value));

                case BinaryOperationNode binary:
                    return EvaluateBinary(binary, unknownValue, options, isRoot);

                case EqualityNode:
                    // Unreachable via the public API: Evaluate/EvaluateWith reject an
                    // EqualityNode before recursing, and EqualityNode.Left/Right can
                    // never themselves be an EqualityNode (AST invariant 2). Kept as a
                    // defensive guard against a future ExpressionNode subtype being
                    // routed here by mistake.
                    throw new ArgumentException(
                        "EqualityNode csak a CheckEquality metóduson keresztül értékelhető ki.", nameof(node));

                default:
                    throw new ArgumentException($"Ismeretlen ExpressionNode típus: {node.GetType()}.", nameof(node));
            }
        }

        private static Result<EvaluationResult> EvaluateBinary(
            BinaryOperationNode node, int? unknownValue, EvaluationOptions options, bool isRoot)
        {
            // Children are evaluated as plain, non-root values: AST invariant 3
            // guarantees neither Left nor Right can itself be an
            // IntegerDivisionWithRemainder node, so their EvaluationResult.Remainder
            // is always null here.
            var leftResult = EvaluateNode(node.Left, unknownValue, options, isRoot: false);
            if (!leftResult.IsSuccess)
            {
                return leftResult;
            }

            var rightResult = EvaluateNode(node.Right, unknownValue, options, isRoot: false);
            if (!rightResult.IsSuccess)
            {
                return rightResult;
            }

            int a = leftResult.Value.Value;
            int b = rightResult.Value.Value;

            switch (node.Operator)
            {
                case BinaryOperator.Addition:
                    return ComputeScalar(a, b, static (x, y) => checked(x + y), options, isRoot);

                case BinaryOperator.Subtraction:
                    return ComputeScalar(a, b, static (x, y) => checked(x - y), options, isRoot);

                case BinaryOperator.Multiplication:
                    return ComputeScalar(a, b, static (x, y) => checked(x * y), options, isRoot);

                case BinaryOperator.Division:
                    if (b == 0)
                    {
                        return Result<EvaluationResult>.Fail("DIVISION_BY_ZERO");
                    }

                    if (a % b != 0)
                    {
                        return Result<EvaluationResult>.Fail("NON_INTEGER_RESULT");
                    }

                    return ComputeScalar(a, b, static (x, y) => x / y, options, isRoot);

                case BinaryOperator.Modulo:
                    if (b <= 0)
                    {
                        return Result<EvaluationResult>.Fail("MODULO_NON_POSITIVE_DIVISOR");
                    }

                    // Spec: "eredmény mindig >= 0" — floor-mod, not C#'s truncating %.
                    return ComputeScalar(a, b, static (x, y) => FloorMod(x, y), options, isRoot);

                case BinaryOperator.IntegerDivisionWithRemainder:
                    if (b == 0)
                    {
                        return Result<EvaluationResult>.Fail("DIVISION_BY_ZERO");
                    }

                    var (quotient, remainder) = FloorDivRem(a, b);
                    return ComputeScalarWithRemainder(quotient, remainder, options, isRoot);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(node), node.Operator, "Ismeretlen BinaryOperator.");
            }
        }

        private static Result<EvaluationResult> ComputeScalar(
            int a, int b, Func<int, int, int> compute, EvaluationOptions options, bool isRoot)
        {
            int raw;
            try
            {
                raw = compute(a, b);
            }
            catch (OverflowException)
            {
                return Result<EvaluationResult>.Fail("OVERFLOW");
            }

            return CheckNegativeAndRange(raw, options, isRoot);
        }

        private static Result<EvaluationResult> CheckNegativeAndRange(int value, EvaluationOptions options, bool isRoot)
        {
            if (value < options.MinValue || value > options.MaxValue)
            {
                return Result<EvaluationResult>.Fail("OVERFLOW");
            }

            bool negativeAllowed = isRoot ? options.AllowNegativeResult : options.AllowNegativeIntermediate;
            if (value < 0 && !negativeAllowed)
            {
                return Result<EvaluationResult>.Fail("NEGATIVE_RESULT_NOT_ALLOWED");
            }

            return Result<EvaluationResult>.Ok(new EvaluationResult(value));
        }

        private static Result<EvaluationResult> ComputeScalarWithRemainder(
            int quotient, int remainder, EvaluationOptions options, bool isRoot)
        {
            if (quotient < options.MinValue || quotient > options.MaxValue)
            {
                return Result<EvaluationResult>.Fail("OVERFLOW");
            }

            bool negativeAllowed = isRoot ? options.AllowNegativeResult : options.AllowNegativeIntermediate;
            if (quotient < 0 && !negativeAllowed)
            {
                return Result<EvaluationResult>.Fail("NEGATIVE_RESULT_NOT_ALLOWED");
            }

            // EvaluationResult's own constructor already guards Remainder >= 0
            // (core-domain.md 3.); FloorDivRem guarantees that, so this never throws.
            return Result<EvaluationResult>.Ok(new EvaluationResult(quotient, remainder));
        }

        /// <summary>a mod b with b &gt; 0, result always in [0, b-1] (never C#'s sign-of-dividend %).</summary>
        private static int FloorMod(int a, int b)
        {
            int r = a % b;
            return r < 0 ? r + b : r;
        }

        /// <summary>
        /// Quotient/remainder pair with remainder always in [0, |b|-1] (floor
        /// division), so a == quotient * b + remainder exactly, matching
        /// EvaluationResult's "Remainder always &gt;= 0" invariant even for a
        /// negative dividend (core-domain.md 3.).
        /// </summary>
        private static (int Quotient, int Remainder) FloorDivRem(int a, int b)
        {
            int bAbs = Math.Abs(b);
            int rawMod = a % bAbs;
            int remainder = rawMod < 0 ? rawMod + bAbs : rawMod;
            int quotient = (a - remainder) / b;
            return (quotient, remainder);
        }

        // --- CountUnknowns ----------------------------------------------

        private static int CountUnknownsRecursive(ExpressionNode node)
        {
            switch (node)
            {
                case UnknownNode:
                    return 1;

                case ConstantNode:
                    return 0;

                case BinaryOperationNode binary:
                    return CountUnknownsRecursive(binary.Left) + CountUnknownsRecursive(binary.Right);

                case EqualityNode equality:
                    return CountUnknownsRecursive(equality.Left) + CountUnknownsRecursive(equality.Right);

                default:
                    throw new ArgumentException($"Ismeretlen ExpressionNode típus: {node.GetType()}.", nameof(node));
            }
        }

        // --- MeasureParenthesisDepth --------------------------------------

        private static int MeasureDepthFromText(string text)
        {
            int depth = 0;
            int maxDepth = 0;
            foreach (char c in text)
            {
                if (c == '(')
                {
                    depth++;
                    if (depth > maxDepth)
                    {
                        maxDepth = depth;
                    }
                }
                else if (c == ')')
                {
                    depth--;
                }
            }

            return maxDepth;
        }
    }
}
