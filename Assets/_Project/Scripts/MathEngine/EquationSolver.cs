using System;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Default <see cref="IEquationSolver"/> implementation (contract
    /// math-engine.md 2., spec 5.2/6.1/6.2). Stateless (P3, math-engine.md
    /// 0.) apart from the injected <see cref="IExpressionEvaluator"/>, which
    /// it uses both to evaluate the known side of an equation and — always
    /// — to verify a found candidate before returning it (P5: "Validálás
    /// kötelező").
    ///
    /// DESIGN NOTES (flagged per the unity-csharp skill's "contract-szabály"
    /// — the contract text is followed as written, these are the concrete
    /// choices it leaves to the implementation):
    ///
    /// 1. The contract's exclusion is "nem Modulo jobb oldalán, nem osztó
    ///    pozícióban" (not on Modulo's right side, not in a divisor
    ///    position). Read literally this excludes the unknown as: (a) the
    ///    divisor of Division, (b) the divisor of Modulo (= its right
    ///    operand), and (c) anything under IntegerDivisionWithRemainder
    ///    (not a scalar, no inverse rule given) — all mapped here to
    ///    "UNSUPPORTED_SHAPE". It does NOT explicitly exclude the unknown as
    ///    Modulo's LEFT operand (the dividend). That shape is algebraically
    ///    solvable in principle, but never uniquely without a bound ("U mod
    ///    b = target" has the infinite solution set {target + k*b}) — Solve/
    ///    SolveForTarget (which take no search range) report this as
    ///    "NOT_UNIQUE" rather than silently picking one value or lumping it
    ///    in with "UNSUPPORTED_SHAPE". HasUniqueIntegerSolution (which DOES
    ///    take a range) resolves it properly via its band-check fallback.
    /// 2. Multiplication by a known zero coefficient is the other inherently
    ///    non-bijective shape: "U * 0 = target" has either no solution
    ///    (target != 0 ⇒ "NO_INTEGER_SOLUTION") or every integer as a
    ///    solution (target == 0 ⇒ "NOT_UNIQUE", same reasoning as note 1).
    /// 3. Every candidate produced by the inverse chain is, before being
    ///    returned, re-verified by substituting it back into the ORIGINAL
    ///    expression/equation via EvaluateWith/CheckEquality (contract:
    ///    "vissza kell helyettesíteni és CheckEquality-vel verifikálni").
    ///    This is not redundant with the algebra above: EvaluationOptions'
    ///    negative-intermediate/range rules are forward-evaluation concerns
    ///    the backward inversion does not itself enforce, so verification is
    ///    what actually rejects a candidate that only satisfies the bare
    ///    arithmetic but violates those options. Any verification failure
    ///    (either the evaluator itself failing, or the equality not holding)
    ///    is reported as "NO_INTEGER_SOLUTION" — the contract names exactly
    ///    five error codes for this interface, so evaluator-level codes
    ///    (e.g. "NEGATIVE_RESULT_NOT_ALLOWED", "OVERFLOW") are not leaked
    ///    through it as-is.
    /// 4. HasUniqueIntegerSolution's fast path relies on the fact that every
    ///    shape for which Solve/SolveForTarget return success is built from
    ///    operators that are each an injective (strictly monotonic) function
    ///    of the unknown sub-expression for a FIXED known operand (x+c, x-c,
    ///    c-x, c*x or x*c with c != 0, and their Division inverses) — and a
    ///    composition of injective functions is itself injective. A
    ///    successful Solve therefore names the UNIQUE integer solution over
    ///    all of ℤ, so it is automatically the unique solution of any
    ///    sub-range that contains it (no bounded re-scan is needed). Only
    ///    the genuinely non-injective shapes (note 1/2, surfaced as
    ///    "NOT_UNIQUE") need the bounded brute-force fallback, which is
    ///    exactly what "sávos ellenőrzés" (band-check) in the contract names.
    /// </summary>
    public sealed class EquationSolver : IEquationSolver
    {
        private readonly IExpressionEvaluator _evaluator;

        public EquationSolver(IExpressionEvaluator evaluator)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public Result<int> Solve(EqualityNode equation, EvaluationOptions options)
        {
            if (equation == null)
            {
                throw new ArgumentNullException(nameof(equation));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            int leftCount = _evaluator.CountUnknowns(equation.Left);
            int rightCount = _evaluator.CountUnknowns(equation.Right);
            int total = leftCount + rightCount;

            if (total == 0)
            {
                return Result<int>.Fail("NO_UNKNOWN");
            }

            if (total > 1)
            {
                return Result<int>.Fail("MULTIPLE_UNKNOWNS");
            }

            bool unknownIsOnLeft = leftCount > 0;
            ExpressionNode unknownSide = unknownIsOnLeft ? equation.Left : equation.Right;
            ExpressionNode knownSide = unknownIsOnLeft ? equation.Right : equation.Left;

            var knownResult = _evaluator.Evaluate(knownSide, options);
            if (!knownResult.IsSuccess)
            {
                return Result<int>.Fail(knownResult.ErrorCode);
            }

            var solveResult = SolveForTarget(unknownSide, knownResult.Value.Value, options);
            if (!solveResult.IsSuccess)
            {
                return solveResult;
            }

            int candidate = solveResult.Value;

            // Final cross-check against the WHOLE original equation (contract
            // note 3 above) — SolveForTarget already verified unknownSide in
            // isolation, this additionally re-confirms knownSide still agrees
            // (defensive against the EvaluationOptions divergence described
            // in note 3, e.g. if knownSide itself were order-sensitive).
            var checkResult = _evaluator.CheckEquality(equation, candidate, options);
            if (!checkResult.IsSuccess || !checkResult.Value)
            {
                return Result<int>.Fail("NO_INTEGER_SOLUTION");
            }

            return Result<int>.Ok(candidate);
        }

        public Result<int> SolveForTarget(ExpressionNode expressionWithUnknown, int targetValue, EvaluationOptions options)
        {
            if (expressionWithUnknown == null)
            {
                throw new ArgumentNullException(nameof(expressionWithUnknown));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (expressionWithUnknown is EqualityNode)
            {
                // Mirrors ExpressionEvaluator's established convention: an
                // EqualityNode is a contract violation here, not a business
                // failure — Solve(EqualityNode, ...) is the entry point for
                // full equations.
                throw new ArgumentException(
                    "EqualityNode-ot a SolveForTarget nem fogad el; használd a Solve-ot.",
                    nameof(expressionWithUnknown));
            }

            int unknownCount = _evaluator.CountUnknowns(expressionWithUnknown);
            if (unknownCount == 0)
            {
                return Result<int>.Fail("NO_UNKNOWN");
            }

            if (unknownCount > 1)
            {
                return Result<int>.Fail("MULTIPLE_UNKNOWNS");
            }

            var inverted = InvertToUnknown(expressionWithUnknown, targetValue, options);
            if (!inverted.IsSuccess)
            {
                return inverted;
            }

            int candidate = inverted.Value;

            // Mandatory verification (P5 / contract "vissza kell helyettesíteni
            // és CheckEquality-vel verifikálni" — this is the SolveForTarget
            // equivalent, since there is no EqualityNode here): substitute the
            // candidate back and confirm the WHOLE expression truly evaluates
            // to targetValue under the real EvaluationOptions rules.
            var verify = _evaluator.EvaluateWith(expressionWithUnknown, candidate, options);
            if (!verify.IsSuccess || verify.Value.Value != targetValue)
            {
                return Result<int>.Fail("NO_INTEGER_SOLUTION");
            }

            return Result<int>.Ok(candidate);
        }

        public bool HasUniqueIntegerSolution(EqualityNode equation, int searchMin, int searchMax, EvaluationOptions options)
        {
            if (equation == null)
            {
                throw new ArgumentNullException(nameof(equation));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (searchMin > searchMax)
            {
                throw new ArgumentException("searchMin nem lehet nagyobb, mint searchMax.", nameof(searchMin));
            }

            var solveResult = Solve(equation, options);
            if (solveResult.IsSuccess)
            {
                // Design note 4: a successful Solve names the unique integer
                // solution over all of ℤ, so no band-scan is needed — it
                // merely needs to fall inside the caller's range.
                int candidate = solveResult.Value;
                return candidate >= searchMin && candidate <= searchMax;
            }

            if (solveResult.ErrorCode != "NOT_UNIQUE")
            {
                // NO_UNKNOWN / MULTIPLE_UNKNOWNS / UNSUPPORTED_SHAPE / NO_INTEGER_SOLUTION
                // (or a bubbled-up evaluator error on the known side): none of
                // these describe "exactly one integer solution".
                return false;
            }

            // Genuinely non-injective shape (design notes 1/2) — fall back to
            // the bounded band-check the contract calls for, over the
            // caller-supplied (and therefore caller-bounded) range.
            int matches = 0;
            for (int candidate = searchMin; candidate <= searchMax; candidate++)
            {
                var check = _evaluator.CheckEquality(equation, candidate, options);

                if (check.IsSuccess && check.Value)
                {
                    matches++;
                    if (matches > 1)
                    {
                        return false;
                    }
                }
            }

            return matches == 1;
        }

        // --- Symbolic inverse chain -----------------------------------------

        /// <summary>
        /// Walks down to the single UnknownNode in <paramref name="node"/>,
        /// inverting each BinaryOperationNode's outer operator along the way
        /// so that, at every step, "the known side combined (via the inverse
        /// operator) with the running target" becomes the new target for the
        /// sub-expression that still contains the unknown.
        /// </summary>
        private Result<int> InvertToUnknown(ExpressionNode node, int target, EvaluationOptions options)
        {
            switch (node)
            {
                case UnknownNode:
                    return Result<int>.Ok(target);

                case BinaryOperationNode binary:
                    bool leftHasUnknown = _evaluator.CountUnknowns(binary.Left) > 0;

                    ExpressionNode knownChild = leftHasUnknown ? binary.Right : binary.Left;
                    ExpressionNode unknownChild = leftHasUnknown ? binary.Left : binary.Right;

                    var knownResult = _evaluator.Evaluate(knownChild, options);
                    if (!knownResult.IsSuccess)
                    {
                        return Result<int>.Fail(knownResult.ErrorCode);
                    }

                    var invertedTarget = InvertOperator(
                        binary.Operator, knownOnRight: leftHasUnknown, knownValue: knownResult.Value.Value, target: target);
                    if (!invertedTarget.IsSuccess)
                    {
                        return invertedTarget;
                    }

                    return InvertToUnknown(unknownChild, invertedTarget.Value, options);

                default:
                    // ConstantNode (no unknown) / EqualityNode: unreachable
                    // from a validated call site (Solve/SolveForTarget only
                    // ever recurse into a sub-tree already confirmed to
                    // contain exactly the one unknown), kept as a defensive
                    // guard against a future ExpressionNode subtype.
                    throw new ArgumentException($"Nem invertálható ExpressionNode ismeretlen nélkül: {node.GetType()}.", nameof(node));
            }
        }

        /// <summary>
        /// Given "unknownSide OP knownValue == target" (knownOnRight) or
        /// "knownValue OP unknownSide == target" (!knownOnRight), returns the
        /// value unknownSide itself must evaluate to.
        /// </summary>
        private static Result<int> InvertOperator(BinaryOperator op, bool knownOnRight, int knownValue, int target)
        {
            checked
            {
                try
                {
                    switch (op)
                    {
                        case BinaryOperator.Addition:
                            // x + b == target  ⇒  x = target - b  (symmetric either side)
                            return Result<int>.Ok(target - knownValue);

                        case BinaryOperator.Subtraction:
                            return knownOnRight
                                ? Result<int>.Ok(target + knownValue)   // x - b == target ⇒ x = target + b
                                : Result<int>.Ok(knownValue - target);  // b - x == target ⇒ x = b - target

                        case BinaryOperator.Multiplication:
                            if (knownValue == 0)
                            {
                                return target == 0 ? Result<int>.Fail("NOT_UNIQUE") : Result<int>.Fail("NO_INTEGER_SOLUTION");
                            }

                            if (target % knownValue != 0)
                            {
                                return Result<int>.Fail("NO_INTEGER_SOLUTION");
                            }

                            return Result<int>.Ok(target / knownValue);

                        case BinaryOperator.Division:
                            if (knownOnRight)
                            {
                                // x / b == target ⇒ x = target * b (b is the divisor, known and non-zero for a valid expression)
                                if (knownValue == 0)
                                {
                                    return Result<int>.Fail("NO_INTEGER_SOLUTION");
                                }

                                return Result<int>.Ok(target * knownValue);
                            }

                            // b / x == target: x is the DIVISOR — contract-excluded shape.
                            return Result<int>.Fail("UNSUPPORTED_SHAPE");

                        case BinaryOperator.Modulo:
                            if (knownOnRight)
                            {
                                // x mod b == target: infinitely many x (x = target + k*b) — not unique without a bound.
                                return Result<int>.Fail("NOT_UNIQUE");
                            }

                            // b mod x == target: x is the DIVISOR (Modulo's right side) — contract-excluded shape.
                            return Result<int>.Fail("UNSUPPORTED_SHAPE");

                        case BinaryOperator.IntegerDivisionWithRemainder:
                            // Not a scalar operation and no inverse rule is given by the contract.
                            return Result<int>.Fail("UNSUPPORTED_SHAPE");

                        default:
                            throw new ArgumentOutOfRangeException(nameof(op), op, "Ismeretlen BinaryOperator.");
                    }
                }
                catch (OverflowException)
                {
                    return Result<int>.Fail("NO_INTEGER_SOLUTION");
                }
            }
        }
    }
}
