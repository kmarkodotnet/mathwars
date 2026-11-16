using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 4. (spec 4.1). Flattens the LevelDefinition's
    /// WaveDefinition/WaveSpawnEntry authoring data into a single,
    /// time-sorted spawn schedule at Initialize time, then simply dispatches
    /// due entries into PendingSpawns as gameTime advances. NEVER references
    /// IMathEventDirector or ICombatService — GameSessionService is the only
    /// glue between this and combat (rule G2, G5).
    ///
    /// MaxWaveGapSeconds enforcement (spec 4.1 "két matematikai feladat
    /// között sem állhat le tartósan a spawnolás"): rather than merely
    /// hoping the authored level never has a silent gap, Initialize actively
    /// closes any gap wider than MaxWaveGapSeconds by inserting synthetic
    /// "echo" spawns — same EnemyId/Lane as the spawn that ends the gap —
    /// spaced exactly MaxWaveGapSeconds apart. This makes the invariant hold
    /// for ANY level data, not just carefully-authored ones.
    /// </summary>
    public sealed class WaveDirector : IWaveDirector
    {
        private const float DefaultSpawnDepth = 12f;

        public float MaxWaveGapSeconds { get; }

        private readonly List<SpawnCommand> _schedule = new List<SpawnCommand>();
        private readonly List<SpawnCommand> _pendingSpawns = new List<SpawnCommand>();
        private readonly List<(double Start, double End)> _waveWindows = new List<(double Start, double End)>();

        private int _dispatchCursor;
        private int _totalWaves;
        private int _laneCount = 1;
        private string _bossEnemyId;
        private double _lastGameTime;
        private CombatModifiers _modifiers = CombatModifiers.Neutral;

        public WaveDirector(float maxWaveGapSeconds = 2f)
        {
            MaxWaveGapSeconds = maxWaveGapSeconds > 0f ? maxWaveGapSeconds : 2f;
        }

        public int CurrentWaveIndex
        {
            get
            {
                int index = 0;
                for (int i = 0; i < _waveWindows.Count; i++)
                {
                    if (_waveWindows[i].Start <= _lastGameTime)
                    {
                        index = i;
                    }
                }

                return index;
            }
        }

        public int TotalWaves => _totalWaves;

        public bool AllWavesCompleted
        {
            get
            {
                if (_waveWindows.Count == 0)
                {
                    return true;
                }

                var last = _waveWindows[_waveWindows.Count - 1];
                return _lastGameTime > last.End && _dispatchCursor >= _schedule.Count;
            }
        }

        public bool IsBossActive
        {
            get
            {
                if (string.IsNullOrEmpty(_bossEnemyId) || _waveWindows.Count == 0)
                {
                    return false;
                }

                var last = _waveWindows[_waveWindows.Count - 1];
                return _lastGameTime >= last.Start && _lastGameTime <= last.End;
            }
        }

        public IReadOnlyList<SpawnCommand> PendingSpawns => _pendingSpawns;

        public void Initialize(LevelDefinition level, IRandomSource random)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            _bossEnemyId = level.BossEnemyId;
            _totalWaves = level.Waves.Count;
            _laneCount = level.LaneCount;

            _waveWindows.Clear();
            _schedule.Clear();
            _pendingSpawns.Clear();
            _dispatchCursor = 0;
            _lastGameTime = 0;

            var laneRandom = random.Derive("waves");

            foreach (var wave in level.Waves)
            {
                _waveWindows.Add((wave.StartGameTime, wave.StartGameTime + wave.DurationSeconds));

                foreach (var spawnEntry in wave.Spawns)
                {
                    for (int i = 0; i < spawnEntry.Count; i++)
                    {
                        double scheduledTime = wave.StartGameTime + i * spawnEntry.SpawnIntervalSeconds;
                        LaneIndex lane = PickLane(spawnEntry.AllowedLanes, laneRandom);
                        _schedule.Add(new SpawnCommand(spawnEntry.EnemyId, lane, DefaultSpawnDepth, scheduledTime));
                    }
                }
            }

            _schedule.Sort((a, b) => a.ScheduledGameTime.CompareTo(b.ScheduledGameTime));

            EnforceMaxGap();
        }

        public void Tick(double gameTime)
        {
            _lastGameTime = gameTime;

            while (_dispatchCursor < _schedule.Count && _schedule[_dispatchCursor].ScheduledGameTime <= gameTime)
            {
                _pendingSpawns.Add(_schedule[_dispatchCursor]);
                _dispatchCursor++;
            }
        }

        public void ClearPendingSpawns()
        {
            _pendingSpawns.Clear();
        }

        public void ApplyCombatModifiers(CombatModifiers modifiers)
        {
            // SpawnRateMultiplier (spec 7.3) is accepted here for forward
            // compatibility with the adaptive-difficulty phase (F4) but does
            // not yet rescale an already-flattened schedule — no in-session
            // consumer exists for it in Phase 1.
            _modifiers = modifiers;
        }

        private LaneIndex PickLane(IReadOnlyList<LaneIndex> allowedLanes, IRandomSource random)
        {
            if (allowedLanes != null && allowedLanes.Count > 0)
            {
                int index = random.Next(0, allowedLanes.Count);
                return allowedLanes[index];
            }

            int laneValue = random.Next(0, Math.Max(1, _laneCount));
            return new LaneIndex(laneValue);
        }

        private void EnforceMaxGap()
        {
            if (_schedule.Count == 0)
            {
                return;
            }

            var filled = new List<SpawnCommand>(_schedule.Count);
            filled.Add(_schedule[0]);

            for (int i = 1; i < _schedule.Count; i++)
            {
                var previous = filled[filled.Count - 1];
                var next = _schedule[i];

                double cursorTime = previous.ScheduledGameTime;
                while (next.ScheduledGameTime - cursorTime > MaxWaveGapSeconds)
                {
                    cursorTime += MaxWaveGapSeconds;
                    filled.Add(new SpawnCommand(next.EnemyId, next.Lane, next.SpawnDepth, cursorTime));
                }

                filled.Add(next);
            }

            _schedule.Clear();
            _schedule.AddRange(filled);
        }
    }
}
