using System;
using System.Collections.Generic;
using MathHorde.Core.Common;

namespace MathHorde.Core.Levels
{
    /// <summary>
    /// One spawn instruction inside a WaveDefinition (contract core-domain.md 7.).
    /// Empty AllowedLanes means "any lane".
    /// </summary>
    public sealed class WaveSpawnEntry
    {
        public string EnemyId { get; }
        public int Count { get; }
        public float SpawnIntervalSeconds { get; }
        public IReadOnlyList<LaneIndex> AllowedLanes { get; }

        public WaveSpawnEntry(string enemyId, int count, float spawnIntervalSeconds, IReadOnlyList<LaneIndex> allowedLanes = null)
        {
            if (string.IsNullOrEmpty(enemyId))
            {
                throw new ArgumentException("EnemyId nem lehet üres.", nameof(enemyId));
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Count >= 1 kötelező.");
            }

            if (spawnIntervalSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnIntervalSeconds), spawnIntervalSeconds, "SpawnIntervalSeconds > 0 kötelező.");
            }

            EnemyId = enemyId;
            Count = count;
            SpawnIntervalSeconds = spawnIntervalSeconds;
            AllowedLanes = allowedLanes ?? Array.Empty<LaneIndex>();
        }
    }
}
