using System;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Strong identifier for a level, e.g. "w1-l03" (contract core-domain.md 6.).
    /// Needed by LevelDefinition.Id (Core.Levels, F1-02) and by the
    /// SessionStarted event payload (F1-05) — see the GameSessionId.cs comment
    /// for why this one type is introduced ahead of the rest of Progress.
    /// </summary>
    public readonly struct LevelId : IEquatable<LevelId>
    {
        public string Value { get; }

        public LevelId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool Equals(LevelId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LevelId other && Equals(other);

        public override int GetHashCode() => Value?.GetHashCode() ?? 0;

        public override string ToString() => Value;
    }
}
