using System;
using MathHorde.Core.Common;
using MathHorde.Core.Expressions;

namespace MathHorde.Core.Problems
{
    public readonly struct ResourceGateOptionTemplate
    {
        public int OptionIndex { get; }
        public LaneIndex Lane { get; }
        public ExpressionNode GateOperation { get; }
        public string DisplayLabel { get; }

        public ResourceGateOptionTemplate(int optionIndex, LaneIndex lane, ExpressionNode gateOperation, string displayLabel)
        {
            OptionIndex = optionIndex;
            Lane = lane;
            GateOperation = gateOperation ?? throw new ArgumentNullException(nameof(gateOperation));
            DisplayLabel = displayLabel ?? throw new ArgumentNullException(nameof(displayLabel));
        }
    }
}
