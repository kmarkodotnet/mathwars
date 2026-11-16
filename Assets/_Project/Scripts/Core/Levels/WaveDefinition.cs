using System;
using System.Collections.Generic;

namespace MathHorde.Core.Levels
{
    /// <summary>Contract core-domain.md 7. StartGameTime is relative to session start.</summary>
    public sealed class WaveDefinition
    {
        public int WaveIndex { get; }
        public double StartGameTime { get; }
        public double DurationSeconds { get; }
        public IReadOnlyList<WaveSpawnEntry> Spawns { get; }

        public WaveDefinition(int waveIndex, double startGameTime, double durationSeconds, IReadOnlyList<WaveSpawnEntry> spawns = null)
        {
            if (waveIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveIndex), waveIndex, "WaveIndex nem lehet negatív.");
            }

            if (startGameTime < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startGameTime), startGameTime, "StartGameTime nem lehet negatív.");
            }

            if (durationSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "DurationSeconds > 0 kötelező.");
            }

            WaveIndex = waveIndex;
            StartGameTime = startGameTime;
            DurationSeconds = durationSeconds;
            Spawns = spawns ?? Array.Empty<WaveSpawnEntry>();
        }
    }
}
