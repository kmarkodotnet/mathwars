using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Default <see cref="IDistractorGenerator"/> implementation (contract
    /// math-engine.md 4., spec 6.4, task F2-07). Stateless (P3, math-engine.md
    /// 0.) — every input is a method parameter.
    ///
    /// DESIGN NOTES (flagged per the unity-csharp skill's "contract-szabály"
    /// — the contract text is followed as written; these are the concrete
    /// choices it leaves to the implementation):
    ///
    /// 1. EFFECTIVE ROOT. Several models key off "a gyökér operátor"
    ///    (NeighbourInTimesTable, CarryError, SignError,
    ///    ReverseCalculationError, the Modulo half of RemainderMisread).
    ///    When <see cref="DistractorRequest.Expression"/> is an
    ///    <see cref="EqualityNode"/> (a MissingOperand/EquationSolving
    ///    problem), "the root" means the side that does NOT contain the
    ///    <see cref="UnknownNode"/> — the only side with a concrete shape to
    ///    perturb. This mirrors the precedent <c>SkillClassifier.
    ///    UnwrapRootForErrorChecks</c> (task F2-06) already established for
    ///    the same situation; re-implemented locally here (private, no
    ///    cross-class access) rather than shared, consistent with every
    ///    other MathEngine class being self-contained (P3).
    /// 2. OperatorPrecedenceError and IgnoredParentheses are DIFFERENT
    ///    mistakes, not the same value under two labels (an earlier draft of
    ///    this file conflated them — caught by hand-worked examples before
    ///    any test ran): "8+4×6−7" (no parens) read OperatorPrecedenceError-
    ///    style is a purely NAIVE left-to-right scan ignoring precedence
    ///    entirely — ((8+4)×6)−7 = 65, the canonical spec 17.1 example.
    ///    "(6+4)×3" read IgnoredParentheses-style is NOT that: a student who
    ///    drops only the VISIBLE grouping still applies normal × before +
    ///    — i.e. "6+4×3" read with STANDARD precedence = 6+(4×3) = 18, not
    ///    the naive 30-ignoring-everything value (which here would
    ///    coincidentally equal the correct answer, since the forced group is
    ///    already the left-most term — naive-left-to-right can never stand
    ///    in for "ignore parens, keep precedence"). So two separate value
    ///    functions exist (<see cref="TryNaiveLeftToRightValue"/> and
    ///    <see cref="TryIgnoreParenthesesValue"/>), and the two models are
    ///    mutually exclusive per expression the same way SkillClassifier.
    ///    ClassifyError treats them: IgnoredParentheses applies whenever the
    ///    effective root's display contains a parenthesis (this contract
    ///    section's own "van zárójel" column, broader than SkillClassifier's
    ///    ForceParentheses-only check — the two components solve different
    ///    problems and are not required to share that narrower nuance);
    ///    OperatorPrecedenceError only when there is NO parenthesis but
    ///    &gt;= 2 precedence tiers are mixed.
    /// 3. SCALAR-ONLY MODELS. OnesDigitError, TensDigitError, CarryError,
    ///    SignError and ReverseCalculationError all assume a plain scalar
    ///    <see cref="EvaluationResult"/> (<c>Remainder == null</c>). The
    ///    contract does not spell out what "ones digit" or "a−b vs b−a"
    ///    would even mean for a (quotient, remainder) pair display, so
    ///    these models simply do not apply to one — RemainderMisread (which
    ///    IS defined for that shape by rule 3) is the only model offered
    ///    there, alongside OnesDigitError/TensDigitError's own scalar guard.
    /// 4. FALLBACK LABEL. <see cref="Distractor.ErrorModel"/> is a
    ///    non-nullable <see cref="ErrorCategory"/>, but the contract's rule
    ///    1 fallback ("correct ± k") is not tied to any of the 9 named
    ///    models. <see cref="ErrorCategory.Unclassified"/> — already
    ///    documented on the enum itself as the catch-all for "didn't match
    ///    a recognized pattern" — is used here; flagged for the architect/
    ///    contract owner rather than inventing an unlisted 10th model.
    /// 5. MODEL TRY ORDER is the contract table's own row order (rule 5
    ///    requires only that the SAME seed produce the SAME list in the SAME
    ///    order, not that the order itself be randomized); only the
    ///    SPECIFIC VALUE each model picks when it offers more than one
    ///    variant (e.g. NeighbourInTimesTable's 4 candidates, OnesDigitError's
    ///    6 deltas) is shuffled via <see cref="DistractorRequest.Random"/>,
    ///    so two different seeds on the same expression do not always surface
    ///    the exact same distractor value.
    /// </summary>
    public sealed class DistractorGenerator : IDistractorGenerator
    {
        private const string FallbackExplanationKey = "distractor.generic_offset";

        public Result<IReadOnlyList<Distractor>> Generate(DistractorRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Random == null)
            {
                throw new ArgumentException("A Random mező kötelező.", nameof(request));
            }

            if (request.Expression == null)
            {
                throw new ArgumentException("Az Expression mező kötelező.", nameof(request));
            }

            if (request.Count < 1 || request.Count > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(request), request.Count, "Count 1..3 lehet.");
            }

            if (request.MinValue > request.MaxValue)
            {
                throw new ArgumentException("MinValue nem lehet nagyobb MaxValue-nál.", nameof(request));
            }

            var effectiveRoot = GetEffectiveRoot(request.Expression);
            var applicableModels = DetermineApplicableModels(effectiveRoot, request.CorrectAnswer);
            var modelOrder = BuildModelOrder(request.PreferredModels, applicableModels);

            var used = new HashSet<EvaluationResult> { request.CorrectAnswer };
            var distractors = new List<Distractor>();

            foreach (var model in modelOrder)
            {
                if (distractors.Count >= request.Count)
                {
                    break;
                }

                if (TryGenerateCandidate(model, effectiveRoot, request.CorrectAnswer, request, used, out var candidate))
                {
                    used.Add(candidate);
                    distractors.Add(new Distractor(candidate, model, ExplanationKeyFor(model)));
                }
            }

            if (distractors.Count < request.Count && !TryFillWithFallback(request, used, distractors))
            {
                return Result<IReadOnlyList<Distractor>>.Fail("DISTRACTOR_EXHAUSTED");
            }

            return Result<IReadOnlyList<Distractor>>.Ok(distractors);
        }

        // --- Model applicability (math-engine.md 4. table, "Mikor alkalmazható") ---

        private static List<ErrorCategory> DetermineApplicableModels(ExpressionNode effectiveRoot, EvaluationResult correct)
        {
            var models = new List<ErrorCategory>();
            bool isScalar = !correct.Remainder.HasValue;

            var rootBinary = effectiveRoot as BinaryOperationNode;
            bool rootHasConstants = rootBinary != null
                && TryGetConstantValue(rootBinary.Left, out _)
                && TryGetConstantValue(rootBinary.Right, out _);

            if (rootHasConstants && rootBinary.Operator == BinaryOperator.Multiplication)
            {
                models.Add(ErrorCategory.NeighbourInTimesTable);
            }

            if (isScalar)
            {
                models.Add(ErrorCategory.OnesDigitError);
            }

            if (isScalar && correct.Value >= 10)
            {
                models.Add(ErrorCategory.TensDigitError);
            }

            if (isScalar && rootHasConstants && rootBinary.Operator == BinaryOperator.Addition
                && TryGetConstantValue(rootBinary.Left, out int carryA) && TryGetConstantValue(rootBinary.Right, out int carryB)
                && carryA >= 0 && carryB >= 0 && OnesDigit(carryA) + OnesDigit(carryB) >= 10)
            {
                models.Add(ErrorCategory.CarryError);
            }

            bool hasParens = ContainsDisplayedParenthesis(effectiveRoot);
            if (hasParens)
            {
                if (TryIgnoreParenthesesValue(effectiveRoot, out _))
                {
                    models.Add(ErrorCategory.IgnoredParentheses);
                }
            }
            else if (CountPrecedenceTiers(effectiveRoot) >= 2 && TryNaiveLeftToRightValue(effectiveRoot, out _))
            {
                models.Add(ErrorCategory.OperatorPrecedenceError);
            }

            bool remainderShape = correct.Remainder.HasValue
                || (rootHasConstants && rootBinary.Operator == BinaryOperator.Modulo);
            if (remainderShape)
            {
                models.Add(ErrorCategory.RemainderMisread);
            }

            if (isScalar && rootHasConstants
                && (rootBinary.Operator == BinaryOperator.Addition || rootBinary.Operator == BinaryOperator.Subtraction))
            {
                models.Add(ErrorCategory.SignError);
            }

            if (isScalar && rootHasConstants
                && (rootBinary.Operator == BinaryOperator.Subtraction || rootBinary.Operator == BinaryOperator.Division))
            {
                models.Add(ErrorCategory.ReverseCalculationError);
            }

            return models;
        }

        private static List<ErrorCategory> BuildModelOrder(IReadOnlyCollection<ErrorCategory> preferred, List<ErrorCategory> applicable)
        {
            if (preferred == null || preferred.Count == 0)
            {
                return applicable;
            }

            var ordered = new List<ErrorCategory>();
            foreach (var model in preferred)
            {
                if (applicable.Contains(model) && !ordered.Contains(model))
                {
                    ordered.Add(model);
                }
            }

            foreach (var model in applicable)
            {
                if (!ordered.Contains(model))
                {
                    ordered.Add(model);
                }
            }

            return ordered;
        }

        // --- Per-model candidate generation ---------------------------------

        private static bool TryGenerateCandidate(
            ErrorCategory model, ExpressionNode root, EvaluationResult correct,
            DistractorRequest request, HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            switch (model)
            {
                case ErrorCategory.NeighbourInTimesTable:
                    return TryNeighbourInTimesTable(root, correct, request, used, out value);

                case ErrorCategory.OnesDigitError:
                    return TryOnesDigitError(correct, request, used, out value);

                case ErrorCategory.TensDigitError:
                    return TryTensDigitError(correct, request, used, out value);

                case ErrorCategory.CarryError:
                    return TryCarryError(root, correct, request, used, out value);

                case ErrorCategory.OperatorPrecedenceError:
                    return TryOperatorPrecedenceError(root, correct, request, used, out value);

                case ErrorCategory.IgnoredParentheses:
                    return TryIgnoredParentheses(root, correct, request, used, out value);

                case ErrorCategory.RemainderMisread:
                    return TryRemainderMisread(root, correct, request, used, out value);

                case ErrorCategory.SignError:
                    return TrySignError(root, correct, request, used, out value);

                case ErrorCategory.ReverseCalculationError:
                    return TryReverseCalculationError(root, correct, request, used, out value);

                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryNeighbourInTimesTable(
            ExpressionNode root, EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;
            if (!(root is BinaryOperationNode binary) || binary.Operator != BinaryOperator.Multiplication
                || !TryGetConstantValue(binary.Left, out int a) || !TryGetConstantValue(binary.Right, out int b))
            {
                return false;
            }

            var candidates = new List<int> { a * (b - 1), a * (b + 1), (a - 1) * b, (a + 1) * b };
            Shuffle(candidates, request.Random);

            foreach (var raw in candidates)
            {
                var candidate = new EvaluationResult(raw);
                if (Accept(candidate, correct, request, used))
                {
                    value = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryOnesDigitError(
            EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;
            if (correct.Remainder.HasValue)
            {
                return false;
            }

            int ones = OnesDigit(correct.Value);
            int baseValue = correct.Value - ones;

            var deltas = new List<int> { -3, -2, -1, 1, 2, 3 };
            Shuffle(deltas, request.Random);

            foreach (var delta in deltas)
            {
                int newOnes = ones + delta;
                if (newOnes < 0 || newOnes > 9)
                {
                    // No tízes átvitel (contract: "tízes átvitel nélkül").
                    continue;
                }

                var candidate = new EvaluationResult(baseValue + newOnes);
                if (Accept(candidate, correct, request, used))
                {
                    value = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryTensDigitError(
            EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;
            if (correct.Remainder.HasValue || correct.Value < 10)
            {
                return false;
            }

            var deltas = new List<int> { 10, -10 };
            Shuffle(deltas, request.Random);

            foreach (var delta in deltas)
            {
                var candidate = new EvaluationResult(correct.Value + delta);
                if (Accept(candidate, correct, request, used))
                {
                    value = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryCarryError(
            ExpressionNode root, EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;
            if (correct.Remainder.HasValue)
            {
                return false;
            }

            if (!(root is BinaryOperationNode binary) || binary.Operator != BinaryOperator.Addition
                || !TryGetConstantValue(binary.Left, out int a) || !TryGetConstantValue(binary.Right, out int b)
                || a < 0 || b < 0)
            {
                return false;
            }

            if (OnesDigit(a) + OnesDigit(b) < 10)
            {
                return false;
            }

            var candidate = new EvaluationResult(AddWithoutCarry(a, b));
            if (!Accept(candidate, correct, request, used))
            {
                return false;
            }

            value = candidate;
            return true;
        }

        private static bool TryOperatorPrecedenceError(
            ExpressionNode root, EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;
            if (!TryNaiveLeftToRightValue(root, out int naive))
            {
                return false;
            }

            var candidate = new EvaluationResult(naive);
            if (!Accept(candidate, correct, request, used))
            {
                return false;
            }

            value = candidate;
            return true;
        }

        private static bool TryIgnoredParentheses(
            ExpressionNode root, EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;
            if (!TryIgnoreParenthesesValue(root, out int withoutParens))
            {
                return false;
            }

            var candidate = new EvaluationResult(withoutParens);
            if (!Accept(candidate, correct, request, used))
            {
                return false;
            }

            value = candidate;
            return true;
        }

        private static bool TryRemainderMisread(
            ExpressionNode root, EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;

            if (correct.Remainder.HasValue)
            {
                if (correct.Value < 0)
                {
                    // The swapped "remainder" slot requires a non-negative
                    // value (EvaluationResult's own ctor invariant) — a
                    // negative quotient cannot be read that way.
                    return false;
                }

                var swapped = new EvaluationResult(correct.Remainder.Value, correct.Value);
                if (!Accept(swapped, correct, request, used))
                {
                    return false;
                }

                value = swapped;
                return true;
            }

            if (root is BinaryOperationNode binary && binary.Operator == BinaryOperator.Modulo
                && TryGetConstantValue(binary.Left, out int a) && TryGetConstantValue(binary.Right, out int b) && b != 0)
            {
                var candidate = new EvaluationResult(a / b);
                if (!Accept(candidate, correct, request, used))
                {
                    return false;
                }

                value = candidate;
                return true;
            }

            return false;
        }

        private static bool TrySignError(
            ExpressionNode root, EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;
            if (correct.Remainder.HasValue
                || !(root is BinaryOperationNode binary)
                || !TryGetConstantValue(binary.Left, out int a)
                || !TryGetConstantValue(binary.Right, out int b))
            {
                return false;
            }

            int candidateValue;
            switch (binary.Operator)
            {
                case BinaryOperator.Addition:
                    candidateValue = a - b;
                    break;

                case BinaryOperator.Subtraction:
                    candidateValue = a + b;
                    break;

                default:
                    return false;
            }

            var candidate = new EvaluationResult(candidateValue);
            if (!Accept(candidate, correct, request, used))
            {
                return false;
            }

            value = candidate;
            return true;
        }

        private static bool TryReverseCalculationError(
            ExpressionNode root, EvaluationResult correct, DistractorRequest request,
            HashSet<EvaluationResult> used, out EvaluationResult value)
        {
            value = default;
            if (correct.Remainder.HasValue
                || !(root is BinaryOperationNode binary)
                || !TryGetConstantValue(binary.Left, out int a)
                || !TryGetConstantValue(binary.Right, out int b))
            {
                return false;
            }

            int candidateValue;
            switch (binary.Operator)
            {
                case BinaryOperator.Subtraction:
                    candidateValue = b - a;
                    break;

                case BinaryOperator.Division:
                    if (a == 0 || b == 0 || a % b != 0 || b % a != 0)
                    {
                        return false;
                    }

                    candidateValue = b / a;
                    break;

                default:
                    return false;
            }

            var candidate = new EvaluationResult(candidateValue);
            if (!Accept(candidate, correct, request, used))
            {
                return false;
            }

            value = candidate;
            return true;
        }

        // --- Fallback: correct ± k, k növekvő (rule 1) -----------------------

        private static bool TryFillWithFallback(DistractorRequest request, HashSet<EvaluationResult> used, List<Distractor> distractors)
        {
            int k = 1;
            int guard = 0;

            // k must be able to reach whichever of [MinValue, MaxValue]'s two
            // edges is FARTHER from the correct answer — not just span the
            // window's own width — otherwise a narrow window sitting far
            // away from CorrectAnswer.Value would be cut off before k ever
            // gets there, misreporting a solvable request as exhausted.
            long distanceToMin = Math.Abs((long)request.CorrectAnswer.Value - request.MinValue);
            long distanceToMax = Math.Abs((long)request.MaxValue - request.CorrectAnswer.Value);
            long maxDistance = Math.Max(distanceToMin, distanceToMax) + 8;
            int maxGuard = (int)Math.Min(200_000L, Math.Max(32L, maxDistance));

            while (distractors.Count < request.Count && guard < maxGuard)
            {
                guard++;
                bool plusFirst = request.Random.NextDouble() < 0.5;
                int plusValue = request.CorrectAnswer.Value + k;
                int minusValue = request.CorrectAnswer.Value - k;

                bool accepted = TryFallbackCandidate(plusFirst ? plusValue : minusValue, request, used, distractors)
                    || TryFallbackCandidate(plusFirst ? minusValue : plusValue, request, used, distractors);
                _ = accepted; // either branch already mutated distractors/used when true

                k++;
            }

            return distractors.Count >= request.Count;
        }

        private static bool TryFallbackCandidate(
            int rawValue, DistractorRequest request, HashSet<EvaluationResult> used, List<Distractor> distractors)
        {
            if (distractors.Count >= request.Count)
            {
                return false;
            }

            var candidate = new EvaluationResult(rawValue);
            if (!Accept(candidate, request.CorrectAnswer, request, used))
            {
                return false;
            }

            used.Add(candidate);
            distractors.Add(new Distractor(candidate, ErrorCategory.Unclassified, FallbackExplanationKey));
            return true;
        }

        // --- Shared acceptance / shape helpers --------------------------------

        private static bool Accept(EvaluationResult candidate, EvaluationResult correct, DistractorRequest request, HashSet<EvaluationResult> used)
        {
            if (candidate.Equals(correct))
            {
                return false;
            }

            if (used.Contains(candidate))
            {
                return false;
            }

            if (candidate.Value < request.MinValue || candidate.Value > request.MaxValue)
            {
                return false;
            }

            if (candidate.Value < 0 && !request.AllowNegative)
            {
                return false;
            }

            return true;
        }

        private static void Shuffle<T>(List<T> list, IRandomSource random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static string ExplanationKeyFor(ErrorCategory model) => model switch
        {
            ErrorCategory.NeighbourInTimesTable => "distractor.neighbour_times_table",
            ErrorCategory.OnesDigitError => "distractor.ones_digit",
            ErrorCategory.TensDigitError => "distractor.tens_digit",
            ErrorCategory.CarryError => "distractor.carry",
            ErrorCategory.OperatorPrecedenceError => "distractor.operator_precedence",
            ErrorCategory.IgnoredParentheses => "distractor.ignored_parentheses",
            ErrorCategory.RemainderMisread => "distractor.remainder_misread",
            ErrorCategory.SignError => "distractor.sign_error",
            ErrorCategory.ReverseCalculationError => "distractor.reverse_calculation",
            _ => FallbackExplanationKey,
        };

        /// <summary>Design note 1: the non-Unknown side of an EqualityNode, else the expression itself.</summary>
        private static ExpressionNode GetEffectiveRoot(ExpressionNode expression)
        {
            if (expression is EqualityNode equality)
            {
                bool leftHasUnknown = ContainsUnknown(equality.Left);
                bool rightHasUnknown = ContainsUnknown(equality.Right);

                if (leftHasUnknown && !rightHasUnknown)
                {
                    return equality.Right;
                }

                if (rightHasUnknown && !leftHasUnknown)
                {
                    return equality.Left;
                }

                return equality.Left;
            }

            return expression;
        }

        private static bool ContainsUnknown(ExpressionNode node)
        {
            switch (node)
            {
                case UnknownNode:
                    return true;

                case ConstantNode:
                    return false;

                case BinaryOperationNode binary:
                    return ContainsUnknown(binary.Left) || ContainsUnknown(binary.Right);

                case EqualityNode equality:
                    return ContainsUnknown(equality.Left) || ContainsUnknown(equality.Right);

                default:
                    return false;
            }
        }

        private static bool TryGetConstantValue(ExpressionNode node, out int value)
        {
            if (node is ConstantNode constant)
            {
                value = constant.Value;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>Ones digit, always in [0, 9] regardless of sign.</summary>
        private static int OnesDigit(int value)
        {
            int mod = value % 10;
            return mod < 0 ? mod + 10 : mod;
        }

        /// <summary>Decimal digit-wise sum with every carry dropped (CarryError, both operands non-negative).</summary>
        private static int AddWithoutCarry(int a, int b)
        {
            int result = 0;
            int place = 1;
            int x = a, y = b;

            while (x > 0 || y > 0)
            {
                int digitA = x % 10;
                int digitB = y % 10;
                result += ((digitA + digitB) % 10) * place;
                x /= 10;
                y /= 10;
                place *= 10;
            }

            return result;
        }

        private static bool ContainsDisplayedParenthesis(ExpressionNode node) =>
            node.ToDisplayString().IndexOf('(') >= 0;

        private static int GetPrecedenceTier(BinaryOperator op)
        {
            switch (op)
            {
                case BinaryOperator.Addition:
                case BinaryOperator.Subtraction:
                    return 1;

                case BinaryOperator.Multiplication:
                case BinaryOperator.Division:
                case BinaryOperator.Modulo:
                case BinaryOperator.IntegerDivisionWithRemainder:
                    return 2;

                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, "Ismeretlen BinaryOperator.");
            }
        }

        private static int CountPrecedenceTiers(ExpressionNode node)
        {
            var tiers = new HashSet<int>();
            CollectTiers(node, tiers);
            return tiers.Count;
        }

        private static void CollectTiers(ExpressionNode node, HashSet<int> tiers)
        {
            switch (node)
            {
                case BinaryOperationNode binary:
                    tiers.Add(GetPrecedenceTier(binary.Operator));
                    CollectTiers(binary.Left, tiers);
                    CollectTiers(binary.Right, tiers);
                    return;

                case EqualityNode equality:
                    CollectTiers(equality.Left, tiers);
                    CollectTiers(equality.Right, tiers);
                    return;

                default:
                    return;
            }
        }

        /// <summary>
        /// The value a student gets by reading the display's flat sequence
        /// of numbers/operators strictly left-to-right, ignoring BOTH real
        /// operator precedence AND any explicit grouping (design note 2).
        /// Returns false for an UnknownNode or an operator this naive model
        /// does not cover (Modulo/IntegerDivisionWithRemainder, or division
        /// by a naively-accumulated zero).
        /// </summary>
        private static bool TryNaiveLeftToRightValue(ExpressionNode node, out int value)
        {
            if (!TryFlattenLeftToRight(node, out List<(int Value, BinaryOperator? Op)> tokens) || tokens.Count == 0)
            {
                value = 0;
                return false;
            }

            value = tokens[0].Value;
            for (int i = 1; i < tokens.Count; i++)
            {
                (int operand, BinaryOperator? op) = tokens[i];
                switch (op)
                {
                    case BinaryOperator.Addition:
                        value += operand;
                        break;

                    case BinaryOperator.Subtraction:
                        value -= operand;
                        break;

                    case BinaryOperator.Multiplication:
                        value *= operand;
                        break;

                    case BinaryOperator.Division:
                        if (operand == 0)
                        {
                            value = 0;
                            return false;
                        }

                        value /= operand;
                        break;

                    default:
                        value = 0;
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The value a student gets by dropping only the VISIBLE grouping
        /// (parentheses) but otherwise still applying normal operator
        /// precedence (design note 2) — i.e. re-reading the flat token
        /// stream as if it had been written without any parentheses at all.
        /// First collapses every tier-2 (tighter) run left-to-right into a
        /// single term, then applies tier-1 (+/−) left-to-right over the
        /// reduced terms. Returns false when the flatten itself fails, or
        /// when a tier-2 step hits an operator this model does not cover
        /// (Modulo/IntegerDivisionWithRemainder, or division that would
        /// divide by zero).
        /// </summary>
        private static bool TryIgnoreParenthesesValue(ExpressionNode node, out int value)
        {
            value = 0;
            if (!TryFlattenLeftToRight(node, out var tokens) || tokens.Count == 0)
            {
                return false;
            }

            var reduced = new List<(int Value, BinaryOperator? Op)> { (tokens[0].Value, null) };

            for (int i = 1; i < tokens.Count; i++)
            {
                (int operand, BinaryOperator? op) = tokens[i];
                if (op == null)
                {
                    return false;
                }

                if (GetPrecedenceTier(op.Value) == 2)
                {
                    var last = reduced[reduced.Count - 1];
                    if (!TryApplyTier2(last.Value, operand, op.Value, out int merged))
                    {
                        return false;
                    }

                    reduced[reduced.Count - 1] = (merged, last.Op);
                }
                else
                {
                    reduced.Add((operand, op));
                }
            }

            value = reduced[0].Value;
            for (int i = 1; i < reduced.Count; i++)
            {
                (int operand, BinaryOperator? op) = reduced[i];
                if (op == BinaryOperator.Addition)
                {
                    value += operand;
                }
                else if (op == BinaryOperator.Subtraction)
                {
                    value -= operand;
                }
                else
                {
                    value = 0;
                    return false;
                }
            }

            return true;
        }

        /// <summary>Multiplication/Division only — Modulo/IntegerDivisionWithRemainder are not modeled by TryIgnoreParenthesesValue.</summary>
        private static bool TryApplyTier2(int a, int b, BinaryOperator op, out int result)
        {
            switch (op)
            {
                case BinaryOperator.Multiplication:
                    result = a * b;
                    return true;

                case BinaryOperator.Division:
                    if (b == 0)
                    {
                        result = 0;
                        return false;
                    }

                    result = a / b;
                    return true;

                default:
                    result = 0;
                    return false;
            }
        }

        private static bool TryFlattenLeftToRight(ExpressionNode node, out List<(int Value, BinaryOperator? Op)> tokens)
        {
            switch (node)
            {
                case ConstantNode constant:
                    tokens = new List<(int Value, BinaryOperator? Op)> { (constant.Value, null) };
                    return true;

                case UnknownNode:
                    tokens = null;
                    return false;

                case BinaryOperationNode binary:
                    if (!TryFlattenLeftToRight(binary.Left, out var leftTokens)
                        || !TryFlattenLeftToRight(binary.Right, out var rightTokens))
                    {
                        tokens = null;
                        return false;
                    }

                    var first = rightTokens[0];
                    rightTokens[0] = (first.Value, binary.Operator);

                    leftTokens.AddRange(rightTokens);
                    tokens = leftTokens;
                    return true;

                default:
                    tokens = null;
                    return false;
            }
        }
    }
}
