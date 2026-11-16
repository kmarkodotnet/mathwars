using System;
using System.Text;

// ADR-006: .NET does not guarantee that a seeded System.Random produces the
// same sequence across runtimes/versions, but the hybrid test stack
// (ADR-003) runs the SAME seeded test under dotnet test (.NET 9/10) and
// under Unity (Mono/IL2CPP). A fuzz-test failure must reproduce with the
// same int seed in both modes, and balancing-simulation runs must be
// comparable across them. So the PRNG algorithm lives entirely in this
// file, not in any runtime library.
namespace MathHorde.Core.Common
{
    /// <summary>
    /// Deterministic, self-contained PRNG (ADR-006): xoshiro128**
    /// (Blackman &amp; Vigna, public domain) seeded via SplitMix32. Produces
    /// the exact same sequence for the same seed on every .NET/Mono/IL2CPP
    /// runtime because the entire algorithm — generation AND seeding — is
    /// implemented here, never delegated to System.Random or
    /// UnityEngine.Random (both forbidden elsewhere in the solution).
    /// </summary>
    public sealed class DeterministicRandomSource : IRandomSource
    {
        // xoshiro128** generator state (4 x 32 bits). SplitMix32 seeding
        // (see SeedState) guarantees this is never degenerate (all zero)
        // even for seed == 0.
        private uint _s0;
        private uint _s1;
        private uint _s2;
        private uint _s3;

        /// <summary>
        /// The seed this instance was constructed with — NOT the live
        /// internal generator state. Derive(purposeKey) is computed from
        /// this fixed value, so calling Next()/NextDouble() any number of
        /// times on this instance never changes what a later Derive(...)
        /// call produces (ADR-006: one subsystem's call count must never
        /// shift another subsystem's output).
        /// </summary>
        public int Seed { get; }

        public DeterministicRandomSource(int seed)
        {
            Seed = seed;
            SeedState(seed);
        }

        private void SeedState(int seed)
        {
            // SplitMix32 expands the single 32-bit seed into four
            // well-mixed state words so seed 0 (or any other single value)
            // never yields a degenerate all-zero xoshiro128** state.
            uint splitMixState = unchecked((uint)seed);
            _s0 = NextSplitMix32(ref splitMixState);
            _s1 = NextSplitMix32(ref splitMixState);
            _s2 = NextSplitMix32(ref splitMixState);
            _s3 = NextSplitMix32(ref splitMixState);
        }

        private static uint NextSplitMix32(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint z = state;
                z = (z ^ (z >> 16)) * 0x21F0AAADu;
                z = (z ^ (z >> 15)) * 0x735A2D97u;
                z ^= z >> 15;
                return z;
            }
        }

        /// <summary>xoshiro128** core step.</summary>
        private uint NextUInt32()
        {
            unchecked
            {
                uint result = RotateLeft(_s1 * 5u, 7) * 9u;

                uint t = _s1 << 9;

                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;

                _s2 ^= t;
                _s3 = RotateLeft(_s3, 11);

                return result;
            }
        }

        private static uint RotateLeft(uint x, int bits)
        {
            unchecked
            {
                return (x << bits) | (x >> (32 - bits));
            }
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    maxExclusive,
                    "maxExclusive must be greater than minInclusive.");
            }

            uint range = unchecked((uint)((long)maxExclusive - minInclusive));
            uint offset = NextBoundedUInt32(range);
            return unchecked((int)(minInclusive + (long)offset));
        }

        /// <summary>
        /// Unbiased value in [0, exclusiveBound) via rejection sampling —
        /// deliberately NOT a plain "% exclusiveBound", which would skew
        /// low values whenever exclusiveBound does not evenly divide 2^32.
        /// </summary>
        private uint NextBoundedUInt32(uint exclusiveBound)
        {
            unchecked
            {
                uint limit = uint.MaxValue - (uint.MaxValue % exclusiveBound);
                uint value;
                do
                {
                    value = NextUInt32();
                } while (value >= limit);

                return value % exclusiveBound;
            }
        }

        public double NextDouble()
        {
            // 2^32 — maps a full 32-bit draw into [0, 1).
            return NextUInt32() / 4294967296.0;
        }

        public IRandomSource Derive(string purposeKey)
        {
            if (purposeKey == null)
            {
                throw new ArgumentNullException(nameof(purposeKey));
            }

            // Derived from THIS instance's fixed Seed, never from the live
            // s0.._s3 state, so the child's sequence cannot be shifted by
            // how many times Next()/NextDouble() happen to have already
            // been called on the parent (ADR-006).
            uint purposeHash = Fnv1a32(purposeKey);
            uint mixState = unchecked((uint)Seed ^ purposeHash);
            int derivedSeed = unchecked((int)NextSplitMix32(ref mixState));
            return new DeterministicRandomSource(derivedSeed);
        }

        /// <summary>
        /// Stable 32-bit FNV-1a hash over the UTF-8 bytes of purposeKey.
        /// Deliberately NOT string.GetHashCode(), whose value is
        /// randomized per process in .NET and therefore unusable for a
        /// reproducible seed derivation (ADR-006).
        /// </summary>
        private static uint Fnv1a32(string text)
        {
            unchecked
            {
                const uint OffsetBasis = 2166136261u;
                const uint Prime = 16777619u;

                uint hash = OffsetBasis;
                if (string.IsNullOrEmpty(text))
                {
                    return hash;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(text);
                foreach (byte b in bytes)
                {
                    hash ^= b;
                    hash *= Prime;
                }

                return hash;
            }
        }
    }
}
