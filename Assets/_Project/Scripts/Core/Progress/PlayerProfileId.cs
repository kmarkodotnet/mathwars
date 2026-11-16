using System;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Strong identifier for a local player profile (contract
    /// core-domain.md 6., spec 15.). One device holds one profile in the
    /// MVP (spec 15.: "DisplayName vagy névtelen helyi profil" — a single
    /// local profile, not a multi-profile switcher); the id still exists as
    /// a strong type so a save file is self-describing and future
    /// multi-profile support (not in this task's scope) needs no migration
    /// of this field.
    /// </summary>
    public readonly struct PlayerProfileId : IEquatable<PlayerProfileId>
    {
        public Guid Value { get; }

        public PlayerProfileId(Guid value)
        {
            Value = value;
        }

        public static PlayerProfileId New() => new PlayerProfileId(Guid.NewGuid());

        public bool Equals(PlayerProfileId other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is PlayerProfileId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();
    }
}
