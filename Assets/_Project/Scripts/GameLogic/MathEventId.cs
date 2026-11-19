using System;

namespace MathHorde.GameLogic
{
    /// <summary>Deterministic session/event identity (game-logic.md 5.).</summary>
    public readonly struct MathEventId : IEquatable<MathEventId>
    {
        public Guid Value { get; }

        public MathEventId(Guid value)
        {
            Value = value;
        }

        public static MathEventId Create(int sessionSeed, int eventIndex)
        {
            // Preserve both inputs without hashing or a platform-dependent byte order.
            // The remaining bytes identify this domain and format version ("MathEvt", 1).
            return new MathEventId(new Guid(sessionSeed, unchecked((short)eventIndex),
                unchecked((short)(eventIndex >> 16)), 0x4d, 0x61, 0x74, 0x68, 0x45, 0x76, 0x74, 1));
        }

        public bool Equals(MathEventId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is MathEventId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(MathEventId left, MathEventId right) => left.Equals(right);
        public static bool operator !=(MathEventId left, MathEventId right) => !left.Equals(right);
        public override string ToString() => Value.ToString();
    }
}
