using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Resources;
using MathHorde.GameLogic.Events;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 3. (spec 4.1, 4.2). Pure simulation: enemy
    /// movement, targeting, automatic unlimited fire, health/shield changes via
    /// IResourceService, projectile/hitscan resolution, contact damage
    /// (shield-then-health, rule 5) and death. NEVER reads any math-event
    /// state (spec 1., 23./4.) — there is no IMathEventDirector reference
    /// anywhere in this class, enforced at compile time.
    ///
    /// Object pooling (unity-csharp SKILL.md 18.): enemy and projectile
    /// instances live in pre-allocated slot lists with a free-index stack;
    /// after the initial warm-up (growing to the session's peak concurrent
    /// count), steady-state Spawn/Kill/Tick never calls `new` on a slot —
    /// only field resets on a reused slot object.
    /// </summary>
    public sealed class CombatService : ICombatService
    {
        private const float MinimumFireIntervalSeconds = 0.01f;

        private readonly IResourceService _resourceService;
        private readonly IGameEventSink _eventSink;

        private WeaponDefinition _weapon;
        private Dictionary<string, WeaponDefinition> _weapons;
        private const string WeaponNotAvailable = "WEAPON_NOT_AVAILABLE";
        private IReadOnlyDictionary<string, EnemyDefinition> _enemyCatalog;
        private CombatModifiers _modifiers = CombatModifiers.Neutral;
        private int _laneCount = 1;

        private LaneIndex _playerLane = new LaneIndex(0);
        private double _lastGameTime;

        private double _nextShotAt;
        private int _nextInstanceId = 1;

        private sealed class EnemySlot
        {
            public bool InUse;
            public int InstanceId;
            public string EnemyId;
            public EnemyDefinition Definition;
            public LaneIndex Lane;
            public float Depth;
            public int CurrentHealth;
            public int MaxHealth;
            public float AttackCooldownRemaining;
        }

        private sealed class ProjectileSlot
        {
            public bool InUse;
            public int TargetInstanceId;
            public float RemainingDistance;
            public float Speed;
            public int Damage;
        }

        private readonly List<EnemySlot> _slots = new List<EnemySlot>();
        private readonly Stack<int> _freeSlotIndices = new Stack<int>();

        private readonly List<ProjectileSlot> _projectileSlots = new List<ProjectileSlot>();
        private readonly Stack<int> _freeProjectileIndices = new Stack<int>();

        // Pre-allocated, refilled once per Tick (not on every getter access) —
        // "stable buffer between Tick calls" per the contract's ActiveEnemies doc.
        private readonly List<EnemySnapshot> _snapshotBuffer = new List<EnemySnapshot>();

        public CombatService(IResourceService resourceService, IGameEventSink eventSink)
        {
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        }

        public LaneIndex PlayerLane => _playerLane;
        public string CurrentWeaponId => _weapon?.WeaponId;

        public bool IsPlayerAlive { get; private set; } = true;

        public IReadOnlyList<EnemySnapshot> ActiveEnemies => _snapshotBuffer;

        public void Initialize(
            LevelDefinition level,
            IReadOnlyList<WeaponDefinition> weapons,
            IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            var catalog = WeaponCatalog.Copy(level, weapons);
            _weapons = new Dictionary<string, WeaponDefinition>(catalog.Count, StringComparer.Ordinal);
            for (int i = 0; i < catalog.Count; i++) _weapons.Add(catalog[i].WeaponId, catalog[i]);
            _weapon = _weapons[level.WeaponId];
            _enemyCatalog = enemyCatalog ?? throw new ArgumentNullException(nameof(enemyCatalog));
            _laneCount = level.LaneCount;
            _modifiers = CombatModifiers.Neutral;

            _playerLane = new LaneIndex(0);
            IsPlayerAlive = true;
            _lastGameTime = 0;
            _nextShotAt = double.NegativeInfinity;
            _nextInstanceId = 1;

            foreach (var slot in _slots)
            {
                slot.InUse = false;
            }

            _freeSlotIndices.Clear();
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                _freeSlotIndices.Push(i);
            }

            foreach (var projectile in _projectileSlots)
            {
                projectile.InUse = false;
            }

            _freeProjectileIndices.Clear();
            for (int i = _projectileSlots.Count - 1; i >= 0; i--)
            {
                _freeProjectileIndices.Push(i);
            }

            _snapshotBuffer.Clear();
        }

        public void ApplyCombatModifiers(CombatModifiers modifiers)
        {
            _modifiers = modifiers;
        }

        public Result<bool> SelectWeapon(string weaponId, double gameTime)
        {
            if (string.IsNullOrEmpty(weaponId) || _weapons == null || !_weapons.TryGetValue(weaponId, out var weapon))
                return Result<bool>.Fail(WeaponNotAvailable);
            if (_weapon.WeaponId == weaponId) return Result<bool>.Ok(false);
            string previous = _weapon.WeaponId;
            _weapon = weapon;
            _nextShotAt = Math.Max(_nextShotAt, gameTime + 1.0 / weapon.FireRate);
            _eventSink.Publish(new WeaponChanged(previous, weaponId, gameTime));
            return Result<bool>.Ok(true);
        }

        public int SpawnEnemy(string enemyId, LaneIndex lane, float spawnDepth)
        {
            if (!_enemyCatalog.TryGetValue(enemyId, out var definition))
            {
                throw new ArgumentException($"Ismeretlen enemyId: '{enemyId}'.", nameof(enemyId));
            }

            if (lane.Value >= _laneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lane), lane.Value, "Lane nagyobb, mint a pálya LaneCount-ja.");
            }

            int slotIndex = AcquireEnemySlot();
            var slot = _slots[slotIndex];

            int scaledMaxHealth = Math.Max(1, (int)Math.Round(definition.MaxHealth * _modifiers.EnemyHealthMultiplier, MidpointRounding.AwayFromZero));

            slot.InUse = true;
            slot.InstanceId = _nextInstanceId++;
            slot.EnemyId = enemyId;
            slot.Definition = definition;
            slot.Lane = lane;
            slot.Depth = spawnDepth;
            slot.CurrentHealth = scaledMaxHealth;
            slot.MaxHealth = scaledMaxHealth;
            slot.AttackCooldownRemaining = 0f;

            return slot.InstanceId;
        }

        public void MovePlayerToLane(LaneIndex lane)
        {
            if (lane.Value >= _laneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lane), lane.Value, "Lane nagyobb, mint a pálya LaneCount-ja.");
            }

            if (lane.Value == _playerLane.Value)
            {
                return;
            }

            var previous = _playerLane;
            _playerLane = lane;
            _eventSink.Publish(new PlayerLaneChanged(previous, lane, _lastGameTime));
        }

        public void Tick(double gameTime, float deltaTime)
        {
            _lastGameTime = gameTime;

            if (!IsPlayerAlive)
            {
                RefreshSnapshotBuffer();
                return;
            }

            MoveAndResolveContacts(gameTime, deltaTime);
            TickWeapon(gameTime);
            TickProjectiles(gameTime, deltaTime);
            RefreshSnapshotBuffer();
        }

        private void MoveAndResolveContacts(double gameTime, float deltaTime)
        {
            float effectiveSpeedMultiplier = _modifiers.EnemySpeedMultiplier;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.InUse)
                {
                    continue;
                }

                if (slot.Depth > 0f)
                {
                    slot.Depth -= slot.Definition.MovementSpeed * effectiveSpeedMultiplier * deltaTime;
                    if (slot.Depth < 0f)
                    {
                        slot.Depth = 0f;
                    }
                }

                if (slot.Depth <= 0f)
                {
                    if (slot.AttackCooldownRemaining > 0f)
                    {
                        slot.AttackCooldownRemaining -= deltaTime;
                        continue;
                    }

                    int damage = Math.Max(0, (int)Math.Round(slot.Definition.ContactDamage * _modifiers.EnemyDamageMultiplier, MidpointRounding.AwayFromZero));
                    slot.AttackCooldownRemaining = slot.Definition.AttackCooldownSeconds;

                    _eventSink.Publish(new EnemyReachedPlayer(slot.InstanceId, damage, gameTime));
                    ApplyContactDamage(damage, gameTime);

                    if (!IsPlayerAlive)
                    {
                        return;
                    }
                }
            }
        }

        private void ApplyContactDamage(int damage, double gameTime)
        {
            int remaining = damage;

            int shieldCurrent = _resourceService.GetCurrent(ResourceType.Shield);
            if (shieldCurrent > 0 && remaining > 0)
            {
                int absorbed = Math.Min(shieldCurrent, remaining);
                _resourceService.ApplyDelta(ResourceType.Shield, -absorbed, "ENEMY_CONTACT");
                remaining -= absorbed;
            }

            if (remaining > 0)
            {
                _resourceService.ApplyDelta(ResourceType.Health, -remaining, "ENEMY_CONTACT");
            }

            int remainingHealth = _resourceService.GetCurrent(ResourceType.Health);
            _eventSink.Publish(new PlayerDamaged(damage, remainingHealth, gameTime));

            if (remainingHealth <= 0)
            {
                IsPlayerAlive = false;
            }
        }

        private void TickWeapon(double gameTime)
        {
            if (gameTime + 1e-8 < _nextShotAt)
            {
                return;
            }

            var target = FindTarget();
            if (target == null)
            {
                // No target: stay "ready" so firing resumes the instant one
                // appears, instead of waiting out an arbitrary interval.
                return;
            }

            _eventSink.Publish(new WeaponFired(target.InstanceId, _weapon.WeaponId, _weapon.Damage, gameTime));
            if (_weapon.ProjectileSpeed <= 0f)
                ResolveHit(target, _weapon.Damage, gameTime);
            else
                SpawnProjectile(target.InstanceId, _weapon.Damage, _weapon.ProjectileSpeed, Math.Max(0f, target.Depth));

            float interval = _weapon.FireRate > 0f ? 1f / _weapon.FireRate : MinimumFireIntervalSeconds;
            _nextShotAt = gameTime + Math.Max(interval, MinimumFireIntervalSeconds);
        }

        private EnemySlot FindTarget()
        {
            switch (_weapon.TargetingMode)
            {
                case TargetingMode.OwnLaneNearestOnly:
                    return FindNearestInLanes(_playerLane.Value, _playerLane.Value);

                case TargetingMode.OwnLaneThenAdjacent:
                    var ownLane = FindNearestInLanes(_playerLane.Value, _playerLane.Value);
                    if (ownLane != null)
                    {
                        return ownLane;
                    }

                    int lowAdjacent = _playerLane.Value - 1;
                    int highAdjacent = _playerLane.Value + 1;
                    EnemySlot best = null;
                    if (lowAdjacent >= 0)
                    {
                        best = FindNearestInLanes(lowAdjacent, lowAdjacent);
                    }

                    var high = highAdjacent < _laneCount ? FindNearestInLanes(highAdjacent, highAdjacent) : null;
                    if (high != null && (best == null || high.Depth < best.Depth))
                    {
                        best = high;
                    }

                    return best;

                case TargetingMode.GlobalNearest:
                    return FindNearestInLanes(0, _laneCount - 1);

                default:
                    return FindNearestInLanes(_playerLane.Value, _playerLane.Value);
            }
        }

        private EnemySlot FindNearestInLanes(int minLane, int maxLane)
        {
            EnemySlot nearest = null;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.InUse)
                {
                    continue;
                }

                if (slot.Lane.Value < minLane || slot.Lane.Value > maxLane || slot.Depth > _weapon.Range)
                {
                    continue;
                }

                if (nearest == null || slot.Depth < nearest.Depth)
                {
                    nearest = slot;
                }
            }

            return nearest;
        }

        private void ResolveHit(EnemySlot slot, int damage, double gameTime)
        {
            if (!slot.InUse)
            {
                // Target died between the shot being fired and resolved
                // (e.g. a projectile mid-flight) — no double credit.
                return;
            }

            slot.CurrentHealth -= damage;
            if (slot.CurrentHealth < 0)
            {
                slot.CurrentHealth = 0;
            }

            _eventSink.Publish(new EnemyDamaged(slot.InstanceId, damage, slot.CurrentHealth, gameTime));

            if (slot.CurrentHealth <= 0)
            {
                _eventSink.Publish(new EnemyKilled(slot.InstanceId, slot.EnemyId, gameTime));
                FreeEnemySlot(slot);
            }
        }

        private void TickProjectiles(double gameTime, float deltaTime)
        {
            for (int i = 0; i < _projectileSlots.Count; i++)
            {
                var projectile = _projectileSlots[i];
                if (!projectile.InUse)
                {
                    continue;
                }

                projectile.RemainingDistance -= projectile.Speed * deltaTime;
                if (projectile.RemainingDistance > 0f)
                {
                    continue;
                }

                var targetSlot = FindSlotByInstanceId(projectile.TargetInstanceId);
                if (targetSlot != null)
                {
                    ResolveHit(targetSlot, projectile.Damage, gameTime);
                }

                FreeProjectileSlot(i);
            }
        }

        private EnemySlot FindSlotByInstanceId(int instanceId)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.InUse && slot.InstanceId == instanceId)
                {
                    return slot;
                }
            }

            return null;
        }

        private void SpawnProjectile(int targetInstanceId, int damage, float speed, float distance)
        {
            int index = AcquireProjectileSlot();
            var projectile = _projectileSlots[index];
            projectile.InUse = true;
            projectile.TargetInstanceId = targetInstanceId;
            projectile.Damage = damage;
            projectile.Speed = speed;
            projectile.RemainingDistance = distance;
        }

        private int AcquireEnemySlot()
        {
            if (_freeSlotIndices.Count > 0)
            {
                return _freeSlotIndices.Pop();
            }

            _slots.Add(new EnemySlot());
            return _slots.Count - 1;
        }

        private void FreeEnemySlot(EnemySlot slot)
        {
            slot.InUse = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (ReferenceEquals(_slots[i], slot))
                {
                    _freeSlotIndices.Push(i);
                    return;
                }
            }
        }

        private int AcquireProjectileSlot()
        {
            if (_freeProjectileIndices.Count > 0)
            {
                return _freeProjectileIndices.Pop();
            }

            _projectileSlots.Add(new ProjectileSlot());
            return _projectileSlots.Count - 1;
        }

        private void FreeProjectileSlot(int index)
        {
            _projectileSlots[index].InUse = false;
            _freeProjectileIndices.Push(index);
        }

        private void RefreshSnapshotBuffer()
        {
            _snapshotBuffer.Clear();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.InUse)
                {
                    continue;
                }

                _snapshotBuffer.Add(new EnemySnapshot(
                    slot.InstanceId,
                    slot.EnemyId,
                    slot.Definition.Type,
                    slot.Lane,
                    new Vec2(slot.Lane.Value, slot.Depth),
                    slot.CurrentHealth,
                    slot.MaxHealth,
                    isAlive: true));
            }
        }
    }
}
