using System.Collections.Generic;

namespace MathHorde.Core.Resources
{
    /// <summary>
    /// Immutable snapshot of a whole session's resource pools (contract
    /// core-domain.md 5.). "Immutable modification" means WithPool returns a
    /// new instance — nothing here is ever mutated in place, which is what
    /// lets ResourceService hand out CaptureSnapshot() values that can never
    /// be invalidated by a later Pools lookup.
    /// </summary>
    public sealed class ResourceState
    {
        private readonly IReadOnlyDictionary<ResourceType, ResourcePool> _pools;

        public IReadOnlyDictionary<ResourceType, ResourcePool> Pools => _pools;

        public ResourceState(IReadOnlyDictionary<ResourceType, ResourcePool> pools)
        {
            _pools = pools ?? new Dictionary<ResourceType, ResourcePool>();
        }

        /// <summary>Unknown/never-initialized resource type returns 0, never throws.</summary>
        public int GetCurrent(ResourceType type) =>
            _pools.TryGetValue(type, out var pool) ? pool.Current : 0;

        /// <summary>Unknown/never-initialized resource type returns 0, never throws.</summary>
        public int GetMax(ResourceType type) =>
            _pools.TryGetValue(type, out var pool) ? pool.Max : 0;

        /// <summary>Returns a new ResourceState with the given pool replacing (or adding) its type's entry.</summary>
        public ResourceState WithPool(ResourcePool pool)
        {
            var next = new Dictionary<ResourceType, ResourcePool>(_pools)
            {
                [pool.Type] = pool
            };
            return new ResourceState(next);
        }
    }
}
