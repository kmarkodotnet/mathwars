// Core contract for every source of randomness in the solution
// (core-domain.md 1., ADR-006). Direct instantiation of System.Random (or,
// in Unity, UnityEngine.Random) outside DeterministicRandomSource is
// forbidden anywhere in Core/MathEngine/GameLogic/Analytics — all
// randomness flows through this interface so a run is fully reproducible
// from a single int seed, identically under dotnet test and Unity
// (Mono/IL2CPP).
namespace MathHorde.Core.Common
{
    /// <summary>
    /// Deterministic source of randomness. See DeterministicRandomSource for
    /// the concrete implementation (xoshiro128**, ADR-006).
    /// </summary>
    public interface IRandomSource
    {
        int Seed { get; }

        /// <summary>Unbiased integer in [minInclusive, maxExclusive). maxExclusive must be &gt; minInclusive.</summary>
        int Next(int minInclusive, int maxExclusive);

        /// <summary>Value in [0, 1).</summary>
        double NextDouble();

        /// <summary>
        /// Returns an independent child source for one subsystem (e.g.
        /// "expression", "distractors", "lane-shuffle", "waves"). The
        /// child's sequence depends only on this source's Seed and
        /// purposeKey — never on how many times Next/NextDouble have
        /// already been called on this instance — so changing one
        /// subsystem's call count never shifts another subsystem's output.
        /// Calling Derive twice with the same purposeKey on an equally
        /// seeded source yields children with identical sequences.
        /// </summary>
        IRandomSource Derive(string purposeKey);
    }
}
