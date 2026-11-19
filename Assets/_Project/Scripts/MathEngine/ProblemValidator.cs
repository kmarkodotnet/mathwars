using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Stateless candidate validation (math-engine.md 5., ADR-013).
    /// Unknown-answer problems use an EqualityNode: its solution, rather than
    /// either side's value, is CorrectAnswer. Resource GateOperation unknowns
    /// denote the snapshot, so do not count as missing operands in Expression.
    /// The main expression carries the concrete raw winning calculation; all
    /// displayed gate operations also contribute operator/operand/skill checks.
    /// </summary>
    public sealed class ProblemValidator : IProblemValidator
    {
        private readonly IExpressionEvaluator _evaluator;
        private readonly IEquationSolver _solver;
        private readonly ISkillClassifier _classifier;

        public ProblemValidator() : this(new ExpressionEvaluator(), null, new SkillClassifier()) { }

        public ProblemValidator(IExpressionEvaluator evaluator, IEquationSolver solver, ISkillClassifier classifier)
        {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _solver = solver ?? new EquationSolver(evaluator);
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        }

        public bool IsValid(MathProblem problem, DifficultyProfile profile) => Validate(problem, profile).IsValid;

        public ValidationReport Validate(MathProblem problem, DifficultyProfile profile)
        {
            if (problem == null) throw new ArgumentNullException(nameof(problem));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var issues = new List<ValidationIssue>();
            var evaluation = new EvaluationOptions
            {
                AllowNegativeIntermediate = true,
                AllowNegativeResult = profile.AllowNegativeResults,
                MinValue = int.MinValue,
                MaxValue = int.MaxValue,
            };

            ValidateAnswer(problem, profile, evaluation, issues);
            ValidateOptions(problem, profile, issues);

            var detected = new HashSet<SkillTag>(_classifier.Classify(problem.Expression, problem.Type));
            ValidateTree(problem.Expression, profile, issues);
            if (problem.Type == ProblemType.ResourceOperationComparison)
            {
                foreach (var option in problem.Options)
                {
                    if (option.GateOperation == null) continue;
                    ValidateTree(option.GateOperation, profile, issues);
                    detected.UnionWith(_classifier.Classify(option.GateOperation, problem.Type));
                }
            }

            if (problem.SkillTags.Count == 0) Add(issues, "EMPTY_SKILL_TAGS", "At least one skill tag is required.");
            if (!detected.SetEquals(problem.SkillTags)) Add(issues, "SKILL_TAG_MISMATCH", "Skill tags do not match the displayed operations.");
            var unlocked = new HashSet<SkillTag>(profile.UnlockedSkills);
            foreach (var tag in problem.SkillTags)
                if (!unlocked.Contains(tag)) Add(issues, "SKILL_NOT_UNLOCKED", "A required skill is not unlocked.");

            if (_evaluator.CountUnknowns(problem.Expression) != profile.UnknownOperandCount)
                Add(issues, "UNKNOWN_COUNT_MISMATCH", "Unknown count differs from the difficulty profile.");
            if (_evaluator.MeasureParenthesisDepth(problem.Expression) > profile.MaxParenthesisDepth)
                Add(issues, "PARENTHESIS_DEPTH_EXCEEDED", "Displayed parentheses exceed the configured depth.");
            if (!(problem.ThinkTimeSeconds > 0f) || float.IsInfinity(problem.ThinkTimeSeconds))
                Add(issues, "THINK_TIME_NON_POSITIVE", "Think time must be finite and positive.");

            ValidateGates(problem, profile, issues);
            return new ValidationReport(issues);
        }

        private void ValidateAnswer(MathProblem problem, DifficultyProfile profile, EvaluationOptions evaluation, List<ValidationIssue> issues)
        {
            bool unknownProblem = problem.Type == ProblemType.MissingOperand || problem.Type == ProblemType.EquationSolving;
            if (unknownProblem)
            {
                if (!(problem.Expression is EqualityNode equation))
                {
                    Add(issues, "UNKNOWN_NOT_UNIQUE", "An unknown-answer problem requires an equation with one integer solution.");
                }
                else
                {
                    var solution = _solver.Solve(equation, evaluation);
                    if (!solution.IsSuccess)
                        Add(issues, "UNKNOWN_NOT_UNIQUE", "The equation has no supported unique integer solution.");
                    else
                    {
                        if (problem.CorrectAnswer != new EvaluationResult(solution.Value))
                            Add(issues, "ANSWER_MISMATCH", "CorrectAnswer differs from the equation solution.");
                        CheckAnswerRange(solution.Value, profile, issues);
                        if (solution.Value < profile.MinOperandValue || solution.Value > profile.MaxOperandValue)
                            Add(issues, "OPERAND_RANGE_VIOLATION", "The solved operand is outside the configured range.");
                    }

                    // Also report independent arithmetic failures, including on
                    // the known side when inversion itself cannot complete.
                    CheckEvaluation(_evaluator.EvaluateWith(equation.Left, problem.CorrectAnswer.Value, evaluation), issues);
                    CheckEvaluation(_evaluator.EvaluateWith(equation.Right, problem.CorrectAnswer.Value, evaluation), issues);
                }
            }
            else if (problem.Expression is EqualityNode)
            {
                Add(issues, "ANSWER_MISMATCH", "A scalar-answer problem cannot use an equation as its result.");
            }
            else
            {
                var result = _evaluator.Evaluate(problem.Expression, evaluation);
                CheckEvaluation(result, issues);
                if (result.IsSuccess)
                {
                    if (result.Value != problem.CorrectAnswer)
                        Add(issues, "ANSWER_MISMATCH", "CorrectAnswer differs from the evaluated expression.");
                    CheckAnswerRange(result.Value.Value, profile, issues);
                }
            }

            CheckAnswerRange(problem.CorrectAnswer.Value, profile, issues);
        }

        private static void CheckAnswerRange(int value, DifficultyProfile profile, List<ValidationIssue> issues)
        {
            if (value > profile.MaxResultValue) Add(issues, "RESULT_RANGE_VIOLATION", "The answer exceeds MaxResultValue.");
            if (value < 0 && !profile.AllowNegativeResults)
                Add(issues, "NEGATIVE_RESULT_NOT_ALLOWED", "Negative final results are disabled.");
        }

        private static void CheckEvaluation(Result<EvaluationResult> result, List<ValidationIssue> issues)
        {
            if (!result.IsSuccess) Add(issues, result.ErrorCode, "Expression evaluation failed: " + result.ErrorCode + ".");
        }

        private static void ValidateOptions(MathProblem problem, DifficultyProfile profile, List<ValidationIssue> issues)
        {
            int count = problem.Options.Count;
            if (count < 2 || count > 4 || count != profile.AnswerOptionCount)
                Add(issues, "OPTION_COUNT_OUT_OF_RANGE", "Option count must be 2..4 and match the profile.");
            int correct = 0;
            var values = new HashSet<EvaluationResult>();
            var lanes = new HashSet<int>();
            bool hasSmaller = false, hasLarger = false;
            foreach (var option in problem.Options)
            {
                if (option.IsCorrect)
                {
                    correct++;
                    if (option.Value != problem.CorrectAnswer)
                        Add(issues, "ANSWER_MISMATCH", "The correct option does not contain CorrectAnswer.");
                }

                if (problem.Type == ProblemType.ResourceOperationComparison)
                    CheckAnswerRange(option.Value.Value, profile, issues);

                if (!values.Add(option.Value)) Add(issues, "DUPLICATE_OPTIONS", "Option values must be distinct, including remainders.");
                if (!lanes.Add(option.Lane.Value)) Add(issues, "DUPLICATE_LANE", "Option lanes must be unique.");
                if (option.Lane.Value < 0 || option.Lane.Value >= count) Add(issues, "LANE_OUT_OF_RANGE", "An option lane is outside 0..Count-1.");
                hasSmaller |= option.Value.Value < problem.CorrectAnswer.Value;
                hasLarger |= option.Value.Value > problem.CorrectAnswer.Value;

                if (!option.IsCorrect && problem.Type != ProblemType.ResourceOperationComparison
                    && option.DistractorErrorModel != ErrorCategory.OperatorPrecedenceError
                    && option.DistractorErrorModel != ErrorCategory.IgnoredParentheses)
                {
                    long distance = Math.Abs((long)option.Value.Value - problem.CorrectAnswer.Value);
                    long limit = Math.Max(5L, (Math.Abs((long)problem.CorrectAnswer.Value) + 1) / 2);
                    if (distance > limit) Add(issues, "DISTRACTOR_TOO_FAR", "A distractor is outside the recommended answer magnitude.", ValidationSeverity.Warning);
                }
            }

            if (correct == 0) Add(issues, "NO_CORRECT_OPTION", "Exactly one option must be marked correct.");
            if (correct > 1) Add(issues, "MULTIPLE_CORRECT_OPTIONS", "More than one option is marked correct.");
            // A per-item signal only. Distribution/heuristic hit rates must be
            // checked on a seeded batch; this validator keeps no history.
            if (count >= 2 && problem.Type != ProblemType.ResourceOperationComparison && (!hasSmaller || !hasLarger))
                Add(issues, "GUESSABLE_PATTERN", "The answer is an extreme option; verify batch heuristic hit rates.", ValidationSeverity.Warning);
        }

        private static void ValidateTree(ExpressionNode node, DifficultyProfile profile, List<ValidationIssue> issues)
        {
            if (node is ConstantNode constant)
            {
                if (constant.Value < profile.MinOperandValue || constant.Value > profile.MaxOperandValue)
                    Add(issues, "OPERAND_RANGE_VIOLATION", "A literal operand is outside the configured range.");
            }
            else if (node is BinaryOperationNode binary)
            {
                bool allowed = false;
                foreach (var op in profile.AllowedOperators) allowed |= op == binary.Operator;
                if (!allowed)
                    Add(issues, "OPERATOR_NOT_ALLOWED", "A displayed operator is disabled by the profile.");
                ValidateTree(binary.Left, profile, issues);
                ValidateTree(binary.Right, profile, issues);
            }
            else if (node is EqualityNode equality)
            {
                ValidateTree(equality.Left, profile, issues);
                ValidateTree(equality.Right, profile, issues);
            }
        }

        private static void ValidateGates(MathProblem problem, DifficultyProfile profile, List<ValidationIssue> issues)
        {
            bool gate = problem.Type == ProblemType.ResourceOperationComparison;
            bool capacityValid = problem.ResourceCapacity.HasValue && problem.ResourceCapacity.Value > 0;
            if (gate ? !capacityValid : problem.ResourceCapacity.HasValue)
                Add(issues, "GATE_CAPACITY_INVALID", "Resource gates require a positive captured capacity; other types require null.");
            if (!gate) return;
            bool targetMissing = problem.Goal == GateGoal.ReachTarget && !problem.TargetValue.HasValue;
            if (targetMissing) Add(issues, "GATE_TARGET_MISSING", "ReachTarget requires TargetValue.");

            for (int i = 0; i < problem.Options.Count; i++)
            {
                for (int j = i + 1; j < problem.Options.Count; j++)
                {
                    long a = problem.Options[i].Value.Value, b = problem.Options[j].Value.Value;
                    if (a == b) Add(issues, "GATE_TIE", "Two raw gate values are equal.");
                    if (!capacityValid) continue;
                    a = Math.Min(a, problem.ResourceCapacity.Value);
                    b = Math.Min(b, problem.ResourceCapacity.Value);
                    if (a == b) Add(issues, "GATE_TIE", "Two effective gate values are equal.");
                    if (Math.Abs(a - b) < profile.MinGateGap)
                        Add(issues, "GATE_GAP_TOO_SMALL", "An effective gate pair is closer than MinGateGap.");
                }
            }

            if (targetMissing || problem.Options.Count == 0) return;
            int raw = FindOptimum(problem, null);
            if (raw < 0) Add(issues, "GATE_TIE", "The raw gate optimum is not unique.");
            else if (!problem.Options[raw].IsCorrect || problem.Options[raw].Value != problem.CorrectAnswer)
                Add(issues, "ANSWER_MISMATCH", "The raw gate optimum must be the correct option and answer.");
            if (!capacityValid) return;
            int effective = FindOptimum(problem, problem.ResourceCapacity);
            if (effective < 0) Add(issues, "GATE_TIE", "The effective gate optimum is not unique.");
            if (raw >= 0 && effective >= 0 && raw != effective)
                Add(issues, "GATE_ORDER_INVERTED_BY_CAP", "Capacity changes the unique winning gate.");
        }

        private static int FindOptimum(MathProblem problem, int? capacity)
        {
            long best = long.MaxValue;
            int winner = -1;
            bool tie = false;
            for (int i = 0; i < problem.Options.Count; i++)
            {
                long value = problem.Options[i].Value.Value;
                if (capacity.HasValue) value = Math.Min(value, capacity.Value);
                long score = problem.Goal == GateGoal.Maximize ? -value
                    : problem.Goal == GateGoal.Minimize ? value : Math.Abs(value - problem.TargetValue.Value);
                if (score < best) { best = score; winner = i; tie = false; }
                else if (score == best) tie = true;
            }

            return tie ? -1 : winner;
        }

        private static void Add(List<ValidationIssue> issues, string code, string message, ValidationSeverity severity = ValidationSeverity.Error)
        {
            foreach (var issue in issues) if (issue.Code == code) return;
            issues.Add(new ValidationIssue(code, message, severity));
        }
    }
}
