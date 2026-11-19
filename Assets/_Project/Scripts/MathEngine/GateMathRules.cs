using System;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    // Shared by offline validation and runtime binding. Arithmetic uses wide
    // intermediates, so a rejected overflow does not allocate an exception.
    internal static class GateMathRules
    {
        internal static long Score(long value, GateGoal goal, int? target)
            => goal == GateGoal.Maximize ? -value : goal == GateGoal.Minimize ? value : Math.Abs(value - target.Value);

        internal static bool IsTie(long a, long b, int? capacity)
            => a == b || (capacity.HasValue && Math.Min(a, capacity.Value) == Math.Min(b, capacity.Value));

        internal static bool GapTooSmall(long a, long b, int capacity, int gap)
            => Math.Abs(Math.Min(a, capacity) - Math.Min(b, capacity)) < gap;

        internal static bool TryEvaluate(ResourceGateTemplate template, int snapshot, int capacity,
            MathHorde.Core.Skills.DifficultyProfile profile, GateGoal goal, int? target,
            out GateValues values, out int winner)
        {
            values = default; winner = -1;
            int v0 = 0, v1 = 0, v2 = 0, v3 = 0;
            for (int i = 0; i < template.Options.Count; i++)
            {
                if (!(template.Options[i].GateOperation is BinaryOperationNode operation)
                    || !(operation.Left is UnknownNode) || !(operation.Right is ConstantNode operand)
                    || !TryScalar(operation.Operator, snapshot, operand.Value, out int value)
                    || value > profile.MaxResultValue || (!profile.AllowNegativeResults && value < 0)) return false;
                switch (i) { case 0: v0 = value; break; case 1: v1 = value; break; case 2: v2 = value; break; default: v3 = value; break; }
            }
            values = new GateValues(v0, v1, v2, v3);
            int raw = -1, effective = -1;
            long bestRaw = long.MaxValue, bestEffective = long.MaxValue;
            bool rawTie = false, effectiveTie = false;
            for (int i = 0; i < template.Options.Count; i++)
            {
                long a = values.Get(i);
                for (int j = 0; j < i; j++)
                    if (IsTie(a, values.Get(j), capacity) || GapTooSmall(a, values.Get(j), capacity, profile.MinGateGap)) return false;
                long r = Score(a, goal, target), e = Score(Math.Min(a, capacity), goal, target);
                if (r < bestRaw) { bestRaw = r; raw = i; rawTie = false; } else if (r == bestRaw) rawTie = true;
                if (e < bestEffective) { bestEffective = e; effective = i; effectiveTie = false; } else if (e == bestEffective) effectiveTie = true;
            }
            if (rawTie || effectiveTie || raw != effective) return false;
            // A minimizing armor gate must actually reduce armor (7.1).
            if (goal == GateGoal.Minimize && values.Get(raw) >= snapshot) return false;
            winner = raw;
            return true;
        }

        private static bool TryScalar(BinaryOperator op, int a, int b, out int result)
        {
            long value;
            switch (op)
            {
                case BinaryOperator.Addition: value = (long)a + b; break;
                case BinaryOperator.Subtraction: value = (long)a - b; break;
                case BinaryOperator.Multiplication: value = (long)a * b; break;
                case BinaryOperator.Division:
                    if (b == 0 || (long)a % b != 0) { result = 0; return false; }
                    value = (long)a / b; break;
                case BinaryOperator.Modulo:
                    if (b <= 0) { result = 0; return false; }
                    value = ((long)a % b + b) % b; break;
                default: result = 0; return false;
            }
            if (value < int.MinValue || value > int.MaxValue) { result = 0; return false; }
            result = (int)value;
            return true;
        }
    }

    internal readonly struct GateValues
    {
        private readonly int _v0, _v1, _v2, _v3;
        public GateValues(int v0, int v1, int v2, int v3) { _v0 = v0; _v1 = v1; _v2 = v2; _v3 = v3; }
        public int Get(int index) => index == 0 ? _v0 : index == 1 ? _v1 : index == 2 ? _v2 : _v3;
    }
}
