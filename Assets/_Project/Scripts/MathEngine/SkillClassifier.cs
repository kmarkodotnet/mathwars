using System;
using System.Collections.Generic;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Default <see cref="ISkillClassifier"/> implementation (contract
    /// math-engine.md 6., spec 7.2/7.4/8., task F2-06). Stateless apart from
    /// the constructor-configured prerequisite minimum level (P3,
    /// math-engine.md 0.) — every other input is a method parameter.
    ///
    /// DESIGN NOTES (flagged per the unity-csharp skill's "contract-szabály"
    /// — the contract text is followed as written; these are the concrete
    /// choices it leaves to the implementation):
    ///
    /// 1. CLASSIFY/fine-grained operand checks (SingleDigitAddition,
    ///    CrossingTen, SubtractionWithBorrowing, TimesTable{k},
    ///    TwoDigitMultiplication) only fire for a <see cref="BinaryOperationNode"/>
    ///    whose Left AND Right are BOTH a literal <see cref="ConstantNode"/>
    ///    — if either operand is a nested sub-expression or an
    ///    <see cref="UnknownNode"/>, that occurrence still contributes its
    ///    COARSE tag (Addition/Subtraction/Multiplication/...) but is
    ///    skipped for the digit-size checks, which need a concrete operand
    ///    VALUE this method does not evaluate (Classify takes no
    ///    IExpressionEvaluator/EvaluationOptions in its contracted
    ///    signature). In practice every leaf IExpressionGenerator (F2-05)
    ///    produces is a ConstantNode except the single UnknownNode a
    ///    MissingOperand/EquationSolving request substitutes, so this only
    ///    ever under-tags the one occurrence directly touching that
    ///    UnknownNode — never silently wrong, only conservatively untagged.
    /// 2. "tényleges zárójel a megjelenítésben" (Parentheses tag) is
    ///    detected by scanning <see cref="ExpressionNode.ToDisplayString"/>
    ///    for '(' — the same design precedent ExpressionEvaluator.
    ///    MeasureParenthesisDepth already established, so both
    ///    ForceParentheses-driven AND ordinary precedence-driven display
    ///    parentheses count, matching the contract's literal wording.
    /// 3. ResourceOperationComparison's classification row only adds
    ///    ResourceComparison — it does NOT list "+ MissingOperand" even
    ///    though a <c>GateOperation</c> tree structurally embeds an
    ///    UnknownNode standing for the resource snapshot (math-engine.md
    ///    7.1), which is not "a missing operand to solve for" in the
    ///    pedagogical sense the MissingOperand skill names. This
    ///    implementation therefore special-cases ResourceOperationComparison
    ///    to skip the generic "contains UnknownNode ⇒ + MissingOperand" rule
    ///    — a reasonable reading of the table's silence, not a contract
    ///    rewrite, flagged here rather than picked silently.
    /// 4. CLASSIFYERROR is a best-effort heuristic from <paramref name="correct"/>/
    ///    <paramref name="selected"/> alone (see ISkillClassifier's own doc
    ///    comment on why AnswerOption.DistractorErrorModel priority is the
    ///    CALLER's job, not this method's). The "naive left-to-right,
    ///    ignoring real operator precedence" reading
    ///    (<see cref="TryNaiveLeftToRightValue"/>) is a generic in-order
    ///    flatten of ANY binary tree shape (not limited to F2-05's current
    ///    left-leaning construction pattern), matching math-horde.md 17.1's
    ///    own canonical "8+4×6−7" naive-mistake example (65, vs. the correct
    ///    25) exactly.
    /// 5. GETPREREQUISITES/ISELIGIBLE use the hard-coded default graph
    ///    math-engine.md 6. documents ("alap, konfigból felülírható —
    ///    SkillPrerequisiteSO") — the config-override half is
    ///    <c>SkillPrerequisiteSO</c>'s own job (contract
    ///    scriptable-object-schema.md 9., task F4, not yet built; P7
    ///    forbids MathEngine from reading ScriptableObject configuration
    ///    directly anyway). The constructor's <c>prerequisiteMinLevel</c>
    ///    defaults to <see cref="SkillMasteryLevel.Practiced"/> per ADR-010
    ///    rule 5's documented default, overridable by a future
    ///    SkillPrerequisiteSO-driven caller without a code change.
    /// </summary>
    public sealed class SkillClassifier : ISkillClassifier
    {
        private readonly SkillMasteryLevel _prerequisiteMinLevel;

        public SkillClassifier(SkillMasteryLevel prerequisiteMinLevel = SkillMasteryLevel.Practiced)
        {
            _prerequisiteMinLevel = prerequisiteMinLevel;
        }

        // --- Classify ------------------------------------------------------

        public IReadOnlyList<SkillTag> Classify(ExpressionNode expression, ProblemType type)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            var tags = new HashSet<SkillTag>();
            var binaryNodes = new List<BinaryOperationNode>();
            CollectBinaryNodes(expression, binaryNodes);

            var precedenceTiers = new HashSet<int>();

            foreach (var node in binaryNodes)
            {
                // Pre-declared (not `out int a`/`out int b` inline) so the
                // compiler's definite-assignment analysis does not report
                // CS0165 further down: it cannot correlate "haveOperands is
                // true" (a separate bool) with "a/b were assigned" across
                // the && short-circuit once that result is stored in a
                // variable instead of used directly in the same expression.
                int a = 0, b = 0;
                bool haveOperands = TryGetConstantValue(node.Left, out a) && TryGetConstantValue(node.Right, out b);

                switch (node.Operator)
                {
                    case BinaryOperator.Addition:
                        tags.Add(SkillTag.Addition);
                        precedenceTiers.Add(1);
                        if (haveOperands)
                        {
                            if (a < 10 && b < 10)
                            {
                                tags.Add(SkillTag.SingleDigitAddition);
                            }

                            if (a >= 10 || b >= 10)
                            {
                                tags.Add(SkillTag.TwoDigitAddition);
                            }

                            if (OnesDigit(a) + OnesDigit(b) >= 10)
                            {
                                tags.Add(SkillTag.CrossingTen);
                            }
                        }

                        break;

                    case BinaryOperator.Subtraction:
                        tags.Add(SkillTag.Subtraction);
                        precedenceTiers.Add(1);
                        if (haveOperands && OnesDigit(a) < OnesDigit(b))
                        {
                            tags.Add(SkillTag.SubtractionWithBorrowing);
                        }

                        break;

                    case BinaryOperator.Multiplication:
                        tags.Add(SkillTag.Multiplication);
                        precedenceTiers.Add(2);
                        if (haveOperands)
                        {
                            AddTimesTableTag(tags, a, b);
                            AddTimesTableTag(tags, b, a);

                            if (a >= 10 && b >= 10)
                            {
                                tags.Add(SkillTag.TwoDigitMultiplication);
                            }
                        }

                        break;

                    case BinaryOperator.Division:
                        tags.Add(SkillTag.Division);
                        precedenceTiers.Add(2);
                        break;

                    case BinaryOperator.Modulo:
                    case BinaryOperator.IntegerDivisionWithRemainder:
                        tags.Add(SkillTag.Division);
                        tags.Add(SkillTag.Remainder);
                        tags.Add(SkillTag.DivisionWithRemainder);
                        precedenceTiers.Add(2);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(expression), node.Operator, "Ismeretlen BinaryOperator.");
                }
            }

            if (precedenceTiers.Count >= 2)
            {
                tags.Add(SkillTag.OperatorPrecedence);
                tags.Add(SkillTag.MultiStepExpressions);
            }

            if (ContainsDisplayedParenthesis(expression))
            {
                tags.Add(SkillTag.Parentheses);
            }

            if (type == ProblemType.ResourceOperationComparison)
            {
                // Design note 3: NOT "+ MissingOperand" even though a
                // GateOperation tree embeds an UnknownNode for the snapshot.
                tags.Add(SkillTag.ResourceComparison);
            }
            else if (type == ProblemType.EquationSolving)
            {
                tags.Add(SkillTag.MissingOperand);
                tags.Add(SkillTag.MultiStepExpressions);
            }
            else if (ContainsUnknown(expression))
            {
                tags.Add(SkillTag.MissingOperand);
            }

            var result = new List<SkillTag>(tags);
            result.Sort((left, right) => ((int)left).CompareTo((int)right));
            return result;
        }

        // --- ClassifyError ---------------------------------------------------

        public ErrorCategory ClassifyError(ExpressionNode expression, EvaluationResult correct, EvaluationResult selected)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (correct.Equals(selected))
            {
                return ErrorCategory.None;
            }

            if ((correct.Remainder.HasValue || selected.Remainder.HasValue) && correct.Value == selected.Value)
            {
                return ErrorCategory.RemainderMisread;
            }

            int correctValue = correct.Value;
            int selectedValue = selected.Value;

            if (correctValue != 0 && selectedValue == -correctValue)
            {
                return ErrorCategory.SignError;
            }

            // ReverseCalculationError models an EQUATION-SOLVING mistake —
            // applying the equation's own operator again instead of its
            // inverse to isolate "?" (e.g. "7 + ? = 15": correct ?=8, but a
            // student who re-adds instead of subtracting gets ?=22). A
            // plain "swap the two operands" check (b-a for Subtraction,
            // b/a for Division) was tried first and rejected: for
            // Subtraction, b-a is ALWAYS exactly -(a-b) — mathematically
            // identical to the SignError check above, so it could never
            // fire; for Division, a value satisfying BOTH a%b==0 (for the
            // correct quotient to be an integer) AND b%a==0 (for the
            // "reversed" quotient to be one too) forces a==b for positive
            // operands, again making it dead code. See TryReverseCalculationValue.
            if (TryReverseCalculationValue(expression, out int reverseValue)
                && selectedValue == reverseValue && reverseValue != correctValue)
            {
                return ErrorCategory.ReverseCalculationError;
            }

            var root = UnwrapRootForErrorChecks(expression);
            if (root is BinaryOperationNode rootBinary
                && rootBinary.Operator == BinaryOperator.Multiplication
                && TryGetConstantValue(rootBinary.Left, out int a)
                && TryGetConstantValue(rootBinary.Right, out int b)
                && IsTimesTableNeighbour(a, b, selectedValue))
            {
                return ErrorCategory.NeighbourInTimesTable;
            }

            if (TryNaiveLeftToRightValue(expression, out int naiveValue) && naiveValue == selectedValue && naiveValue != correctValue)
            {
                return ContainsForceParentheses(expression)
                    ? ErrorCategory.IgnoredParentheses
                    : ErrorCategory.OperatorPrecedenceError;
            }

            int diff = selectedValue - correctValue;
            if (diff != 0)
            {
                // CarryError and a generic TensDigitError numerically LOOK
                // identical whenever the gap is a multiple of ten (both
                // leave the ones digit untouched) — the extra
                // IsPlausibleCarryError check is what tells them apart: a
                // real "forgot to carry/borrow" mistake only makes sense
                // when the underlying Addition/Subtraction's own operands
                // actually crossed a ten-boundary (OnesDigit sum >= 10, or
                // OnesDigit(a) < OnesDigit(b)); otherwise it is just a
                // generic tens-place slip.
                if (diff % 10 == 0 && IsPlausibleCarryError(expression))
                {
                    return ErrorCategory.CarryError;
                }

                if (correctValue / 10 == selectedValue / 10 && correctValue % 10 != selectedValue % 10)
                {
                    return ErrorCategory.OnesDigitError;
                }

                if (correctValue % 10 == selectedValue % 10 && correctValue / 10 != selectedValue / 10)
                {
                    return ErrorCategory.TensDigitError;
                }
            }

            return ErrorCategory.Unclassified;
        }

        private static bool IsPlausibleCarryError(ExpressionNode expression)
        {
            var root = UnwrapRootForErrorChecks(expression);
            if (!(root is BinaryOperationNode binary)
                || !TryGetConstantValue(binary.Left, out int a)
                || !TryGetConstantValue(binary.Right, out int b))
            {
                return false;
            }

            switch (binary.Operator)
            {
                case BinaryOperator.Addition:
                    return OnesDigit(a) + OnesDigit(b) >= 10;

                case BinaryOperator.Subtraction:
                    return OnesDigit(a) < OnesDigit(b);

                default:
                    return false;
            }
        }

        // --- GetPrerequisites / IsEligible ----------------------------------

        public IReadOnlyCollection<SkillTag> GetPrerequisites(SkillTag skill)
        {
            return PrerequisiteGraph.TryGetValue(skill, out var prerequisites)
                ? prerequisites
                : Array.Empty<SkillTag>();
        }

        public bool IsEligible(SkillTag skill, SkillProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            foreach (var prerequisite in GetPrerequisites(skill))
            {
                if (profile.GetMasteryLevel(prerequisite) < _prerequisiteMinLevel)
                {
                    return false;
                }
            }

            return true;
        }

        // --- Prerequisite graph (math-engine.md 6., design note 5) ---------

        private static readonly IReadOnlyDictionary<SkillTag, SkillTag[]> PrerequisiteGraph = BuildPrerequisiteGraph();

        private static IReadOnlyDictionary<SkillTag, SkillTag[]> BuildPrerequisiteGraph()
        {
            var timesTables = new[]
            {
                SkillTag.TimesTable2, SkillTag.TimesTable3, SkillTag.TimesTable4,
                SkillTag.TimesTable5, SkillTag.TimesTable6, SkillTag.TimesTable7,
                SkillTag.TimesTable8, SkillTag.TimesTable9, SkillTag.TimesTable10
            };

            var graph = new Dictionary<SkillTag, SkillTag[]>
            {
                [SkillTag.TwoDigitAddition] = new[] { SkillTag.SingleDigitAddition },
                [SkillTag.CrossingTen] = new[] { SkillTag.TwoDigitAddition },
                [SkillTag.Subtraction] = new[] { SkillTag.Addition },
                [SkillTag.SubtractionWithBorrowing] = new[] { SkillTag.Subtraction },
                [SkillTag.Multiplication] = new[] { SkillTag.Addition },
                [SkillTag.Division] = new[] { SkillTag.Multiplication },
                [SkillTag.DivisionWithRemainder] = new[] { SkillTag.Division },
                [SkillTag.Remainder] = new[] { SkillTag.Division },
                [SkillTag.OperatorPrecedence] = new[] { SkillTag.MultiStepExpressions },
                [SkillTag.Parentheses] = new[] { SkillTag.OperatorPrecedence },
                [SkillTag.MissingOperand] = new[] { SkillTag.Addition, SkillTag.Subtraction },
                [SkillTag.TwoDigitMultiplication] = timesTables,
            };

            foreach (var table in timesTables)
            {
                graph[table] = new[] { SkillTag.Multiplication };
            }

            return graph;
        }

        // --- Shared helpers --------------------------------------------------

        private static void CollectBinaryNodes(ExpressionNode node, List<BinaryOperationNode> into)
        {
            switch (node)
            {
                case ConstantNode:
                case UnknownNode:
                    return;

                case BinaryOperationNode binary:
                    into.Add(binary);
                    CollectBinaryNodes(binary.Left, into);
                    CollectBinaryNodes(binary.Right, into);
                    return;

                case EqualityNode equality:
                    CollectBinaryNodes(equality.Left, into);
                    CollectBinaryNodes(equality.Right, into);
                    return;

                default:
                    throw new ArgumentException($"Ismeretlen ExpressionNode típus: {node.GetType()}.", nameof(node));
            }
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
                    throw new ArgumentException($"Ismeretlen ExpressionNode típus: {node.GetType()}.", nameof(node));
            }
        }

        /// <summary>
        /// The value a "reversed inverse operation" equation-solving mistake
        /// produces (design note 4 / the ClassifyError comment above): only
        /// recognizes the simple "constant op ? = constant" /
        /// "? op constant = constant" shape IExpressionGenerator's
        /// EquationSolving requests actually produce (a single
        /// BinaryOperationNode with one UnknownNode and one ConstantNode
        /// operand, under an EqualityNode whose other side is a plain
        /// ConstantNode) — a multi-step equation (nested BinaryOperationNode
        /// around the unknown) is out of scope for this heuristic and
        /// simply returns false.
        /// </summary>
        private static bool TryReverseCalculationValue(ExpressionNode expression, out int value)
        {
            value = 0;

            if (!(expression is EqualityNode equality))
            {
                return false;
            }

            bool leftHasUnknown = ContainsUnknown(equality.Left);
            bool rightHasUnknown = ContainsUnknown(equality.Right);
            if (leftHasUnknown == rightHasUnknown)
            {
                return false;
            }

            var unknownSide = leftHasUnknown ? equality.Left : equality.Right;
            var knownSide = leftHasUnknown ? equality.Right : equality.Left;

            if (!(knownSide is ConstantNode knownSideConstant) || !(unknownSide is BinaryOperationNode unknownBinary))
            {
                return false;
            }

            int knownOperand;
            if (unknownBinary.Left is UnknownNode && unknownBinary.Right is ConstantNode rightConstant)
            {
                knownOperand = rightConstant.Value;
            }
            else if (unknownBinary.Right is UnknownNode && unknownBinary.Left is ConstantNode leftConstant)
            {
                knownOperand = leftConstant.Value;
            }
            else
            {
                return false;
            }

            return TryComputeSimple(unknownBinary.Operator, knownSideConstant.Value, knownOperand, out value);
        }

        /// <summary>Addition/Subtraction/Multiplication/Division only — Modulo/IntegerDivisionWithRemainder are not modeled by this heuristic.</summary>
        private static bool TryComputeSimple(BinaryOperator op, int x, int y, out int result)
        {
            switch (op)
            {
                case BinaryOperator.Addition:
                    result = x + y;
                    return true;

                case BinaryOperator.Subtraction:
                    result = x - y;
                    return true;

                case BinaryOperator.Multiplication:
                    result = x * y;
                    return true;

                case BinaryOperator.Division:
                    if (y == 0)
                    {
                        result = 0;
                        return false;
                    }

                    result = x / y;
                    return true;

                default:
                    result = 0;
                    return false;
            }
        }

        private static bool ContainsForceParentheses(ExpressionNode node)
        {
            switch (node)
            {
                case BinaryOperationNode binary:
                    return binary.ForceParentheses || ContainsForceParentheses(binary.Left) || ContainsForceParentheses(binary.Right);

                case EqualityNode equality:
                    return ContainsForceParentheses(equality.Left) || ContainsForceParentheses(equality.Right);

                default:
                    return false;
            }
        }

        private static bool ContainsDisplayedParenthesis(ExpressionNode expression) =>
            expression.ToDisplayString().IndexOf('(') >= 0;

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

        /// <summary>Ones digit, always in [0, 9] regardless of sign (so negative operands never produce a bogus "borrow"/"crossing ten" read).</summary>
        private static int OnesDigit(int value)
        {
            int mod = value % 10;
            return mod < 0 ? mod + 10 : mod;
        }

        private static void AddTimesTableTag(HashSet<SkillTag> tags, int k, int other)
        {
            if (k < 2 || k > 10 || other > 10)
            {
                return;
            }

            SkillTag? tag = k switch
            {
                2 => SkillTag.TimesTable2,
                3 => SkillTag.TimesTable3,
                4 => SkillTag.TimesTable4,
                5 => SkillTag.TimesTable5,
                6 => SkillTag.TimesTable6,
                7 => SkillTag.TimesTable7,
                8 => SkillTag.TimesTable8,
                9 => SkillTag.TimesTable9,
                10 => SkillTag.TimesTable10,
                _ => null,
            };

            if (tag.HasValue)
            {
                tags.Add(tag.Value);
            }
        }

        private static bool IsTimesTableNeighbour(int a, int b, int selectedValue) =>
            selectedValue == a * (b - 1)
            || selectedValue == a * (b + 1)
            || selectedValue == (a - 1) * b
            || selectedValue == (a + 1) * b;

        /// <summary>
        /// For an EqualityNode, picks the side most useful for the root-level
        /// error checks (times-table neighbour, reverse calculation):
        /// whichever side does NOT contain the UnknownNode, since that is
        /// the side built from plain constants. Falls back to Left when both
        /// or neither side has one.
        /// </summary>
        private static ExpressionNode UnwrapRootForErrorChecks(ExpressionNode expression)
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

        /// <summary>
        /// The value a student gets by reading the DISPLAY's flat sequence
        /// of numbers/operators strictly left-to-right, ignoring BOTH real
        /// operator precedence AND any explicit grouping — the mistake
        /// pattern behind OperatorPrecedenceError/IgnoredParentheses
        /// (design note 4). Returns false if the tree contains an
        /// UnknownNode or an operator this naive model does not cover
        /// (Modulo/IntegerDivisionWithRemainder, or division by a naively-
        /// accumulated zero).
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
        /// In-order flatten of an arbitrary binary tree into a left-to-right
        /// token sequence (leaf values and the operator that connects each
        /// one to the previous token) — the general version of "read the
        /// display left-to-right", not tied to any one tree shape.
        /// </summary>
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

                    // rightTokens[0]'s Op is always null (its own subtree's
                    // start) — overwrite it with the operator connecting the
                    // left subtree's last leaf to the right subtree's first
                    // leaf in the flat, left-to-right reading.
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
