using System;
using System.Collections.Generic;
using System.Globalization;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.Core.Skills;

namespace MathHorde.MathEngine
{
    /// <summary>
    /// Stateless orchestration of math-engine.md 7.'s ten steps. Retries keep
    /// advancing independent local streams. Simplification narrows the operand
    /// range and maximum count, never the profile's minimum or unlocked skills.
    /// Batch item zero replays Generate(request); later items use independent
    /// derived seeds, so prefetch size and failed items cannot shift a prefix.
    /// </summary>
    public sealed partial class MathProblemGenerator : IMathProblemGenerator
    {
        private const int AttemptsPerStage = 16;
        private readonly IExpressionGenerator _expressions;
        private readonly IExpressionEvaluator _evaluator;
        private readonly IEquationSolver _solver;
        private readonly IDistractorGenerator _distractors;
        private readonly ISkillClassifier _classifier;
        private readonly IProblemValidator _validator;

        public MathProblemGenerator() : this(new ExpressionEvaluator(), new SkillClassifier()) { }

        private MathProblemGenerator(IExpressionEvaluator evaluator, ISkillClassifier classifier)
            : this(new ExpressionGenerator(evaluator, new EquationSolver(evaluator)), evaluator,
                new EquationSolver(evaluator), new DistractorGenerator(), classifier,
                new ProblemValidator(evaluator, new EquationSolver(evaluator), classifier)) { }

        public MathProblemGenerator(IExpressionGenerator expressions, IExpressionEvaluator evaluator,
            IEquationSolver solver, IDistractorGenerator distractors, ISkillClassifier classifier,
            IProblemValidator validator)
        {
            _expressions = expressions ?? throw new ArgumentNullException(nameof(expressions));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _solver = solver ?? throw new ArgumentNullException(nameof(solver));
            _distractors = distractors ?? throw new ArgumentNullException(nameof(distractors));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public Result<MathProblem> Generate(ProblemGenerationRequest request)
        {
            string error = ValidateRequest(request);
            if (error != null) return Result<MathProblem>.Fail(error);

            var rng = new DeterministicRandomSource(request.Seed);
            var rngExpr = rng.Derive("expression");
            var rngDist = rng.Derive("distractors");
            var rngLane = rng.Derive("lane-shuffle");
            var profile = request.Difficulty;
            var operators = EligibleOperators(request);
            if (request.Type == ProblemType.ResourceOperationComparison)
            {
                operators.RemoveAll(op => op != BinaryOperator.Addition && op != BinaryOperator.Subtraction
                    && op != BinaryOperator.Multiplication && op != BinaryOperator.Division);
                if (operators.Count == 0) return Result<MathProblem>.Fail("GATE_OPERATOR_SET_EMPTY");
                int lo = Math.Max(profile.MinOperandValue, request.GateLimits.Value.OperandMin);
                int hi = Math.Min(profile.MaxOperandValue, request.GateLimits.Value.OperandMax);
                if (lo > hi) return Result<MathProblem>.Fail("GATE_LIMITS_INVALID");
                if ((operators.Contains(BinaryOperator.Multiplication) || operators.Contains(BinaryOperator.Division))
                    && Math.Max(lo, request.GateLimits.Value.MultiplierMin) > Math.Min(hi, request.GateLimits.Value.MultiplierMax))
                    return Result<MathProblem>.Fail("GATE_LIMITS_INVALID");
                return GenerateIndependentGate(request, operators, rngExpr, rngLane,
                    MathProblemId.New(rng.Derive("problem-id")), lo, hi);
            }
            if (operators.Count == 0) return Result<MathProblem>.Fail("GENERATION_EXHAUSTED");
            var evaluation = new EvaluationOptions
            {
                AllowNegativeIntermediate = profile.AllowNegativeResults,
                AllowNegativeResult = profile.AllowNegativeResults,
                MinValue = int.MinValue, MaxValue = int.MaxValue,
            };
            int maxCount = profile.MaxOperandCount, maxOperand = profile.MaxOperandValue;
            // ExpressionGenerator's inclusive sampler uses max + 1. Narrow
            // that one unsupported endpoint rather than leaking an overflow
            // exception; generated operands still obey the original profile.
            if (request.Type != ProblemType.ResourceOperationComparison && maxOperand == int.MaxValue)
            {
                maxOperand--;
                if (maxOperand <= profile.MinOperandValue) return Result<MathProblem>.Fail("GENERATION_EXHAUSTED");
            }
            // The identifier has its own stream and cannot perturb arithmetic or lanes.
            var id = MathProblemId.New(rng.Derive("problem-id"));
            while (true)
            {
                for (int attempt = 0; attempt < AttemptsPerStage; attempt++)
                {
                    var candidate = GenerateAnswer(request, operators, maxCount, maxOperand, rngExpr, rngDist, rngLane, evaluation, id);
                    if (candidate.IsSuccess && _validator.Validate(candidate.Value, profile).IsValid
                        && SkillsEligible(candidate.Value.SkillTags, request.SkillProfile))
                        return candidate;
                }

                int nextCount = Math.Max(profile.MinOperandCount, maxCount - 1);
                int nextOperand = profile.MinOperandValue + (int)(((long)maxOperand - profile.MinOperandValue + 1) / 2);
                if (nextCount == maxCount && nextOperand == maxOperand) break;
                maxCount = nextCount;
                maxOperand = nextOperand;
            }

            return Result<MathProblem>.Fail("GENERATION_EXHAUSTED");
        }

        public IReadOnlyList<Result<MathProblem>> GenerateBatch(ProblemGenerationRequest request, int count)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var results = new Result<MathProblem>[count];
            var root = new DeterministicRandomSource(request.Seed);
            for (int i = 0; i < count; i++)
            {
                int seed = i == 0 ? request.Seed : root.Derive("batch-" + i.ToString(CultureInfo.InvariantCulture)).Seed;
                results[i] = Generate(CopyWithSeed(request, seed));
            }

            return Array.AsReadOnly(results);
        }

        private Result<MathProblem> GenerateAnswer(ProblemGenerationRequest request, IReadOnlyCollection<BinaryOperator> operators,
            int maxCount, int maxOperand, IRandomSource rngExpr, IRandomSource rngDist, IRandomSource rngLane,
            EvaluationOptions evaluation, MathProblemId id)
        {
            var p = request.Difficulty;
            bool unknown = request.Type == ProblemType.MissingOperand || request.Type == ProblemType.EquationSolving;
            bool target = request.Type == ProblemType.TargetValueSelection;
            var generated = _expressions.Generate(new ExpressionGenerationRequest
            {
                Random = rngExpr, AllowedOperators = operators,
                MinOperandValue = p.MinOperandValue, MaxOperandValue = maxOperand,
                MaxResultValue = unknown ? Math.Min(p.MaxResultValue, maxOperand) : p.MaxResultValue,
                MinOperandCount = p.MinOperandCount, MaxOperandCount = maxCount,
                AllowParentheses = p.AllowParentheses, MaxParenthesisDepth = p.MaxParenthesisDepth,
                IncludeUnknown = unknown || target,
                UnknownPlacement = unknown || target ? UnknownPlacement.EitherSide : UnknownPlacement.None,
                Evaluation = evaluation, PreferredSkills = p.UnlockedSkills,
            });
            if (!generated.IsSuccess) return Result<MathProblem>.Fail(generated.ErrorCode);
            ExpressionNode expression = generated.Value.Root;
            Result<EvaluationResult> answer;
            if (target)
            {
                // Solve a temporary construction equation, then substitute back:
                // the final target-selection expression contains no unknown.
                var equation = expression as EqualityNode;
                if (equation == null) return Result<MathProblem>.Fail("GENERATION_EXHAUSTED");
                ExpressionNode withUnknown = _evaluator.CountUnknowns(equation.Left) > 0 ? equation.Left : equation.Right;
                var solved = _solver.SolveForTarget(withUnknown, request.TargetValue.Value, evaluation);
                if (!solved.IsSuccess) return Result<MathProblem>.Fail(solved.ErrorCode);
                expression = Substitute(withUnknown, solved.Value);
                answer = _evaluator.Evaluate(expression, evaluation);
                if (answer.IsSuccess && answer.Value != new EvaluationResult(request.TargetValue.Value))
                    return Result<MathProblem>.Fail("ANSWER_MISMATCH");
            }
            else if (unknown)
            {
                if (!(expression is EqualityNode equation)) return Result<MathProblem>.Fail("UNKNOWN_NOT_UNIQUE");
                var solved = _solver.Solve(equation, evaluation);
                answer = solved.IsSuccess ? Result<EvaluationResult>.Ok(new EvaluationResult(solved.Value))
                    : Result<EvaluationResult>.Fail(solved.ErrorCode);
            }
            else answer = _evaluator.Evaluate(expression, evaluation);
            if (!answer.IsSuccess) return Result<MathProblem>.Fail(answer.ErrorCode);

            var wrong = _distractors.Generate(new DistractorRequest
            {
                Random = rngDist, Expression = expression, CorrectAnswer = answer.Value,
                Count = p.AnswerOptionCount - 1, MinValue = p.AllowNegativeResults ? -p.MaxResultValue : 0,
                MaxValue = p.MaxResultValue, AllowNegative = p.AllowNegativeResults,
            });
            if (!wrong.IsSuccess) return Result<MathProblem>.Fail(wrong.ErrorCode);
            var choices = new List<Choice> { new Choice(answer.Value, null, true, null) };
            foreach (var distractor in wrong.Value)
                choices.Add(new Choice(distractor.Value, null, false, distractor.ErrorModel));
            return Assemble(request, expression, answer.Value, choices, rngLane, id);
        }

        private Result<MathProblem> Assemble(ProblemGenerationRequest request, ExpressionNode expression,
            EvaluationResult answer, List<Choice> choices, IRandomSource rngLane, MathProblemId id, int? gateBaseValue = null)
        {
            Shuffle(choices, rngLane);
            var options = new AnswerOption[choices.Count];
            var tags = new SortedSet<SkillTag>(_classifier.Classify(expression, request.Type));
            for (int i = 0; i < choices.Count; i++)
            {
                var c = choices[i];
                string label = c.Operation == null ? c.Value.ToDisplayString() : GateLabel(c.Operation);
                options[i] = new AnswerOption(i, new LaneIndex(i), c.Value, c.Operation, c.Correct, c.Error, label);
                if (c.Operation != null) tags.UnionWith(_classifier.Classify(c.Operation, request.Type));
            }

            var skillTags = new List<SkillTag>(tags).AsReadOnly();
            string display = gateBaseValue.HasValue ? gateBaseValue.Value.ToString(CultureInfo.InvariantCulture) : expression.ToDisplayString();
            if (!(expression is EqualityNode) && request.Type != ProblemType.ResourceOperationComparison) display += " = ?";
            return Result<MathProblem>.Ok(new MathProblem(id, request.Type, expression, display, answer,
                Array.AsReadOnly(options), request.Difficulty.ChallengeLevel, skillTags, request.ThinkTimeSeconds,
                request.Reward, request.Seed, request.Goal, request.TargetValue, null, gateBaseValue));
        }

        private List<BinaryOperator> EligibleOperators(ProblemGenerationRequest request)
        {
            var result = new List<BinaryOperator>();
            foreach (var op in request.Difficulty.AllowedOperators)
            {
                var tag = op == BinaryOperator.Addition ? SkillTag.Addition : op == BinaryOperator.Subtraction ? SkillTag.Subtraction
                    : op == BinaryOperator.Multiplication ? SkillTag.Multiplication : SkillTag.Division;
                if (request.Difficulty.IsSkillUnlocked(tag)
                    && (request.SkillProfile == null || _classifier.IsEligible(tag, request.SkillProfile))) result.Add(op);
            }
            result.Sort((a, b) => ((int)a).CompareTo((int)b));
            return result;
        }

        private bool SkillsEligible(IReadOnlyList<SkillTag> tags, SkillProfile profile)
        {
            if (profile == null) return true;
            foreach (var tag in tags) if (!_classifier.IsEligible(tag, profile)) return false;
            return true;
        }

        private static string ValidateRequest(ProblemGenerationRequest r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (r.Difficulty == null) throw new ArgumentNullException(nameof(r.Difficulty));
            if (r.Reward == null) throw new ArgumentNullException(nameof(r.Reward));
            string rewardError = ResourceRules.ValidateRewardPair(r.RewardResource, r.Reward.Resource);
            if (rewardError != null) return rewardError;
            bool gate = r.Type == ProblemType.ResourceOperationComparison;
            if (gate ? !r.GateLimits.HasValue || !r.GateLimits.Value.IsValid : r.GateLimits.HasValue)
                return "GATE_LIMITS_INVALID";
            if (r.Reward.FlatAmount <= 0 || r.Reward.IncorrectAmount < 0 || r.Reward.IncorrectAmount > r.Reward.FlatAmount)
                return "REWARD_AMOUNT_INVALID";
            if (gate && r.Goal == GateGoal.ReachTarget && !r.TargetValue.HasValue) return "GATE_TARGET_MISSING";
            if (!Enum.IsDefined(typeof(ProblemType), r.Type) || !Enum.IsDefined(typeof(GateGoal), r.Goal)
                || !Enum.IsDefined(typeof(ResourceType), r.RewardResource) || r.RewardResource != r.Reward.Resource
                || r.LaneCount < 2 || r.LaneCount > 4 || r.LaneCount < r.Difficulty.AnswerOptionCount
                || !(r.ThinkTimeSeconds > 0f) || float.IsInfinity(r.ThinkTimeSeconds)) return "INVALID_REQUEST";
            bool unknown = r.Type == ProblemType.MissingOperand || r.Type == ProblemType.EquationSolving;
            if (!gate && r.Difficulty.UnknownOperandCount != (unknown ? 1 : 0)) return "INVALID_REQUEST";
            if (r.Type == ProblemType.TargetValueSelection && !r.TargetValue.HasValue) return "INVALID_REQUEST";
            if (r.TargetValue.HasValue && r.Type != ProblemType.TargetValueSelection && !(gate && r.Goal == GateGoal.ReachTarget))
                return "INVALID_REQUEST";
            return null;
        }

        private static ProblemGenerationRequest CopyWithSeed(ProblemGenerationRequest r, int seed)
            => new ProblemGenerationRequest
            {
                Seed = seed, Type = r.Type, Difficulty = r.Difficulty, SkillProfile = r.SkillProfile,
                LaneCount = r.LaneCount, ThinkTimeSeconds = r.ThinkTimeSeconds,
                RewardResource = r.RewardResource, Reward = r.Reward, Goal = r.Goal,
                GateLimits = r.GateLimits, TargetValue = r.TargetValue,
            };

        private static ExpressionNode Substitute(ExpressionNode node, int value)
        {
            if (node is UnknownNode) return new ConstantNode(value);
            if (node is BinaryOperationNode b)
                return new BinaryOperationNode(b.Operator, Substitute(b.Left, value), Substitute(b.Right, value), b.ForceParentheses);
            return node;
        }

        private static long Score(int value, ProblemGenerationRequest r)
            => r.Goal == GateGoal.Maximize ? -(long)value : r.Goal == GateGoal.Minimize ? value : Math.Abs((long)value - r.TargetValue.Value);

        private static int NextInclusive(IRandomSource rng, int min, int max)
            => max < int.MaxValue ? rng.Next(min, max + 1) : (int)(min + (long)(rng.NextDouble() * ((long)max - min + 1)));

        private static string GateLabel(ExpressionNode operation)
        {
            var binary = (BinaryOperationNode)operation;
            // AST owns operator glyphs; remove the common generated operand.
            string text = binary.ToDisplayString();
            return text.Substring(binary.Left.ToDisplayString().Length).Trim().Replace(" ", "");
        }

        private static void Shuffle<T>(IList<T> values, IRandomSource random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                T temp = values[i]; values[i] = values[j]; values[j] = temp;
            }
        }

        private readonly struct Choice
        {
            public EvaluationResult Value { get; }
            public ExpressionNode Operation { get; }
            public bool Correct { get; }
            public ErrorCategory? Error { get; }
            public Choice(EvaluationResult value, ExpressionNode operation, bool correct, ErrorCategory? error)
            { Value = value; Operation = operation; Correct = correct; Error = error; }
        }
    }
}
