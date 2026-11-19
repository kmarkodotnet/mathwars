using System;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.Core.Skills;
using MathHorde.MathEngine;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.MathEngine
{
    [TestFixture]
    public class ProblemValidatorTests
    {
        private readonly ProblemValidator _validator = new ProblemValidator();

        [Test]
        public void Validate_ValidCandidate_BothEntryPointsAccept()
        {
            var report = _validator.Validate(Candidate(), Profile());
            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Issues.Count, Is.EqualTo(0));
            Assert.That(_validator.IsValid(Candidate(), Profile()), Is.True);
        }

        [Test]
        public void Validate_NullArguments_AreProgrammerErrors()
        {
            Assert.That(() => _validator.Validate(null, Profile()), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => _validator.IsValid(Candidate(), null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Validate_WrongAnswerOrMarkedOption_IsRejected()
        {
            Has(Candidate(answer: new EvaluationResult(51)), "ANSWER_MISMATCH");
            Has(Candidate(options: Options(50, 0, 48, 50, 53)), "ANSWER_MISMATCH");
        }

        [TestCase(0, "NO_CORRECT_OPTION")]
        [TestCase(2, "MULTIPLE_CORRECT_OPTIONS")]
        public void Validate_CorrectFlagCount_IsChecked(int flags, string code)
        {
            var options = new[] { Option(0, 48, flags > 0), Option(1, 50, flags > 1), Option(2, 53, false) };
            Has(Candidate(options: options), code);
        }

        [Test]
        public void Validate_ActualCorrectValueMustBeMarked()
        {
            Has(Candidate(options: Options(50, 0, 48, 50, 53)), "ANSWER_MISMATCH");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        public void Validate_OptionCount_MatchesBoundsAndProfile(int count)
        {
            Has(Candidate(options: Enumerable.Range(0, count).Select(i => Option(i, 50 + i, i == 0)).ToArray()), "OPTION_COUNT_OUT_OF_RANGE");
        }

        [Test]
        public void Validate_LaneUniquenessAndBounds_AreIndependent()
        {
            var options = new[] { Option(0, 48, false), Option(0, 50, true), Option(3, 53, false) };
            var report = Has(Candidate(options: options), "DUPLICATE_LANE");
            Assert.That(Codes(report), Does.Contain("LANE_OUT_OF_RANGE"));
        }

        [Test]
        public void Validate_EqualityIncludesRemainder()
        {
            var expr = Binary(BinaryOperator.IntegerDivisionWithRemainder, 29, 4);
            var options = new[] { Option(0, 7, true, 1), Option(1, 7, false, 2), Option(2, 7, false, 3) };
            var good = Candidate(expr: expr, answer: new EvaluationResult(7, 1), options: options);
            Assert.That(_validator.IsValid(good, Profile()), Is.True);
            Has(Candidate(expr: expr, answer: new EvaluationResult(7, 2), options: options), "ANSWER_MISMATCH");
            Has(Candidate(options: new[] { Option(0, 50, true), Option(1, 50, false), Option(2, 51, false) }), "DUPLICATE_OPTIONS");
        }

        [Test]
        public void Validate_SkillTags_EmptyMissingExtraAndLocked()
        {
            Has(Candidate(tags: Array.Empty<SkillTag>()), "EMPTY_SKILL_TAGS");
            Has(Candidate(tags: new[] { SkillTag.Addition }), "SKILL_TAG_MISMATCH");
            var tags = new SkillClassifier().Classify(DefaultExpression(), ProblemType.AnswerLaneSelection).Concat(new[] { SkillTag.Division }).ToArray();
            Has(Candidate(tags: tags), "SKILL_TAG_MISMATCH");
            Has(Candidate(), "SKILL_NOT_UNLOCKED", Profile(skills: Array.Empty<SkillTag>()));
        }

        [Test]
        public void Validate_OperatorAndOperands_AreCheckedRecursively()
        {
            var expr = new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(30), Binary(BinaryOperator.Multiplication, 5, 4));
            Has(Candidate(expr: expr), "OPERATOR_NOT_ALLOWED", Profile(operators: new[] { BinaryOperator.Addition }));
            Has(Candidate(), "OPERAND_RANGE_VIOLATION", Profile(maxOperand: 25));
            Has(Candidate(), "OPERAND_RANGE_VIOLATION", Profile(minOperand: 25));
        }

        [TestCase(BinaryOperator.Division, 1, 0, "DIVISION_BY_ZERO")]
        [TestCase(BinaryOperator.Division, 7, 2, "NON_INTEGER_RESULT")]
        [TestCase(BinaryOperator.Addition, int.MaxValue, 1, "OVERFLOW")]
        [TestCase(BinaryOperator.Modulo, 7, -2, "MODULO_NON_POSITIVE_DIVISOR")]
        public void Validate_EvaluationFailures_UseEvaluatorCodes(BinaryOperator op, int a, int b, string code)
        {
            Has(Candidate(expr: Binary(op, a, b)), code);
        }

        [Test]
        public void Validate_NegativeResult_UsesProfilePolicy()
        {
            var problem = Candidate(expr: Binary(BinaryOperator.Subtraction, 20, 30), answer: new EvaluationResult(-10),
                options: Options(-10, 1, -12, -10, -8));
            Has(problem, "NEGATIVE_RESULT_NOT_ALLOWED");
            Assert.That(_validator.IsValid(problem, Profile(allowNegative: true)), Is.True);
        }

        [Test]
        public void Validate_NegativeIntermediateAndLargeIntermediate_AreNotFinalBounds()
        {
            var negative = new BinaryOperationNode(BinaryOperator.Addition, Binary(BinaryOperator.Subtraction, 20, 30), new ConstantNode(60));
            Assert.That(_validator.IsValid(Candidate(expr: negative), Profile()), Is.True);
            var large = new BinaryOperationNode(BinaryOperator.Subtraction, Binary(BinaryOperator.Multiplication, 100, 100), new ConstantNode(9950));
            Assert.That(_validator.IsValid(Candidate(expr: large), Profile(maxOperand: 10000, maxResult: 50)), Is.True);
        }

        [Test]
        public void Validate_FinalResult_UsesIndependentMaxResultValue()
        {
            Has(Candidate(), "RESULT_RANGE_VIOLATION", Profile(maxResult: 49));
            Assert.That(_validator.IsValid(Candidate(), Profile(maxOperand: 30, maxResult: 50)), Is.True);
            Has(Candidate(expr: new ConstantNode(1001), answer: new EvaluationResult(1001)), "RESULT_RANGE_VIOLATION");
        }

        [Test]
        public void Validate_UnknownCountAndDisplayedParentheses_AreChecked()
        {
            Has(Candidate(), "UNKNOWN_COUNT_MISMATCH", Profile(unknowns: 1));
            var expr = new BinaryOperationNode(BinaryOperator.Multiplication, Binary(BinaryOperator.Addition, 6, 4), new ConstantNode(5));
            Has(Candidate(expr: expr), "PARENTHESIS_DEPTH_EXCEEDED", Profile(depth: 0));
            Assert.That(_validator.IsValid(Candidate(expr: expr), Profile(depth: 1)), Is.True);
        }

        [TestCase(ProblemType.MissingOperand)]
        [TestCase(ProblemType.EquationSolving)]
        public void Validate_Equation_UsesUniqueSolutionAndChecksAnswer(ProblemType type)
        {
            var expr = new EqualityNode(new BinaryOperationNode(BinaryOperator.Addition, new ConstantNode(7), new UnknownNode()), Binary(BinaryOperator.Multiplication, 3, 8));
            var good = Candidate(type: type, expr: expr, answer: new EvaluationResult(17), options: Options(17, 1, 16, 17, 18));
            Assert.That(_validator.IsValid(good, Profile(unknowns: 1)), Is.True);
            Has(Candidate(type: type, expr: expr, answer: new EvaluationResult(18)), "ANSWER_MISMATCH", Profile(unknowns: 1));
        }

        [TestCase(BinaryOperator.Multiplication, 0, 0)]
        [TestCase(BinaryOperator.Multiplication, 2, 7)]
        [TestCase(BinaryOperator.Modulo, 4, 1)]
        public void Validate_NonUniqueOrNonIntegerEquation_IsRejected(BinaryOperator op, int value, int target)
        {
            var expr = new EqualityNode(new BinaryOperationNode(op, new UnknownNode(), new ConstantNode(value)), new ConstantNode(target));
            Has(Candidate(type: ProblemType.EquationSolving, expr: expr), "UNKNOWN_NOT_UNIQUE", Profile(unknowns: 1));
        }

        [Test]
        public void Validate_MalformedUnknownShape_DoesNotThrow()
        {
            Has(Candidate(type: ProblemType.MissingOperand), "UNKNOWN_NOT_UNIQUE");
            var expr = new EqualityNode(new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new UnknownNode()), new ConstantNode(10));
            var report = Has(Candidate(type: ProblemType.EquationSolving, expr: expr), "UNKNOWN_NOT_UNIQUE", Profile(unknowns: 1));
            Assert.That(Codes(report), Does.Contain("UNKNOWN_COUNT_MISMATCH"));
        }

        [Test]
        public void Validate_EquationChecksArithmeticAndSolutionOperandRange()
        {
            var broken = new EqualityNode(new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(1)), Binary(BinaryOperator.Division, 7, 0));
            Has(Candidate(type: ProblemType.EquationSolving, expr: broken), "DIVISION_BY_ZERO", Profile(unknowns: 1));
            var large = new EqualityNode(new BinaryOperationNode(BinaryOperator.Subtraction, new UnknownNode(), new ConstantNode(90)), new ConstantNode(20));
            Has(Candidate(type: ProblemType.EquationSolving, expr: large, answer: new EvaluationResult(110)), "OPERAND_RANGE_VIOLATION", Profile(unknowns: 1));
        }

        [Test]
        public void Validate_NanThinkTime_IsRejected()
        {
            Has(Candidate(thinkTime: float.NaN), "THINK_TIME_NON_POSITIVE");
        }

        [Test]
        public void Validate_Warnings_DoNotInvalidateAndRespectErrorModelExceptions()
        {
            var report = _validator.Validate(Candidate(options: Options(50, 0, 50, 100, 101)), Profile());
            Assert.That(report.IsValid, Is.True);
            Assert.That(Codes(report), Does.Contain("DISTRACTOR_TOO_FAR"));
            Assert.That(Codes(report), Does.Contain("GUESSABLE_PATTERN"));
            Assert.That(report.Issues.All(i => i.Severity == ValidationSeverity.Warning), Is.True);
            var options = new[] { Option(0, 50, true), Option(1, 100, false, error: ErrorCategory.OperatorPrecedenceError), Option(2, 101, false, error: ErrorCategory.IgnoredParentheses) };
            Assert.That(Codes(_validator.Validate(Candidate(options: options), Profile())), Does.Not.Contain("DISTRACTOR_TOO_FAR"));
        }

        [TestCase(null)]
        [TestCase(0)]
        [TestCase(-5)]
        public void Validate_InvalidCapacity_SkipsOnlyCapacityDependentRules(int? capacity)
        {
            var report = Has(Gates(new[] { 40, 40 }, capacity), "GATE_CAPACITY_INVALID", Profile(count: 2));
            Assert.That(Codes(report), Does.Contain("GATE_TIE"));
            Assert.That(Codes(report), Does.Contain("DUPLICATE_OPTIONS"));
            Assert.That(Codes(report), Does.Not.Contain("GATE_GAP_TOO_SMALL"));
            Assert.That(Codes(report), Does.Not.Contain("GATE_ORDER_INVERTED_BY_CAP"));
            Has(Gates(new[] { 40, 45 }, capacity, GateGoal.ReachTarget), "GATE_TARGET_MISSING", Profile(count: 2));
        }

        [Test]
        public void Validate_CapacityOnOtherType_IsInvalid()
        {
            Has(Candidate(capacity: 100), "GATE_CAPACITY_INVALID");
        }

        [TestCase(40, 45, 100, 4, null)]
        [TestCase(40, 45, 40, 4, "GATE_TIE")]
        [TestCase(40, 50, 45, 8, "GATE_GAP_TOO_SMALL")]
        public void Validate_ContractCapacityExamples(int a, int b, int capacity, int gap, string code)
        {
            var problem = Gates(new[] { a, b }, capacity);
            var report = _validator.Validate(problem, Profile(count: 2, gap: gap));
            if (code == null) Assert.That(report.IsValid, Is.True);
            else Assert.That(Codes(report), Does.Contain(code));
            if (capacity == 40) Assert.That(Codes(report), Does.Contain("GATE_GAP_TOO_SMALL"));
            Assert.That(Codes(report), Does.Not.Contain("GATE_ORDER_INVERTED_BY_CAP"));
            Assert.That(_validator.IsValid(problem, Profile(count: 2, gap: gap)), Is.EqualTo(report.IsValid));
        }

        [TestCase(75, "GATE_TIE")]
        [TestCase(65, "GATE_ORDER_INVERTED_BY_CAP")]
        public void Validate_ReachTarget_UsesUnchangedTarget(int capacity, string code)
        {
            var report = Has(Gates(new[] { 45, 80 }, capacity, GateGoal.ReachTarget, 60), code, Profile(count: 2));
            Assert.That(Codes(report), Does.Not.Contain("ANSWER_MISMATCH"));
            Assert.That(Codes(report), Does.Not.Contain(code == "GATE_TIE" ? "GATE_ORDER_INVERTED_BY_CAP" : "GATE_TIE"));
        }

        [Test]
        public void Validate_ReachTarget_RawDistanceTieAndMissingTarget()
        {
            Has(Gates(new[] { 45, 75 }, 100, GateGoal.ReachTarget, 60), "GATE_TIE", Profile(count: 2));
            var report = Has(Gates(new[] { 40, 50 }, 42, GateGoal.ReachTarget), "GATE_TARGET_MISSING", Profile(count: 2));
            Assert.That(Codes(report), Does.Contain("GATE_GAP_TOO_SMALL"));
            Assert.That(Codes(report), Does.Not.Contain("GATE_ORDER_INVERTED_BY_CAP"));
        }

        [Test]
        public void Validate_AllGatePairsAndGoals_AreChecked()
        {
            Has(Gates(new[] { 10, 20, 22, 80 }, 100), "GATE_GAP_TOO_SMALL", Profile(count: 4));
            Assert.That(_validator.IsValid(Gates(new[] { 40, 80 }, 60, GateGoal.Minimize), Profile(count: 2)), Is.True);
            Has(Gates(new[] { 40, 80 }, 35, GateGoal.Minimize), "GATE_TIE", Profile(count: 2));
            Has(Gates(new[] { 10, 20, 20, 80 }, 100), "GATE_TIE", Profile(count: 4));
        }

        [Test]
        public void Validate_GateWinnerMustMatchMarkedCorrectOption()
        {
            Has(Gates(new[] { 40, 80 }, 100, GateGoal.Maximize, correctIndex: 0), "ANSWER_MISMATCH", Profile(count: 2));
        }

        [Test]
        public void Validate_EachGateResult_RespectsFinalRangeAndSignPolicy()
        {
            Has(Gates(new[] { 40, 80 }, 100, GateGoal.Minimize), "RESULT_RANGE_VIOLATION", Profile(count: 2, maxResult: 60));
            Has(Gates(new[] { -5, 40 }, 100), "NEGATIVE_RESULT_NOT_ALLOWED", Profile(count: 2));
        }

        [Test]
        public void Validate_GateArithmetic_UsesWidenedDifferences()
        {
            var report = _validator.Validate(Gates(new[] { int.MinValue, int.MaxValue }, int.MaxValue,
                GateGoal.ReachTarget, int.MinValue), Profile(count: 2, allowNegative: true, maxOperand: int.MaxValue, maxResult: int.MaxValue));
            Assert.That(Codes(report), Does.Not.Contain("GATE_GAP_TOO_SMALL"));
            Assert.That(Codes(report), Does.Not.Contain("GATE_TIE"));
            Assert.That(Codes(report), Does.Not.Contain("GATE_ORDER_INVERTED_BY_CAP"));
        }

        [Test]
        public void Validate_GateOperations_AreIncludedInSkillAndOperatorChecks()
        {
            var ops = new[] {
                new AnswerOption(0, new LaneIndex(0), new EvaluationResult(40), new BinaryOperationNode(BinaryOperator.Multiplication, new UnknownNode(), new ConstantNode(2)), false, null, "x2"),
                new AnswerOption(1, new LaneIndex(1), new EvaluationResult(45), new BinaryOperationNode(BinaryOperator.Addition, new UnknownNode(), new ConstantNode(25)), true, null, "+25") };
            var expr = Binary(BinaryOperator.Addition, 20, 25);
            var tags = new SkillClassifier().Classify(expr, ProblemType.ResourceOperationComparison).Concat(new[] { SkillTag.Multiplication }).Distinct().ToArray();
            var good = Candidate(type: ProblemType.ResourceOperationComparison, expr: expr, answer: new EvaluationResult(45), options: ops, capacity: 100, tags: tags);
            Assert.That(_validator.IsValid(good, Profile(count: 2)), Is.True);
            Has(good, "OPERATOR_NOT_ALLOWED", Profile(count: 2, operators: new[] { BinaryOperator.Addition }));
            Has(good, "OPERAND_RANGE_VIOLATION", Profile(count: 2, maxOperand: 24));
        }

        [Test]
        public void ValidationReport_CopiesIssuesAndErrorsDetermineValidity()
        {
            var issues = new List<ValidationIssue> { new ValidationIssue("WARN", "Warning.", ValidationSeverity.Warning) };
            var report = new ValidationReport(issues);
            issues.Add(new ValidationIssue("ERR", "Error.", ValidationSeverity.Error));
            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Issues.Count, Is.EqualTo(1));
            Assert.That(new ValidationReport(issues).IsValid, Is.False);
            Assert.That(new ValidationReport(Array.Empty<ValidationIssue>()).IsValid, Is.True);
        }

        private ValidationReport Has(MathProblem problem, string code, DifficultyProfile profile = null)
        {
            profile = profile ?? Profile();
            var report = _validator.Validate(problem, profile);
            Assert.That(Codes(report), Does.Contain(code));
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.First(i => i.Code == code).Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(_validator.IsValid(problem, profile), Is.EqualTo(report.IsValid));
            return report;
        }

        internal static string[] Codes(ValidationReport report) => report.Issues.Select(i => i.Code).ToArray();
        internal static ExpressionNode DefaultExpression() => Binary(BinaryOperator.Addition, 20, 30);
        internal static ExpressionNode Binary(BinaryOperator op, int a, int b) => new BinaryOperationNode(op, new ConstantNode(a), new ConstantNode(b));

        internal static AnswerOption Option(int lane, int value, bool correct, int? remainder = null, ErrorCategory? error = null)
            => new AnswerOption(lane, new LaneIndex(lane), new EvaluationResult(value, remainder), null, correct, correct ? null : error, value.ToString());

        internal static AnswerOption[] Options(int correct, int correctIndex, params int[] values)
            => values.Select((value, i) => Option(i, value, i == correctIndex)).ToArray();

        internal static MathProblem Candidate(ExpressionNode expr = null, EvaluationResult? answer = null, IReadOnlyList<AnswerOption> options = null,
            IReadOnlyList<SkillTag> tags = null, ProblemType type = ProblemType.AnswerLaneSelection, int? capacity = null,
            GateGoal goal = GateGoal.Maximize, int? target = null, float thinkTime = 6f, int seed = 808)
        {
            expr = expr ?? DefaultExpression();
            return new MathProblem(new MathProblemId(new Guid("08080808-0808-0808-0808-080808080808")), type,
                expr, expr.ToDisplayString(), answer ?? new EvaluationResult(50), options ?? Options(50, 1, 48, 50, 53), 4,
                tags ?? new SkillClassifier().Classify(expr, type), thinkTime, new RewardSpec(ResourceType.Ammo, 0, type == ProblemType.ResourceOperationComparison, 0),
                seed, goal, target, null, capacity);
        }

        internal static DifficultyProfile Profile(int count = 3, int gap = 4, int minOperand = 0, int maxOperand = 100, int maxResult = 1000,
            int unknowns = 0, int depth = 2, bool allowNegative = false, IReadOnlyCollection<SkillTag> skills = null,
            IReadOnlyCollection<BinaryOperator> operators = null)
            => new DifficultyProfile(4, 4, 4, 4, operators ?? (BinaryOperator[])Enum.GetValues(typeof(BinaryOperator)),
                skills ?? (SkillTag[])Enum.GetValues(typeof(SkillTag)), minOperand, maxOperand, maxResult, 2, 5,
                depth > 0, depth, unknowns > 0, unknowns, allowNegative, count, 6f, gap, true);

        internal static MathProblem Gates(int[] values, int? capacity, GateGoal goal = GateGoal.Maximize, int? target = null, int? correctIndex = null)
        {
            int winner = correctIndex ?? Enumerable.Range(0, values.Length).OrderBy(i => goal == GateGoal.Maximize ? -(long)values[i]
                : goal == GateGoal.Minimize ? values[i] : Math.Abs((long)values[i] - (target ?? 0))).First();
            var options = values.Select((value, i) => Option(i, value, i == winner)).ToArray();
            return Candidate(expr: new ConstantNode(values[winner]), answer: new EvaluationResult(values[winner]), options: options,
                type: ProblemType.ResourceOperationComparison, capacity: capacity, goal: goal, target: target);
        }
    }
}
