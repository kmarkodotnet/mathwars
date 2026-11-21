using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 3. (spec 4.1, 4.2) — enemy movement, targeting,
    /// automatic unlimited fire, projectile/hitscan resolution,
    /// contact damage and death. Deliberately has NO reference to
    /// IMathEventDirector anywhere in its surface (constructor, Initialize,
    /// Tick) — the independence contract (game-logic.md 5.) is enforced at
    /// compile time by this absence, not by a runtime check.
    /// </summary>
    public interface ICombatService
    {
        void Initialize(
            LevelDefinition level,
            WeaponDefinition weapon,
            IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog);

        /// <summary>
        /// One simulation step: enemy movement, targeting, firing, hit
        /// resolution, contact damage, death. Never consults any math-event
        /// state (spec 1., 23./4.) — gameTime comes from the caller
        /// (IGameClock via GameSessionService), never from UnityEngine.Time
        /// (rule G1).
        /// </summary>
        void Tick(double gameTime, float deltaTime);

        /// <summary>
        /// Inserts one enemy instance. The WaveDirector is the normal
        /// caller, via GameSessionService draining WaveDirector.PendingSpawns
        /// — the two services never reference each other directly (rule G2).
        /// Returns the newly assigned InstanceId (monotonic within the
        /// session; never reused, even after the enemy dies — see rule 7).
        /// </summary>
        int SpawnEnemy(string enemyId, LaneIndex lane, float spawnDepth);

        void MovePlayerToLane(LaneIndex lane);

        LaneIndex PlayerLane { get; }

        bool IsPlayerAlive { get; }

        /// <summary>
        /// Read-only view for Presentation. Backed by a pre-allocated buffer
        /// that is cleared and refilled once per Tick — stable (safe to
        /// iterate) between Tick calls, never reallocated in the hot path.
        /// </summary>
        IReadOnlyList<EnemySnapshot> ActiveEnemies { get; }

        /// <summary>Combat difficulty multipliers (spec 7.3). Applied to newly spawned enemies and live fire/contact calculations.</summary>
        void ApplyCombatModifiers(CombatModifiers modifiers);
    }

    /// <summary>
    /// Live per-instance combat state for one enemy (game-logic.md 3.,
    /// ADR-009). Deliberately separate from EnemyDefinition: CurrentHealth /
    /// Position / Lane / IsAlive live ONLY here, never on the shared,
    /// poolable EnemyDefinition — see EnemyDefinition.cs for the full
    /// rationale.
    /// </summary>
    public readonly struct EnemySnapshot
    {
        public int InstanceId { get; }
        public string EnemyId { get; }
        public EnemyType Type { get; }
        public LaneIndex Lane { get; }
        public Vec2 Position { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
        public bool IsAlive { get; }

        public EnemySnapshot(
            int instanceId,
            string enemyId,
            EnemyType type,
            LaneIndex lane,
            Vec2 position,
            int currentHealth,
            int maxHealth,
            bool isAlive)
        {
            InstanceId = instanceId;
            EnemyId = enemyId;
            Type = type;
            Lane = lane;
            Position = position;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            IsAlive = isAlive;
        }
    }

    /// <summary>Combat difficulty multipliers (game-logic.md 3., spec 7.3). All default to 1.0 (Neutral).</summary>
    public readonly struct CombatModifiers
    {
        public float EnemySpeedMultiplier { get; }
        public float EnemyHealthMultiplier { get; }
        public float EnemyDamageMultiplier { get; }
        public float SpawnRateMultiplier { get; }

        public CombatModifiers(
            float enemySpeedMultiplier,
            float enemyHealthMultiplier,
            float enemyDamageMultiplier,
            float spawnRateMultiplier)
        {
            EnemySpeedMultiplier = enemySpeedMultiplier;
            EnemyHealthMultiplier = enemyHealthMultiplier;
            EnemyDamageMultiplier = enemyDamageMultiplier;
            SpawnRateMultiplier = spawnRateMultiplier;
        }

        public static CombatModifiers Neutral { get; } = new CombatModifiers(1f, 1f, 1f, 1f);
    }
}
