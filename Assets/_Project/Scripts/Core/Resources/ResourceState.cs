using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MathHorde.Core.Resources
{
    /// <summary>
    /// Immutable snapshot of a whole session's resource pools (contract
    /// core-domain.md 5.). "Immutable modification" means WithPool returns a
    /// new instance — nothing here is ever mutated in place, which is what
    /// lets readers retain immutable state values that can never
    /// be invalidated by a later Pools lookup.
    /// </summary>
    public sealed class ResourceState
    {
        private readonly IReadOnlyDictionary<ResourceType, ResourcePool> _pools;

        public IReadOnlyDictionary<ResourceType, ResourcePool> Pools => _pools;

        public ResourceState(IReadOnlyDictionary<ResourceType, ResourcePool> pools)
        {
            var copy = new Dictionary<ResourceType, ResourcePool>();
            if (pools != null)
                foreach (var entry in pools)
                {
                    ResourceRules.RequireActive(entry.Key);
                    if (entry.Value == null) throw new ArgumentException("A resource pool cannot be null.", nameof(pools));
                    ResourceRules.RequireActive(entry.Value.Type);
                    copy.Add(entry.Key, entry.Value);
                }
            _pools = new ReadOnlyDictionary<ResourceType, ResourcePool>(copy);
        }

        /// <summary>Unknown/never-initialized resource type returns 0, never throws.</summary>
        public int GetCurrent(ResourceType type)
        {
            ResourceRules.RequireActive(type);
            return _pools.TryGetValue(type, out var pool) ? pool.Current : 0;
        }

        /// <summary>Unknown/never-initialized resource type returns 0, never throws.</summary>
        public int GetMax(ResourceType type)
        {
            ResourceRules.RequireActive(type);
            return _pools.TryGetValue(type, out var pool) ? pool.Max : 0;
        }

        /// <summary>Returns a new ResourceState with the given pool replacing (or adding) its type's entry.</summary>
        public ResourceState WithPool(ResourcePool pool)
        {
            ResourceRules.RequireActive(pool.Type);
            var next = new Dictionary<ResourceType, ResourcePool>(_pools)
            {
                [pool.Type] = pool
            };
            return new ResourceState(next);
        }
    }
}
