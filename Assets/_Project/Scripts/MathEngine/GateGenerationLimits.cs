using System;

namespace MathHorde.MathEngine
{
    /// <summary>Immutable global gate constraints, copied from MathEventConfigSO.</summary>
    public readonly struct GateGenerationLimits
    {
        public int OperandMin { get; }
        public int OperandMax { get; }
        public int MultiplierMin { get; }
        public int MultiplierMax { get; }
        public GateGenerationLimits(int operandMin, int operandMax, int multiplierMin, int multiplierMax)
        {
            if (operandMin < 1 || operandMax < operandMin || multiplierMin < 2 || multiplierMax < multiplierMin)
                throw new ArgumentOutOfRangeException(nameof(operandMin), "GATE_LIMITS_INVALID");
            OperandMin = operandMin; OperandMax = operandMax;
            MultiplierMin = multiplierMin; MultiplierMax = multiplierMax;
        }
        internal bool IsValid => OperandMin >= 1 && OperandMax >= OperandMin && MultiplierMin >= 2 && MultiplierMax >= MultiplierMin;
    }
}
