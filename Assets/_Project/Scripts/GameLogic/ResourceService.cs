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
        private readonly Dictionary<ResourceType, PoolValues> _pools;
        private ResourceState _state;

        private readonly struct PoolValues
        {
            internal readonly int Current;
            internal readonly int Max;
            internal readonly int Min;
            internal PoolValues(int current, int max, int min = 0)
            { Current = current; Max = max; Min = min; }
        }

        public ResourceService(IGameEventSink eventSink, IGameClock clock)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            // Reserve every defined resource type, including pools introduced after F1.
            // Updating these value entries never allocates during combat or math rewards.
            _pools = new Dictionary<ResourceType, PoolValues>(Enum.GetValues(typeof(ResourceType)).Length);
        }

        /// <summary>
        /// Immutable snapshot for readers outside Tick. Materialize only on demand after
        /// a mutation; Tick consumers use GetCurrent/GetMax and ResourceChanged instead.
        /// Previously returned snapshots never change when the live pools change.
        /// </summary>
        public ResourceState State
        {
            get
            {
                if (_state == null)
                {
                    var pools = new Dictionary<ResourceType, ResourcePool>(_pools.Count);
                    foreach (var entry in _pools)
                        pools.Add(entry.Key, new ResourcePool(entry.Key, entry.Value.Current,
                            entry.Value.Max, entry.Value.Min));
                    _state = new ResourceState(pools);
                }
                return _state;
            }
        }

        public int GetCurrent(ResourceType type) => _pools.TryGetValue(type, out var pool) ? pool.Current : 0;

        public int GetMax(ResourceType type) => _pools.TryGetValue(type, out var pool) ? pool.Max : 0;

        public void Initialize(LevelDefinition level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            _pools.Clear();
            _pools.Add(ResourceType.Ammo, new PoolValues(level.StartingAmmo, level.MaxAmmo));
            _pools.Add(ResourceType.Health, new PoolValues(level.StartingHealth, level.MaxHealth));

            // Contract game-logic.md 2.2: the Shield pool is created ONLY when
            // MaxShield > 0. MaxShield == 0 means the level has no shield
            // mechanic at all — GetCurrent/GetMax(Shield) then fall back to 0
            // via GetPoolOrDefault, which is the correct representation (not a
            // Max = max(StartingShield, 1) workaround, which is forbidden).
            if (level.MaxShield > 0)
            {
                _pools.Add(ResourceType.Shield, new PoolValues(level.StartingShield, level.MaxShield));
            }

            _state = null;

            foreach (var entry in _pools)
            {
                var pool = entry.Value;
                PublishChange(entry.Key, 0, pool.Current, pool.Current, pool.Current, wasClamped: false, "LEVEL_INIT");
            }
        }

        public ResourceChange ApplyDelta(ResourceType type, int delta, string reasonCode)
        {
            var pool = GetPoolOrDefault(type);
            int previous = pool.Current;
            long rawTarget = (long)previous + delta;
            int clampedTarget = Clamp(rawTarget, pool.Min, pool.Max);
            bool wasClamped = clampedTarget != rawTarget;
            int appliedDelta = clampedTarget - previous;

            _pools[type] = new PoolValues(clampedTarget, pool.Max, pool.Min);
            _state = null;

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

            _pools[type] = new PoolValues(clampedCurrent, newMax, pool.Min);
            _state = null;

            return PublishChange(type, previous, clampedCurrent, requestedDelta: 0, clampedCurrent - previous, wasClamped, reasonCode);
        }

        public int CaptureSnapshot(ResourceType type)
        {
            // The returned value is the snapshot. Retaining another dictionary
            // copy serves no reader and would allocate on the first math Tick.
            return GetCurrent(type);
        }

        private PoolValues GetPoolOrDefault(ResourceType type)
        {
            if (_pools.TryGetValue(type, out var pool))
            {
                return pool;
            }

            // Generic resource model (spec 4.3): a type nobody has configured
            // yet (e.g. Initialize wasn't called, or a brand-new ResourceType
            // was added without any code change here) still behaves sanely —
            // starts at 0 with an effectively unbounded Max, rather than
            // throwing. This is never hit by the Initialize → gameplay flow
            // the contract describes; it is a defensive fallback only.
            return new PoolValues(current: 0, max: int.MaxValue);
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

        private static int Clamp(long value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : (int)value;
        }
    }
}
