using System;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Strong identifier for a world, e.g. "w1" (contract core-domain.md 6.).
    /// Needed by LevelDefinition.WorldId (Core.Levels, F1-02) — see
    /// GameSessionId.cs for why this one type is introduced ahead of the rest
    /// of Progress.
    /// </summary>
    public readonly struct WorldId : IEquatable<WorldId>
    {
        public string Value { get; }

        public WorldId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool Equals(WorldId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is WorldId other && Equals(other);

        public override int GetHashCode() => Value?.GetHashCode() ?? 0;

        public override string ToString() => Value;
    }
}
