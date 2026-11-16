using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Resources;
using MathHorde.GameLogic.Events;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 2. (spec 4.3, 5.1). Reads "now" from an injected
    /// IGameClock (rule G1 — never UnityEngine.Time) to stamp every
    /// ResourceChanged event it publishes through the injected IGameEventSink
    /// (rule G3 — this class never knows who, if anyone, is subscribed).
    /// </summary>
    public sealed class ResourceService : IResourceService
    {
        private readonly IGameEventSink _eventSink;
        private readonly IGameClock _clock;
        private readonly Dictionary<ResourceType, int> _snapshots = new Dictionary<ResourceType, int>();
        private ResourceState _state;

        public ResourceService(IGameEventSink eventSink, IGameClock clock)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _state = new ResourceState(new Dictionary<ResourceType, ResourcePool>());
        }

        public ResourceState State => _state;

        public int GetCurrent(ResourceType type) => _state.GetCurrent(type);

        public int GetMax(ResourceType type) => _state.GetMax(type);

        public void Initialize(LevelDefinition level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var pools = new Dictionary<ResourceType, ResourcePool>
            {
                [ResourceType.Ammo] = new ResourcePool(ResourceType.Ammo, level.StartingAmmo, level.MaxAmmo),
                [ResourceType.Health] = new ResourcePool(ResourceType.Health, level.StartingHealth, level.MaxHealth),
            };

            // Contract game-logic.md 2.2: the Shield pool is created ONLY when
            // MaxShield > 0. MaxShield == 0 means the level has no shield
            // mechanic at all — GetCurrent/GetMax(Shield) then fall back to 0
            // via GetPoolOrDefault, which is the correct representation (not a
            // Max = max(StartingShield, 1) workaround, which is forbidden).
            if (level.MaxShield > 0)
            {
                pools[ResourceType.Shield] = new ResourcePool(ResourceType.Shield, level.StartingShield, level.MaxShield);
            }

            _state = new ResourceState(pools);
            _snapshots.Clear();

            foreach (var pool in pools.Values)
            {
                PublishChange(pool.Type, 0, pool.Current, pool.Current, pool.Current, wasClamped: false, "LEVEL_INIT");
            }
        }

        public ResourceChange ApplyDelta(ResourceType type, int delta, string reasonCode)
        {
            var pool = GetPoolOrDefault(type);
            int previous = pool.Current;
            int rawTarget = previous + delta;
            int clampedTarget = Clamp(rawTarget, pool.Min, pool.Max);
            bool wasClamped = clampedTarget != rawTarget;
            int appliedDelta = clampedTarget - previous;

            var newPool = new ResourcePool(type, clampedTarget, pool.Max, pool.Min);
            _state = _state.WithPool(newPool);

            return PublishChange(type, previous, clampedTarget, delta, appliedDelta, wasClamped, reasonCode);
        }

        public bool TryConsume(ResourceType type, int amount, string reasonCode, out ResourceChange change)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount nem lehet negatív.");
            }

            var pool = GetPoolOrDefault(type);
            if (pool.Current < amount)
            {
                // Insufficient (including the depleted/zero case) — no partial
                // consumption, no state change, no event: the caller (e.g. the
                // weapon) is expected to react with its own WeaponDryFire, not
                // through a ResourceChanged this service would have to fabricate.
                change = default;
                return false;
            }

            change = ApplyDelta(type, -amount, reasonCode);
            return true;
        }

        public ResourceChange SetMax(ResourceType type, int newMax, string reasonCode)
        {
            if (newMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newMax), newMax, "newMax > 0 kötelező.");
            }

            var pool = GetPoolOrDefault(type);
            int previous = pool.Current;
            int clampedCurrent = Clamp(previous, pool.Min, newMax);
            bool wasClamped = clampedCurrent != previous;

            var newPool = new ResourcePool(type, clampedCurrent, newMax, pool.Min);
            _state = _state.WithPool(newPool);

            return PublishChange(type, previous, clampedCurrent, requestedDelta: 0, clampedCurrent - previous, wasClamped, reasonCode);
        }

        public int CaptureSnapshot(ResourceType type)
        {
            int current = GetCurrent(type);
            _snapshots[type] = current;
            return current;
        }

        private ResourcePool GetPoolOrDefault(ResourceType type)
        {
            if (_state.Pools.TryGetValue(type, out var pool))
            {
                return pool;
            }

            // Generic resource model (spec 4.3): a type nobody has configured
            // yet (e.g. Initialize wasn't called, or a brand-new ResourceType
            // was added without any code change here) still behaves sanely —
            // starts at 0 with an effectively unbounded Max, rather than
            // throwing. This is never hit by the Initialize → gameplay flow
            // the contract describes; it is a defensive fallback only.
            return new ResourcePool(type, current: 0, max: int.MaxValue, min: 0);
        }

        private ResourceChange PublishChange(
            ResourceType type,
            int previousValue,
            int newValue,
            int requestedDelta,
            int appliedDelta,
            bool wasClamped,
            string reasonCode)
        {
            var change = new ResourceChange(type, previousValue, newValue, requestedDelta, appliedDelta, wasClamped, reasonCode);
            _eventSink.Publish(new ResourceChanged(change, _clock.GameTime));
            return change;
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
