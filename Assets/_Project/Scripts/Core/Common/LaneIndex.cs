using System;

namespace MathHorde.Core.Common
{
    /// <summary>
    /// 0-based lane index (contract: core-domain.md 1.). Valid range is
    /// 0 .. LaneCount-1, where LaneCount is 2..4 (see LevelDefinition.LaneCount).
    /// This type only guards against a negative value at construction time —
    /// the upper bound depends on the level's LaneCount and is validated by
    /// whichever service resolves lane membership (WaveDirector, CombatService).
    /// </summary>
    public readonly struct LaneIndex : IEquatable<LaneIndex>
    {
        public int Value { get; }

        public LaneIndex(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "LaneIndex nem lehet negatív.");
            }

            Value = value;
        }

        public bool Equals(LaneIndex other) => Value == other.Value;

        public override bool Equals(object obj) => obj is LaneIndex other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(LaneIndex left, LaneIndex right) => left.Equals(right);

        public static bool operator !=(LaneIndex left, LaneIndex right) => !left.Equals(right);

        public override string ToString() => Value.ToString();
    }
}
