using System;
using MathHorde.Core.Common;

namespace MathHorde.Core.Problems
{
    /// <summary>
    /// Strong identifier for a generated math problem (contract
    /// core-domain.md 3.). Introduced ahead of the rest of the Problems
    /// package — see SkillTag.cs — because QuestionAttempt (Core.Progress,
    /// spec 15.) needs it as its QuestionId field type. The generator's own
    /// New(IRandomSource) factory is included because the contract specifies
    /// it; nothing in this task's scope calls it yet (no MathProblem exists
    /// to generate one from) — that call site lands with the MathEngine/
    /// Problems task.
    /// </summary>
    public readonly struct MathProblemId : IEquatable<MathProblemId>
    {
        public Guid Value { get; }

        public MathProblemId(Guid value)
        {
            Value = value;
        }

        /// <summary>
        /// Derives a new id from the given random source (ADR-006 —
        /// System.Random/Guid.NewGuid() are forbidden in the engine layers;
        /// every random byte comes from IRandomSource so a seeded run is
        /// fully reproducible).
        /// </summary>
        public static MathProblemId New(IRandomSource rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            var bytes = new byte[16];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)rng.Next(0, 256);
            }

            return new MathProblemId(new Guid(bytes));
        }

        public bool Equals(MathProblemId other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is MathProblemId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();
    }
}
