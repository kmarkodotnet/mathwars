using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;

namespace MathHorde.MathEngine
{
    public sealed partial class MathProblemGenerator
    {
        public Result<PreparedResourceGate> PrepareResourceGate(ResourceGatePreparationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var offline = PreparationRequest(request);
            string error = ValidateRequest(offline);
            if (error != null) return Result<PreparedResourceGate>.Fail(error);
            var p = request.Difficulty;
            // The concrete winning expression contains the snapshot as a literal.
            // Full coverage therefore cannot extend beyond the operand profile.
            if (p.MinOperandValue > 0 || request.ResourceCapacity > p.MaxOperandValue
                || request.ResourceCapacity == int.MaxValue
                || (long)(p.AnswerOptionCount - 1) * p.MinGateGap > (long)request.ResourceCapacity + (p.AllowNegativeResults ? p.MaxOperandValue : 0))
                return Result<PreparedResourceGate>.Fail("GENERATION_EXHAUSTED");

            var root = new DeterministicRandomSource(request.Seed);
            var expressionRandom = root.Derive("expression");
            var laneRandom = root.Derive("lane-shuffle");
            var id = MathProblemId.New(root.Derive("problem-id"));
            var operators = EligibleOperators(offline);
            operators.Remove(BinaryOperator.IntegerDivisionWithRemainder);
            if (operators.Count == 0) return Result<PreparedResourceGate>.Fail("GENERATION_EXHAUSTED");
            var candidates = new List<ResourceGateTemplate>();
            var metadata = new List<IReadOnlyList<SkillTag>[]>();
            var covered = new bool[request.ResourceCapacity + 1];
            int remaining = covered.Length, maxOperand = p.MaxOperandValue;
            while (true)
            {
                for (int attempt = 0; attempt < AttemptsPerStage; attempt++)
                {
                    var pool = new List<BinaryOperator>(operators);
                    Shuffle(pool, expressionRandom);
                    var operations = new List<ExpressionNode>();
                    for (int i = 0; i < p.AnswerOptionCount; i++)
                        operations.Add(new BinaryOperationNode(pool[i % pool.Count], new UnknownNode(),
                            new ConstantNode(NextInclusive(expressionRandom, p.MinOperandValue, maxOperand))));
                    Shuffle(operations, laneRandom);
                    var options = new ResourceGateOptionTemplate[operations.Count];
                    var symbolicTags = new SortedSet<SkillTag>();
                    for (int i = 0; i < operations.Count; i++)
                    {
                        options[i] = new ResourceGateOptionTemplate(i, new LaneIndex(i), operations[i], GateLabel(operations[i]));
                        symbolicTags.UnionWith(_classifier.Classify(operations[i], ProblemType.ResourceOperationComparison));
                    }
                    var template = new ResourceGateTemplate(options, new List<SkillTag>(symbolicTags));
                    var tagsBySnapshot = new IReadOnlyList<SkillTag>[covered.Length];
                    bool useful = false;
                    for (int snapshot = 0; snapshot < covered.Length; snapshot++)
                    {
                        if (!GateMathRules.TryEvaluate(template, snapshot, request.ResourceCapacity, p,
                            request.Goal, request.TargetValue, out var values, out int winner)) continue;
                        var expression = Substitute(operations[winner], snapshot);
                        var tags = new SortedSet<SkillTag>(symbolicTags);
                        tags.UnionWith(_classifier.Classify(expression, ProblemType.ResourceOperationComparison));
                        var immutableTags = new List<SkillTag>(tags).AsReadOnly();
                        if (!SkillsEligible(immutableTags, request.SkillProfile)) continue;
                        var concreteOptions = new AnswerOption[options.Length];
                        for (int i = 0; i < options.Length; i++)
                            concreteOptions[i] = new AnswerOption(i, options[i].Lane, new EvaluationResult(values.Get(i)),
                                options[i].GateOperation, i == winner, null, options[i].DisplayLabel);
                        // The unchanged validator is the static/contextual oracle
                        // during Prepare only. Bind never creates a problem/report.
                        var candidate = new MathProblem(id, ProblemType.ResourceOperationComparison, expression,
                            expression.ToDisplayString(), new EvaluationResult(values.Get(winner)), concreteOptions,
                            p.ChallengeLevel, immutableTags, request.ThinkTimeSeconds, request.Reward, request.Seed,
                            request.Goal, request.TargetValue, null, request.ResourceCapacity);
                        if (!_validator.IsValid(candidate, p)) continue;
                        tagsBySnapshot[snapshot] = immutableTags;
                        useful = true;
                        if (!covered[snapshot]) { covered[snapshot] = true; remaining--; }
                    }
                    if (useful) { candidates.Add(template); metadata.Add(tagsBySnapshot); }
                    if (remaining == 0)
                        return Result<PreparedResourceGate>.Ok(new PreparedResourceGate(id, request.Seed, p, request.ThinkTimeSeconds,
                            request.Reward, request.Goal, request.TargetValue, request.ResourceCapacity, candidates, metadata));
                }
                int next = p.MinOperandValue + (int)(((long)maxOperand - p.MinOperandValue + 1) / 2);
                if (next == maxOperand) break;
                maxOperand = next;
            }
            return Result<PreparedResourceGate>.Fail("GENERATION_EXHAUSTED");
        }

        public Result<ResourceGateBinding> BindResourceGate(PreparedResourceGate prepared, int snapshotValue, int resourceCapacity)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            string rewardError = ResourceRules.ValidateRewardPair(prepared.Reward.Resource, prepared.Reward.Resource);
            if (rewardError != null) return Result<ResourceGateBinding>.Fail(rewardError);
            if (resourceCapacity <= 0) return Result<ResourceGateBinding>.Fail("GATE_CAPACITY_INVALID");
            if (resourceCapacity != prepared.PreparedCapacity) return Result<ResourceGateBinding>.Fail("GATE_CONTEXT_CHANGED");
            if (snapshotValue < 0 || snapshotValue > resourceCapacity) return Result<ResourceGateBinding>.Fail("GATE_SNAPSHOT_INVALID");
            for (int i = 0; i < prepared.Candidates.Count; i++)
            {
                if (prepared.GetSkillTags(i, snapshotValue) == null) continue;
                if (GateMathRules.TryEvaluate(prepared.Candidates[i], snapshotValue, resourceCapacity, prepared.Difficulty,
                    prepared.Goal, prepared.TargetValue, out var values, out int winner))
                    return Result<ResourceGateBinding>.Ok(new ResourceGateBinding(prepared, i, snapshotValue, resourceCapacity,
                        winner, new EvaluationResult(values.Get(0)), new EvaluationResult(values.Get(1)),
                        new EvaluationResult(values.Get(2)), new EvaluationResult(values.Get(3))));
            }
            return Result<ResourceGateBinding>.Fail("GENERATION_EXHAUSTED");
        }

        private static ProblemGenerationRequest PreparationRequest(ResourceGatePreparationRequest r)
            => new ProblemGenerationRequest
            {
                Seed = r.Seed, Type = ProblemType.ResourceOperationComparison, Difficulty = r.Difficulty,
                SkillProfile = r.SkillProfile, LaneCount = r.LaneCount, ThinkTimeSeconds = r.ThinkTimeSeconds,
                RewardResource = r.RewardResource, Reward = r.Reward, Goal = r.Goal, TargetValue = r.TargetValue,
                ResourceSnapshotValue = 0, ResourceCapacity = r.ResourceCapacity,
            };
    }
}
