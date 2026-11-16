using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 4. (spec 4.1). Schedules enemy spawns from the
    /// LevelDefinition's waves. NEVER queries or waits on any math-event
    /// state (spec 14.) — no IMathEventDirector reference anywhere on this
    /// interface or its implementation (rule G5, enforced at compile time).
    /// </summary>
    public interface IWaveDirector
    {
        void Initialize(LevelDefinition level, IRandomSource random);

        /// <summary>Wave scheduling step. gameTime comes from the caller (rule G1) — never UnityEngine.Time.</summary>
        void Tick(double gameTime);

        int CurrentWaveIndex { get; }

        int TotalWaves { get; }

        bool AllWavesCompleted { get; }

        bool IsBossActive { get; }

        /// <summary>
        /// Spawn commands raised during the last Tick. GameSessionService
        /// drains this into ICombatService.SpawnEnemy — the two services
        /// never reference each other directly (rule G2).
        /// </summary>
        IReadOnlyList<SpawnCommand> PendingSpawns { get; }

        void ClearPendingSpawns();

        void ApplyCombatModifiers(CombatModifiers modifiers);

        /// <summary>
        /// Maximum allowed silence between two consecutive spawns across the
        /// whole level (spec 4.1: "két matematikai feladat között sem állhat
        /// le tartósan a spawnolás"). Default 2 seconds
        /// (CombatBalanceSO.MaxWaveGapSeconds, contract game-logic.md 4.).
        /// WaveDirector actively enforces this at Initialize time — see
        /// WaveDirector.cs for how gaps wider than this are closed.
        /// </summary>
        float MaxWaveGapSeconds { get; }
    }

    public readonly struct SpawnCommand
    {
        public string EnemyId { get; }
        public LaneIndex Lane { get; }
        public float SpawnDepth { get; }
        public double ScheduledGameTime { get; }

        public SpawnCommand(string enemyId, LaneIndex lane, float spawnDepth, double scheduledGameTime)
        {
            EnemyId = enemyId;
            Lane = lane;
            SpawnDepth = spawnDepth;
            ScheduledGameTime = scheduledGameTime;
        }
    }
}
