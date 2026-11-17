using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Default <see cref="IExpressionGenerator"/> implementation (contract
    /// math-engine.md 3., spec 6.1/6.2, task F2-05). Stateless apart from
    /// the injected <see cref="IExpressionEvaluator"/>/<see cref="IEquationSolver"/>
    /// (P3, math-engine.md 0.), which it uses both to verify every
    /// intermediate step as the tree is built and to verify the final
    /// unknown-placement/solution (P5: "Validálás kötelező").
    ///
    /// DESIGN NOTES (flagged per the unity-csharp skill's "contract-szabály"
    /// — the contract text is followed as written; these are the concrete
    /// choices it leaves to the implementation):
    ///
    /// 1. CONSTRUCTION ORDER (rule 2/3): the tree is built strictly
    ///    left-to-right. The very first leaf and the first join's operator
    ///    are decided TOGETHER (see <see cref="TryBuildFirstJoin"/>) so that,
    ///    when that first operator is Division/Modulo/IntegerDivisionWithRemainder,
    ///    the divisor/quotient (or divisor/remainder) are chosen FIRST and the
    ///    dividend is DERIVED from them (checked multiplication) — exactly the
    ///    contract's literal description, "soha nem sorsolunk, majd eldobunk".
    ///    Every join AFTER the first has an already-fixed left value, so a
    ///    Division/Modulo there picks its divisor FROM THE SET OF ACTUAL
    ///    DIVISORS of that fixed value (<see cref="FindDivisorsInRange"/>) —
    ///    still construction/enumeration-based, never "guess a divisor, then
    ///    discard if it doesn't divide evenly".
    /// 2. IntegerDivisionWithRemainder is only ever offered as a candidate
    ///    operator for the LAST join of the tree (<see cref="BuildCandidateOperators"/>),
    ///    which makes its node the tree's ROOT — the only position AST
    ///    invariant 3 (core-domain.md 2.) allows a non-root
    ///    BinaryOperationNode to embed it would throw inside
    ///    BinaryOperationNode's own constructor, so this restriction is not
    ///    a contract narrowing, it is the only way to satisfy a hard
    ///    Core invariant from a chain-building generator.
    /// 3. PARENTHESES (rule 4/5): "(6+4)×3"-shaped grouping (spec 17.1) is
    ///    built as the tree's INITIAL seed — a small Addition/Subtraction/
    ///    Multiplication-only sub-tree with <c>ForceParentheses = true</c>
    ///    (<see cref="BuildParenGroup"/>) — rather than injected mid-chain,
    ///    because the canonical examples the contract names always show the
    ///    forced group as an OPERAND of a later operator, never containing
    ///    one. AllowParentheses == false therefore naturally yields the
    ///    flat, left-associative shape rule 4 requires: every join's right
    ///    operand is always a single ConstantNode (or, for Division/Modulo,
    ///    the divisor constant), never a nested BinaryOperationNode, so no
    ///    extra "flatten" step is needed. When RequireParentheses is true,
    ///    an attempt whose operand count is too small to fit even a
    ///    depth-1 group (needs operandCount &gt;= 3, see
    ///    <see cref="TryBuildTree"/>) simply fails and the outer retry loop
    ///    (rule 7) draws a fresh operand count on the next attempt.
    /// 4. UNKNOWN PLACEMENT/VERIFICATION (rule 6): rather than
    ///    pre-filtering which leaf positions are structurally eligible
    ///    (divisor of Division/Modulo, or anywhere under
    ///    IntegerDivisionWithRemainder, per <see cref="IEquationSolver"/>'s
    ///    own documented exclusions), the chosen leaf (leftmost/rightmost
    ///    per <see cref="UnknownPlacement"/>) is always tried, and
    ///    <see cref="IEquationSolver.Solve"/>'s own Result failure
    ///    ("UNSUPPORTED_SHAPE", "NOT_UNIQUE", ...) is what rejects an
    ///    infeasible placement — the outer retry loop then tries a freshly
    ///    built tree. This is simpler than duplicating the solver's shape
    ///    rules here and cannot drift out of sync with them.
    /// 5. GeneratedExpression.Value's two-line contract comment
    ///    ("ismeretlen nélküli érték / a megoldás") is terse enough to
    ///    read two ways; the interpretation used here (documented on
    ///    GeneratedExpression itself) is: Value is always the evaluated,
    ///    unknown-free tree's result (for IncludeUnknown, this is the
    ///    constant that ends up on the equation's KNOWN side), and
    ///    UnknownSolution is the separate, solver-verified value for the
    ///    unknown. Flagged for the architect, not silently picked as the
    ///    only possible reading.
    /// 6. GeneratedExpression.DetectedSkills is intentionally a BASIC-only
    ///    classification (root-level operator presence, not digit-size
    ///    distinctions) — see GeneratedExpression's own doc comment;
    ///    ISkillClassifier (task F2-06) is not a dependency of this task.
    /// </summary>
    public sealed class ExpressionGenerator : IExpressionGenerator
    {
        private readonly IExpressionEvaluator _evaluator;
        private readonly IEquationSolver _solver;

        public ExpressionGenerator(IExpressionEvaluator evaluator, IEquationSolver solver)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
        }

        public Result<GeneratedExpression> Generate(ExpressionGenerationRequest request)
        {
            ValidateRequest(request);
            var options = request.Evaluation ?? EvaluationOptions.Default;

            for (int attempt = 1; attempt <= request.MaxAttempts; attempt++)
            {
                if (!TryBuildTree(request, options, out ExpressionNode tree, out EvaluationResult value))
                {
                    continue;
                }

                // Rule 8: the final result must respect MaxResultValue — this is
                // a GENERATOR-level constraint on top of the evaluator's own
                // OVERFLOW check (which guards the int/EvaluationOptions range,
                // a different, usually much wider, bound), so a violation here
                // is reported as a retry, never as "OVERFLOW".
                if (value.Value > request.MaxResultValue)
                {
                    continue;
                }

                if (!request.IncludeUnknown)
                {
                    var skills = DetectBasicSkills(_evaluator, tree, includeUnknown: false);
                    return Result<GeneratedExpression>.Ok(new GeneratedExpression(tree, value, null, skills, attempt));
                }

                if (!TryPlaceUnknownAndSolve(request, options, tree, value.Value, out EqualityNode equation, out int solution))
                {
                    continue;
                }

                var equationSkills = DetectBasicSkills(_evaluator, equation, includeUnknown: true);
                return Result<GeneratedExpression>.Ok(
                    new GeneratedExpression(equation, new EvaluationResult(value.Value), solution, equationSkills, attempt));
            }

            return Result<GeneratedExpression>.Fail("GENERATION_EXHAUSTED");
        }

        // --- Request validation -----------------------------------------

        private static void ValidateRequest(ExpressionGenerationRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Random == null)
            {
                throw new ArgumentException("request.Random kötelező.", nameof(request));
            }

            if (request.AllowedOperators == null || request.AllowedOperators.Count == 0)
            {
                throw new ArgumentException("request.AllowedOperators nem lehet üres.", nameof(request));
            }

            if (request.MinOperandCount < 2)
            {
                throw new ArgumentException("request.MinOperandCount >= 2 kell legyen.", nameof(request));
            }

            if (request.MaxOperandCount < request.MinOperandCount)
            {
                throw new ArgumentException("request.MaxOperandCount nem lehet kisebb, mint MinOperandCount.", nameof(request));
            }

            if (request.MaxOperandCount > 5)
            {
                throw new ArgumentException("request.MaxOperandCount <= 5 (MVP).", nameof(request));
            }

            if (request.MaxOperandValue <= request.MinOperandValue)
            {
                throw new ArgumentException("request.MaxOperandValue > MinOperandValue kell legyen.", nameof(request));
            }

            if (request.MaxAttempts <= 0)
            {
                throw new ArgumentException("request.MaxAttempts > 0 kell legyen.", nameof(request));
            }

            if (!request.AllowParentheses && request.RequireParentheses)
            {
                throw new ArgumentException("RequireParentheses csak AllowParentheses == true esetén értelmezett.", nameof(request));
            }

            if (request.AllowParentheses && (request.MaxParenthesisDepth < 0 || request.MaxParenthesisDepth > 2))
            {
                throw new ArgumentException("request.MaxParenthesisDepth 0..2 kell legyen.", nameof(request));
            }

            if (request.IncludeUnknown && request.UnknownPlacement == UnknownPlacement.None)
            {
                throw new ArgumentException("IncludeUnknown == true esetén UnknownPlacement nem lehet None.", nameof(request));
            }
        }

        // --- Tree construction ------------------------------------------

        private bool TryBuildTree(ExpressionGenerationRequest request, EvaluationOptions options, out ExpressionNode tree, out EvaluationResult value)
        {
            tree = null;
            value = default;

            int operandCount = request.Random.Next(request.MinOperandCount, request.MaxOperandCount + 1);

            bool wantParens = request.AllowParentheses && (request.RequireParentheses || request.Random.NextDouble() < 0.5);

            ExpressionNode current = null;
            int? currentValue = null;
            int leavesUsed = 0;

            // Tracks whether a WEAK-precedence operator (Addition/Subtraction)
            // has been placed as the chain's own top-level operator yet. Every
            // join this method builds attaches the new operand to the RIGHT of
            // the already-accumulated `current` (never nested on the right) —
            // so the display-time parenthesization rule in
            // BinaryOperationNode.FormatOperand (core-domain.md 2.) only ever
            // needs an UNFORCED parenthesis around `current` when `current`'s
            // own top-level operator binds WEAKER than the new step's operator
            // (e.g. "(a+b)×c" — Addition accumulated, then Multiplication).
            // That would be an IMPLICIT, un-requested parenthesis — forbidden
            // outright when AllowParentheses == false, and liable to exceed
            // MaxParenthesisDepth by stacking on top of a deliberate
            // BuildParenGroup seed when AllowParentheses == true. Once a weak
            // operator has been used anywhere in the chain, every later step
            // is therefore restricted to weak operators too (BuildCandidateOperators) —
            // a same-or-weaker-only sequence, like "a×b + c − d", never needs
            // an implicit parenthesis (BinaryOperationNode.FormatOperand's own
            // documented rule: same precedence nests left without parens, and
            // a tight-precedence operand under a weak-precedence parent never
            // needs one either). This is a sound, if conservative, subset of
            // every parenthesis-free shape — it never produces an UNREQUESTED
            // parenthesis, even though it cannot produce a "weak then nested
            // tight" shape like "a + b×c" (that would need the tight sub-chain
            // built as a nested RIGHT-hand term, which this left-accumulating
            // chain-builder does not attempt, consistent with F2-05's declared
            // scope: construction-based validity + parentheses + unknown
            // insertion, not a full term/factor expression grammar).
            bool weakOperatorUsed = false;

            if (wantParens)
            {
                // Reserve at least one leaf for a join AFTER the group — a
                // group that swallows every operand would leave nothing to
                // attach it to, producing a pointless "parenthesized whole
                // expression" instead of the canonical "(a+b)×c" shape.
                int maxDepth = Math.Min(request.MaxParenthesisDepth, operandCount - 2);

                if (maxDepth < 1)
                {
                    if (request.RequireParentheses)
                    {
                        return false;
                    }

                    wantParens = false;
                }
                else
                {
                    int depth = request.Random.Next(1, maxDepth + 1);
                    var group = BuildParenGroup(request, options, depth);

                    if (group == null)
                    {
                        if (request.RequireParentheses)
                        {
                            return false;
                        }

                        wantParens = false;
                    }
                    else
                    {
                        current = group.Value.Node;
                        currentValue = group.Value.Value;
                        leavesUsed = depth + 1;
                        weakOperatorUsed = IsWeakOperator(((BinaryOperationNode)current).Operator);
                    }
                }
            }

            int joinsNeeded = current == null ? operandCount - 1 : operandCount - leavesUsed;

            for (int i = 0; i < joinsNeeded; i++)
            {
                bool isLastJoin = i == joinsNeeded - 1;
                bool currentFixed = current != null;

                var candidates = BuildCandidateOperators(request, currentFixed, currentValue, isLastJoin, weakOperatorUsed);
                if (candidates.Count == 0)
                {
                    return false;
                }

                var op = PickOperator(request, candidates);

                if (!currentFixed)
                {
                    if (!TryBuildFirstJoin(request, options, op, out ExpressionNode firstNode, out int firstValue))
                    {
                        return false;
                    }

                    current = firstNode;
                    currentValue = firstValue;
                }
                else
                {
                    if (!TryBuildFollowingJoin(request, options, op, current, currentValue.Value, out ExpressionNode nextNode, out int nextValue))
                    {
                        return false;
                    }

                    current = nextNode;
                    currentValue = nextValue;
                }

                weakOperatorUsed = weakOperatorUsed || IsWeakOperator(op);
            }

            if (current == null)
            {
                // Defensive fallback; MinOperandCount >= 2 (ValidateRequest) and
                // joinsNeeded >= 1 in that case make this unreachable in practice.
                current = new ConstantNode(RandomOperand(request));
            }

            var finalEval = _evaluator.Evaluate(current, options);
            if (!finalEval.IsSuccess)
            {
                return false;
            }

            tree = current;
            value = finalEval.Value;
            return true;
        }

        /// <summary>
        /// Builds a small, ForceParentheses-marked Addition/Subtraction/
        /// Multiplication-only sub-tree (design note 3) — the seed of the
        /// whole expression when parentheses are produced. Recurses for
        /// depth &gt; 1 (CL10's MaxParenthesisDepth == 2).
        /// </summary>
        private (ExpressionNode Node, int Value)? BuildParenGroup(ExpressionGenerationRequest request, EvaluationOptions options, int depth)
        {
            var simpleOps = new List<BinaryOperator>();
            foreach (var candidateOp in request.AllowedOperators)
            {
                if (candidateOp == BinaryOperator.Addition || candidateOp == BinaryOperator.Subtraction || candidateOp == BinaryOperator.Multiplication)
                {
                    simpleOps.Add(candidateOp);
                }
            }

            if (simpleOps.Count == 0)
            {
                return null;
            }

            ExpressionNode left;
            if (depth > 1)
            {
                var inner = BuildParenGroup(request, options, depth - 1);
                if (inner == null)
                {
                    return null;
                }

                left = inner.Value.Node;
            }
            else
            {
                left = new ConstantNode(RandomOperand(request));
            }

            var right = new ConstantNode(RandomOperand(request));
            var chosenOp = simpleOps[request.Random.Next(0, simpleOps.Count)];
            var node = new BinaryOperationNode(chosenOp, left, right, forceParentheses: true);

            var eval = _evaluator.Evaluate(node, options);
            if (!eval.IsSuccess)
            {
                return null;
            }

            return (node, eval.Value.Value);
        }

        /// <summary>
        /// The first join: the seed leaf and the first operator are decided
        /// TOGETHER (design note 1) so Division/Modulo/IntegerDivisionWithRemainder
        /// can derive their dividend from a freshly chosen divisor/quotient.
        /// </summary>
        private bool TryBuildFirstJoin(ExpressionGenerationRequest request, EvaluationOptions options, BinaryOperator op, out ExpressionNode node, out int value)
        {
            node = null;
            value = 0;

            switch (op)
            {
                case BinaryOperator.Addition:
                case BinaryOperator.Subtraction:
                case BinaryOperator.Multiplication:
                {
                    int a = RandomOperand(request);
                    int b = RandomOperand(request);
                    node = new BinaryOperationNode(op, new ConstantNode(a), new ConstantNode(b));
                    break;
                }

                case BinaryOperator.Division:
                {
                    if (!TryPickFreshDivisor(request, out int divisor))
                    {
                        return false;
                    }

                    int quotient = RandomOperand(request);
                    int dividend;
                    try
                    {
                        dividend = checked(divisor * quotient);
                    }
                    catch (OverflowException)
                    {
                        return false;
                    }

                    node = new BinaryOperationNode(op, new ConstantNode(dividend), new ConstantNode(divisor));
                    break;
                }

                case BinaryOperator.Modulo:
                {
                    if (!TryPickModuloDivisor(request, out int divisor))
                    {
                        return false;
                    }

                    // "osztandó pozitív" (rule 3).
                    int dividend = request.Random.Next(0, Math.Max(request.MaxOperandValue, 1) + 1);
                    node = new BinaryOperationNode(op, new ConstantNode(dividend), new ConstantNode(divisor));
                    break;
                }

                case BinaryOperator.IntegerDivisionWithRemainder:
                {
                    if (!TryPickFreshDivisor(request, out int divisor))
                    {
                        return false;
                    }

                    int quotient = RandomOperand(request);
                    int remainder = request.Random.Next(0, divisor);
                    int dividend;
                    try
                    {
                        dividend = checked(divisor * quotient + remainder);
                    }
                    catch (OverflowException)
                    {
                        return false;
                    }

                    node = new BinaryOperationNode(op, new ConstantNode(dividend), new ConstantNode(divisor));
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Ismeretlen BinaryOperator.");
            }

            var eval = _evaluator.Evaluate(node, options);
            if (!eval.IsSuccess)
            {
                return false;
            }

            value = eval.Value.Value;
            return true;
        }

        /// <summary>
        /// Any join after the first: <paramref name="current"/>/<paramref name="currentValue"/>
        /// are already fixed, so Division picks its divisor FROM the actual
        /// divisors of currentValue (design note 1) instead of drawing one
        /// blind.
        /// </summary>
        private bool TryBuildFollowingJoin(
            ExpressionGenerationRequest request, EvaluationOptions options, BinaryOperator op,
            ExpressionNode current, int currentValue, out ExpressionNode node, out int value)
        {
            node = null;
            value = 0;

            switch (op)
            {
                case BinaryOperator.Addition:
                case BinaryOperator.Subtraction:
                case BinaryOperator.Multiplication:
                {
                    int b = RandomOperand(request);
                    node = new BinaryOperationNode(op, current, new ConstantNode(b));
                    break;
                }

                case BinaryOperator.Division:
                {
                    if (!TryPickDivisorOf(request, currentValue, out int divisor))
                    {
                        return false;
                    }

                    node = new BinaryOperationNode(op, current, new ConstantNode(divisor));
                    break;
                }

                case BinaryOperator.Modulo:
                {
                    if (currentValue < 0)
                    {
                        return false;
                    }

                    if (!TryPickModuloDivisor(request, out int divisor))
                    {
                        return false;
                    }

                    node = new BinaryOperationNode(op, current, new ConstantNode(divisor));
                    break;
                }

                case BinaryOperator.IntegerDivisionWithRemainder:
                {
                    if (!TryPickFreshDivisor(request, out int divisor))
                    {
                        return false;
                    }

                    node = new BinaryOperationNode(op, current, new ConstantNode(divisor));
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Ismeretlen BinaryOperator.");
            }

            var eval = _evaluator.Evaluate(node, options);
            if (!eval.IsSuccess)
            {
                return false;
            }

            value = eval.Value.Value;
            return true;
        }

        // --- Operator selection ------------------------------------------

        private static List<BinaryOperator> BuildCandidateOperators(
            ExpressionGenerationRequest request, bool currentFixed, int? currentValue, bool isLastJoin, bool weakOperatorUsed)
        {
            var result = new List<BinaryOperator>();
            int minDivisorBound = Math.Max(1, request.MinOperandValue);
            int maxModDivisor = Math.Min(12, request.MaxOperandValue);

            foreach (var op in request.AllowedOperators)
            {
                // Once a weak (Addition/Subtraction) operator has been used as
                // the chain's top-level operator, no tight (Multiplication/
                // Division/Modulo/IntegerDivisionWithRemainder) operator may
                // follow it — see the "weakOperatorUsed" comment in
                // TryBuildTree for why this prevents an unrequested implicit
                // parenthesis.
                if (weakOperatorUsed && !IsWeakOperator(op))
                {
                    continue;
                }

                switch (op)
                {
                    case BinaryOperator.IntegerDivisionWithRemainder:
                        // AST invariant 3 (core-domain.md 2.) — only ever the root.
                        if (!isLastJoin)
                        {
                            continue;
                        }

                        if (minDivisorBound > request.MaxOperandValue)
                        {
                            continue;
                        }

                        break;

                    case BinaryOperator.Modulo:
                        if (maxModDivisor < 2)
                        {
                            continue;
                        }

                        if (currentFixed && currentValue.HasValue && currentValue.Value < 0)
                        {
                            continue;
                        }

                        break;

                    case BinaryOperator.Division:
                        if (minDivisorBound > request.MaxOperandValue)
                        {
                            continue;
                        }

                        if (currentFixed && currentValue.HasValue
                            && FindDivisorsInRange(currentValue.Value, minDivisorBound, request.MaxOperandValue).Count == 0)
                        {
                            continue;
                        }

                        break;
                }

                result.Add(op);
            }

            return result;
        }

        /// <summary>
        /// Addition/Subtraction are the WEAK-precedence operators (the
        /// contract's own "+ −" tier); Multiplication/Division/Modulo/
        /// IntegerDivisionWithRemainder are the TIGHT tier ("× ÷ mod ÷r").
        /// Only the ORDERING between these two tiers matters here (see
        /// "weakOperatorUsed" in TryBuildTree) — MathEngine deliberately does
        /// not re-import Core's own internal numeric precedence table
        /// (BinaryOperatorNotation is internal to Core.Expressions; see
        /// ExpressionEvaluator's own design note 2 for the established
        /// precedent of not duplicating it).
        /// </summary>
        private static bool IsWeakOperator(BinaryOperator op) =>
            op == BinaryOperator.Addition || op == BinaryOperator.Subtraction;

        private static BinaryOperator PickOperator(ExpressionGenerationRequest request, List<BinaryOperator> candidates)
        {
            if (request.PreferredSkills != null && request.PreferredSkills.Count > 0)
            {
                var preferred = new List<BinaryOperator>();
                foreach (var op in candidates)
                {
                    if (MapsToPreferredSkill(op, request.PreferredSkills))
                    {
                        preferred.Add(op);
                    }
                }

                if (preferred.Count > 0)
                {
                    return preferred[request.Random.Next(0, preferred.Count)];
                }
            }

            return candidates[request.Random.Next(0, candidates.Count)];
        }

        private static bool MapsToPreferredSkill(BinaryOperator op, IReadOnlyCollection<SkillTag> preferred)
        {
            bool wantsAddition = Contains(preferred, SkillTag.Addition);
            bool wantsSubtraction = Contains(preferred, SkillTag.Subtraction);
            bool wantsMultiplication = Contains(preferred, SkillTag.Multiplication);
            bool wantsDivision = Contains(preferred, SkillTag.Division);
            bool wantsRemainder = Contains(preferred, SkillTag.Remainder);

            switch (op)
            {
                case BinaryOperator.Addition: return wantsAddition;
                case BinaryOperator.Subtraction: return wantsSubtraction;
                case BinaryOperator.Multiplication: return wantsMultiplication;
                case BinaryOperator.Division: return wantsDivision;
                case BinaryOperator.Modulo: return wantsRemainder || wantsDivision;
                case BinaryOperator.IntegerDivisionWithRemainder: return wantsRemainder || wantsDivision;
                default: return false;
            }
        }

        private static bool Contains(IReadOnlyCollection<SkillTag> collection, SkillTag tag)
        {
            foreach (var item in collection)
            {
                if (item == tag)
                {
                    return true;
                }
            }

            return false;
        }

        // --- Divisor helpers ----------------------------------------------

        private static bool TryPickFreshDivisor(ExpressionGenerationRequest request, out int divisor)
        {
            int min = Math.Max(1, request.MinOperandValue);
            int max = request.MaxOperandValue;

            if (max < min)
            {
                divisor = 0;
                return false;
            }

            divisor = request.Random.Next(min, max + 1);
            return true;
        }

        private static bool TryPickModuloDivisor(ExpressionGenerationRequest request, out int divisor)
        {
            int max = Math.Min(12, request.MaxOperandValue);

            if (max < 2)
            {
                divisor = 0;
                return false;
            }

            divisor = request.Random.Next(2, max + 1);
            return true;
        }

        private static bool TryPickDivisorOf(ExpressionGenerationRequest request, int value, out int divisor)
        {
            int min = Math.Max(1, request.MinOperandValue);
            int max = request.MaxOperandValue;
            var divisors = FindDivisorsInRange(value, min, max);

            if (divisors.Count == 0)
            {
                divisor = 0;
                return false;
            }

            divisor = divisors[request.Random.Next(0, divisors.Count)];
            return true;
        }

        /// <summary>
        /// Every positive divisor of <paramref name="value"/> within
        /// [minDivisor, maxDivisor] — construction/enumeration, never a
        /// blind guess-then-discard (design note 1). value == 0 is
        /// divisible by every candidate in range.
        /// </summary>
        private static List<int> FindDivisorsInRange(int value, int minDivisor, int maxDivisor)
        {
            var result = new List<int>();

            if (maxDivisor < minDivisor)
            {
                return result;
            }

            if (value == 0)
            {
                for (int d = minDivisor; d <= maxDivisor; d++)
                {
                    result.Add(d);
                }

                return result;
            }

            int abs = Math.Abs(value);
            int upper = Math.Min(maxDivisor, abs);

            for (int d = minDivisor; d <= upper; d++)
            {
                if (abs % d == 0)
                {
                    result.Add(d);
                }
            }

            return result;
        }

        private static int RandomOperand(ExpressionGenerationRequest request) =>
            request.Random.Next(request.MinOperandValue, request.MaxOperandValue + 1);

        // --- Unknown placement (rule 6) -----------------------------------

        private bool TryPlaceUnknownAndSolve(
            ExpressionGenerationRequest request, EvaluationOptions options,
            ExpressionNode tree, int treeValue, out EqualityNode equation, out int solution)
        {
            equation = null;
            solution = 0;

            int leafCount = CountConstantLeaves(tree);
            if (leafCount == 0)
            {
                return false;
            }

            var placement = request.UnknownPlacement;
            if (placement == UnknownPlacement.EitherSide)
            {
                placement = request.Random.Next(0, 2) == 0 ? UnknownPlacement.LeftOperand : UnknownPlacement.RightOperand;
            }

            int chosenIndex = placement == UnknownPlacement.LeftOperand ? 0 : leafCount - 1;
            int counter = 0;
            ExpressionNode withUnknown = ReplaceConstantAt(tree, chosenIndex, ref counter);

            bool knownOnRight = request.Random.Next(0, 2) == 0;
            var candidateEquation = knownOnRight
                ? new EqualityNode(withUnknown, new ConstantNode(treeValue))
                : new EqualityNode(new ConstantNode(treeValue), withUnknown);

            var solveResult = _solver.Solve(candidateEquation, options);
            if (!solveResult.IsSuccess)
            {
                return false;
            }

            equation = candidateEquation;
            solution = solveResult.Value;
            return true;
        }

        private static int CountConstantLeaves(ExpressionNode node)
        {
            switch (node)
            {
                case ConstantNode:
                    return 1;

                case BinaryOperationNode binary:
                    return CountConstantLeaves(binary.Left) + CountConstantLeaves(binary.Right);

                default:
                    throw new ArgumentException($"Ismeretlen vagy nem-konstans ExpressionNode: {node.GetType()}.", nameof(node));
            }
        }

        /// <summary>
        /// Rebuilds the tree with the targetIndex-th ConstantNode leaf
        /// (0-indexed, left-to-right) replaced by a fresh UnknownNode.
        /// Unaffected sub-trees are structurally shared.
        /// </summary>
        private static ExpressionNode ReplaceConstantAt(ExpressionNode node, int targetIndex, ref int counter)
        {
            switch (node)
            {
                case ConstantNode constant:
                    if (counter == targetIndex)
                    {
                        counter++;
                        return new UnknownNode();
                    }

                    counter++;
                    return constant;

                case BinaryOperationNode binary:
                    var newLeft = ReplaceConstantAt(binary.Left, targetIndex, ref counter);
                    var newRight = ReplaceConstantAt(binary.Right, targetIndex, ref counter);

                    if (ReferenceEquals(newLeft, binary.Left) && ReferenceEquals(newRight, binary.Right))
                    {
                        return binary;
                    }

                    return new BinaryOperationNode(binary.Operator, newLeft, newRight, binary.ForceParentheses);

                default:
                    throw new ArgumentException($"Ismeretlen vagy nem-konstans ExpressionNode: {node.GetType()}.", nameof(node));
            }
        }

        // --- Basic skill detection (design note 6) ------------------------

        private static IReadOnlyList<SkillTag> DetectBasicSkills(IExpressionEvaluator evaluator, ExpressionNode root, bool includeUnknown)
        {
            var operators = new List<BinaryOperator>();
            CollectOperators(root, operators);

            bool hasAdd = false, hasSub = false, hasMul = false, hasDiv = false, hasMod = false, hasIntDivRem = false;
            var precedences = new HashSet<int>();

            foreach (var op in operators)
            {
                switch (op)
                {
                    case BinaryOperator.Addition: hasAdd = true; precedences.Add(1); break;
                    case BinaryOperator.Subtraction: hasSub = true; precedences.Add(1); break;
                    case BinaryOperator.Multiplication: hasMul = true; precedences.Add(2); break;
                    case BinaryOperator.Division: hasDiv = true; precedences.Add(2); break;
                    case BinaryOperator.Modulo: hasMod = true; precedences.Add(2); break;
                    case BinaryOperator.IntegerDivisionWithRemainder: hasIntDivRem = true; precedences.Add(2); break;
                }
            }

            var tags = new List<SkillTag>();
            if (hasAdd) tags.Add(SkillTag.Addition);
            if (hasSub) tags.Add(SkillTag.Subtraction);
            if (hasMul) tags.Add(SkillTag.Multiplication);
            if (hasDiv || hasMod || hasIntDivRem) tags.Add(SkillTag.Division);
            if (hasMod || hasIntDivRem) tags.Add(SkillTag.Remainder);

            if (precedences.Count >= 2)
            {
                tags.Add(SkillTag.OperatorPrecedence);
                tags.Add(SkillTag.MultiStepExpressions);
            }

            if (evaluator.MeasureParenthesisDepth(root) > 0)
            {
                tags.Add(SkillTag.Parentheses);
            }

            if (includeUnknown)
            {
                tags.Add(SkillTag.MissingOperand);
            }

            return tags;
        }

        private static void CollectOperators(ExpressionNode node, List<BinaryOperator> into)
        {
            switch (node)
            {
                case ConstantNode:
                case UnknownNode:
                    return;

                case BinaryOperationNode binary:
                    into.Add(binary.Operator);
                    CollectOperators(binary.Left, into);
                    CollectOperators(binary.Right, into);
                    return;

                case EqualityNode equality:
                    CollectOperators(equality.Left, into);
                    CollectOperators(equality.Right, into);
                    return;

                default:
                    throw new ArgumentException($"Ismeretlen ExpressionNode típus: {node.GetType()}.", nameof(node));
            }
        }
    }
}
