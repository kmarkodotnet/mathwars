namespace MathHorde.Core.Resources
{
    /// <summary>
    /// Describes a single applied change (contract core-domain.md 5.) — feeds
    /// the event log and the HUD delta animation. AppliedDelta can differ from
    /// RequestedDelta only because of Min/Max clamping (WasClamped records
    /// that), never because of an exception — clamping never throws.
    /// </summary>
    public readonly struct ResourceChange
    {
        public ResourceType Type { get; }
        public int PreviousValue { get; }
        public int NewValue { get; }
        public int RequestedDelta { get; }
        public int AppliedDelta { get; }
        public bool WasClamped { get; }

        /// <summary>"WEAPON_FIRE", "MATH_GATE", "ENEMY_CONTACT", "PICKUP", "LEVEL_INIT", ...</summary>
        public string ReasonCode { get; }

        public ResourceChange(
            ResourceType type,
            int previousValue,
            int newValue,
            int requestedDelta,
            int appliedDelta,
            bool wasClamped,
            string reasonCode)
        {
            Type = type;
            PreviousValue = previousValue;
            NewValue = newValue;
            RequestedDelta = requestedDelta;
            AppliedDelta = appliedDelta;
            WasClamped = wasClamped;
            ReasonCode = reasonCode;
        }
    }
}
