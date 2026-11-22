using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;

namespace MathHorde.MathEngine
{
    public sealed partial class MathProblemGenerator
    {
        // Finite construction, outside Tick. We search legal operations rather than
        // hoping that a narrow CL1/gap8 profile survives random rejection sampling.
        private Result<MathProblem> GenerateIndependentGate(ProblemGenerationRequest request,
            List<BinaryOperator> operators, IRandomSource rng, IRandomSource lanes, MathProblemId id, int lo, int hi)
        {
            var profile = request.Difficulty;
            if ((long)(profile.AnswerOptionCount - 1) * profile.MinGateGap
                > (long)profile.MaxResultValue * (profile.AllowNegativeResults ? 2 : 1))
                return Result<MathProblem>.Fail("GENERATION_EXHAUSTED");
            for (int stage = 0; stage < 4; stage++)
            {
                if (stage == 1) operators.RemoveAll(op => op != BinaryOperator.Addition && op != BinaryOperator.Subtraction);
                if (stage == 2) hi = lo + (hi - lo) / 2;
                int operandHi = stage == 3 ? (int)Math.Min(hi, (long)lo + profile.MinGateGap) : hi;
                var pairs = new List<GatePair>();
                foreach (var op in operators)
                {
                    int min = op == BinaryOperator.Multiplication || op == BinaryOperator.Division
                        ? Math.Max(lo, request.GateLimits.Value.MultiplierMin) : lo;
                    int max = op == BinaryOperator.Multiplication || op == BinaryOperator.Division
                        ? Math.Min(operandHi, request.GateLimits.Value.MultiplierMax) : operandHi;
                    // SO ranges are <=200. Oversized direct requests are sampled with
                    // the same bounded work; no request can allocate an unbounded array.
                    foreach (int k in CandidateValues(min, max, rng)) pairs.Add(new GatePair(op, k));
                }
                Shuffle(pairs, rng);
                var bases = CandidateValues(lo, hi, rng);
                var selected = new GatePair[profile.AnswerOptionCount];
                foreach (int basis in bases)
                {
                    var legal = new List<GateCandidate>();
                    var distinct = new HashSet<BinaryOperator>();
                    foreach (var pair in pairs)
                        if (TryGateChoice(request, pair, basis, out Choice choice))
                        { legal.Add(new GateCandidate(pair, choice.Value.Value)); distinct.Add(pair.Operator); }
                    // Preserve the seeded operation order. Sorting by value here
                    // would turn operand size into a winning answer strategy.
                    bool found = distinct.Count >= selected.Length && SelectGatePairs(legal, selected, 0, 0, profile.MinGateGap, true, request);
                    if (!found) found = SelectGatePairs(legal, selected, 0, 0, profile.MinGateGap, false, request);
                    if (!found) continue;
                    // Operations are fixed first; publish the first valid common
                    // operand in the seeded permutation, exactly as the contract requires.
                    foreach (int finalBase in bases)
                    {
                        var choices = new List<Choice>();
                        bool valid = true;
                        for (int i = 0; i < selected.Length; i++)
                        {
                            if (!TryGateChoice(request, selected[i], finalBase, out Choice choice)) { valid = false; break; }
                            choices.Add(choice);
                        }
                        if (!valid || !TryWinner(choices, request, out int winner)) continue;
                        var best = choices[winner];
                        choices[winner] = new Choice(best.Value, best.Operation, true, null);
                        var result = Assemble(request, best.Operation, best.Value, choices, lanes, id, finalBase);
                        if (_validator.IsValid(result.Value, profile)) return result;
                    }
                }
            }
            return Result<MathProblem>.Fail("GENERATION_EXHAUSTED");
        }

        private bool TryGateChoice(ProblemGenerationRequest request, GatePair pair, int basis, out Choice choice)
        {
            choice = default;
            if (pair.Operator == BinaryOperator.Division && basis % pair.Operand != 0) return false;
            var tree = new BinaryOperationNode(pair.Operator, new ConstantNode(basis), new ConstantNode(pair.Operand));
            var value = _evaluator.Evaluate(tree, new EvaluationOptions
            { AllowNegativeResult = request.Difficulty.AllowNegativeResults, AllowNegativeIntermediate = request.Difficulty.AllowNegativeResults,
                MinValue = int.MinValue, MaxValue = int.MaxValue });
            if (!value.IsSuccess || value.Value.Remainder.HasValue || value.Value.Value > request.Difficulty.MaxResultValue) return false;
            var tags = _classifier.Classify(tree, ProblemType.ResourceOperationComparison);
            foreach (var tag in tags) if (!request.Difficulty.IsSkillUnlocked(tag)) return false;
            if (!SkillsEligible(tags, request.SkillProfile)) return false;
            choice = new Choice(value.Value, tree, false, null);
            return true;
        }

        private static List<int> CandidateValues(int min, int max, IRandomSource rng)
        {
            var values = new List<int>();
            if (min > max) return values;
            if ((long)max - min < 1024)
                for (long i = min; i <= max; i++) values.Add((int)i);
            else
            {
                values.Add(min); values.Add(max);
                for (int i = 0; i < 64; i++) values.Add(NextInclusive(rng, min, max));
            }
            Shuffle(values, rng);
            return values;
        }

        private static bool SelectGatePairs(List<GateCandidate> legal, GatePair[] selected, int depth,
            int start, int gap, bool distinct, ProblemGenerationRequest request)
        {
            if (depth == selected.Length) return true;
            for (int i = start; i < legal.Count; i++)
            {
                bool allowed = true;
                for (int j = 0; j < depth; j++)
                {
                    if (distinct && selected[j].Operator == legal[i].Pair.Operator) { allowed = false; break; }
                    // selected values can be recovered from the candidate list without arithmetic.
                    for (int k = 0; k < legal.Count; k++)
                        if (legal[k].Pair.Equals(selected[j])
                            && (Math.Abs((long)legal[i].Value - legal[k].Value) < Math.Max(1, gap)
                                || GateMathRules.Score(legal[i].Value, request.Goal, request.TargetValue)
                                    == GateMathRules.Score(legal[k].Value, request.Goal, request.TargetValue)))
                        { allowed = false; break; }
                    if (!allowed) break;
                }
                if (!allowed) continue;
                selected[depth] = legal[i].Pair;
                if (SelectGatePairs(legal, selected, depth + 1, i + 1, gap, distinct, request)) return true;
            }
            return false;
        }

        private static bool TryWinner(List<Choice> choices, ProblemGenerationRequest request, out int winner)
        {
            winner = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                for (int j = 0; j < i; j++)
                    if (Math.Abs((long)choices[i].Value.Value - choices[j].Value.Value) < Math.Max(1, request.Difficulty.MinGateGap)) return false;
                if (Score(choices[i].Value.Value, request) < Score(choices[winner].Value.Value, request)) winner = i;
            }
            for (int i = 0; i < choices.Count; i++)
                if (i != winner && Score(choices[i].Value.Value, request) == Score(choices[winner].Value.Value, request)) return false;
            return true;
        }

        private readonly struct GatePair : IEquatable<GatePair>
        {
            internal readonly BinaryOperator Operator;
            internal readonly int Operand;
            internal GatePair(BinaryOperator op, int operand) { Operator = op; Operand = operand; }
            public bool Equals(GatePair other) => Operator == other.Operator && Operand == other.Operand;
        }
        private readonly struct GateCandidate
        {
            internal readonly GatePair Pair;
            internal readonly int Value;
            internal GateCandidate(GatePair pair, int value) { Pair = pair; Value = value; }
        }
    }
}
