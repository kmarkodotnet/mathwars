using System;
using MathHorde.Core.Problems;

namespace MathHorde.MathEngine
{
    internal static class GateMathRules
    {
        internal static long Score(long value, GateGoal goal, int? target)
            => goal == GateGoal.Maximize ? -value : goal == GateGoal.Minimize ? value : Math.Abs(value - target.Value);
    }
}
