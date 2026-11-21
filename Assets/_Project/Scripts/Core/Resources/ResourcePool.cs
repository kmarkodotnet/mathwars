using System;

namespace MathHorde.Core.Resources
{
    /// <summary>
    /// Immutable single-resource pool (contract core-domain.md 5.). Invariant:
    /// Min &lt;= Current &lt;= Max always holds — enforced here at construction
    /// by clamping, never by throwing for an out-of-range Current (only
    /// structurally invalid Max/Min combinations are a programmer error and
    /// throw). ResourceService is the only place that produces new instances
    /// of this type during gameplay; it is otherwise a read-only DTO.
    /// </summary>
    public sealed class ResourcePool
    {
        public ResourceType Type { get; }
        public int Current { get; }
        public int Max { get; }
        public int Min { get; }

        /// <summary>True when the pool is fully drained (Current == Min).</summary>
        public bool IsDepleted => Current == Min;

        public ResourcePool(ResourceType type, int current, int max, int min = 0)
        {
            ResourceRules.RequireActive(type);
            if (min < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(min), min, "Min nem lehet negatív.");
            }

            if (max <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "Max > 0 kötelező.");
            }

            if (min >= max)
            {
                throw new ArgumentOutOfRangeException(nameof(min), min, "Min < Max kötelező.");
            }

            Type = type;
            Min = min;
            Max = max;
            Current = Clamp(current, min, max);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
