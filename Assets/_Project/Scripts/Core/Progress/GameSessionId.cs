using System;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// Strong identifier for a played session (contract core-domain.md 6.).
    /// Added ahead of the full Progress/Session package (PlayerProfile,
    /// GameSessionRecord, ...) only because the F1-05 event catalog
    /// (SessionStarted/SessionEnded) needs it to carry a well-typed payload —
    /// see MathHorde.GameLogic.Events. The remaining Progress types are a
    /// separate task's scope.
    /// </summary>
    public readonly struct GameSessionId : IEquatable<GameSessionId>
    {
        public Guid Value { get; }

        public GameSessionId(Guid value)
        {
            Value = value;
        }

        public static GameSessionId New() => new GameSessionId(Guid.NewGuid());

        public bool Equals(GameSessionId other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is GameSessionId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();
    }
}
