using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;
using MathHorde.GameLogic.Events;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 8. — composition root. Owns the single
    /// authoritative Tick order (ARCHITECTURE 6.1); see IGameSessionService
    /// for the flagged signature deviation (PlayerProgress omitted).
    ///
    /// Victory/defeat (spec 1.2, 1.3):
    ///   Victory = AllWavesCompleted &amp;&amp; !IsBossActive &amp;&amp; IsPlayerAlive &amp;&amp;
    ///             MathAccuracy >= LevelDefinition.RequiredMathAccuracy.
    ///   Defeat  = !IsPlayerAlive  =>  FailureReason.HealthDepleted.
    ///   Ammo running out is explicitly NOT a defeat condition (spec 1.3) —
    ///   there is no code path here that ends the session for ammo being 0;
    ///   the player can keep surviving and reach another resource gate.
    ///
    /// MathAccuracy is hardcoded to 1.0 (vacuously satisfied) in Phase 1:
    /// IMathEventDirector is currently the no-op placeholder (no math
    /// events exist yet, F3), so "0 questions asked, 0 wrong" is the only
    /// honest value — see IMathEventDirector.cs.
    /// </summary>
    public sealed class GameSessionService : IGameSessionService
    {
        private readonly IGameClock _clock;
        private readonly IWaveDirector _waveDirector;
        private readonly ICombatService _combatService;
        private readonly IMathEventDirector _mathEventDirector;
        private readonly IResourceService _resourceService;
        private readonly IGameEventSink _eventSink;

        private LevelDefinition _level;
        private int _seed;
        private DateTimeOffset _startedAt;
        private bool _finalized;
        private GameSessionRecord _finalizedRecord;

        public GameSessionService(
            IGameClock clock,
            IWaveDirector waveDirector,
            ICombatService combatService,
            IMathEventDirector mathEventDirector,
            IResourceService resourceService,
            IGameEventSink eventSink)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _waveDirector = waveDirector ?? throw new ArgumentNullException(nameof(waveDirector));
            _combatService = combatService ?? throw new ArgumentNullException(nameof(combatService));
            _mathEventDirector = mathEventDirector ?? throw new ArgumentNullException(nameof(mathEventDirector));
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        }

        public GameSessionId SessionId { get; private set; }

        public SessionResult Result { get; private set; } = SessionResult.InProgress;

        public bool IsRunning { get; private set; }

        public FailureReason FailureReason { get; private set; } = FailureReason.None;

        public void StartSession(
            LevelDefinition level,
            WeaponDefinition weapon,
            IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog,
            int seed)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _seed = seed;
            SessionId = GameSessionId.New();
            _startedAt = DateTimeOffset.UtcNow;
            Result = SessionResult.InProgress;
            FailureReason = FailureReason.None;
            _finalized = false;
            _finalizedRecord = null;
            IsRunning = true;

            _resourceService.Initialize(level);

            var random = new DeterministicRandomSource(seed);
            _waveDirector.Initialize(level, random);
            _combatService.Initialize(level, weapon, enemyCatalog);

            _eventSink.Publish(new SessionStarted(SessionId, level.Id, seed, _clock.GameTime));
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            // Step 1.
            _clock.Advance(deltaTime);
            double t = _clock.GameTime;

            if (_clock.IsPaused)
            {
                // "szünetben sem harc, sem határidő nem halad" — steps 2-5 skipped entirely.
                return;
            }

            // Step 2: wave scheduling, then drain spawns into combat.
            _waveDirector.Tick(t);
            var pendingSpawns = _waveDirector.PendingSpawns;
            for (int i = 0; i < pendingSpawns.Count; i++)
            {
                var cmd = pendingSpawns[i];
                int instanceId = _combatService.SpawnEnemy(cmd.EnemyId, cmd.Lane, cmd.SpawnDepth);
                _eventSink.Publish(new EnemySpawned(instanceId, cmd.EnemyId, cmd.Lane, new Vec2(cmd.Lane.Value, cmd.SpawnDepth), t));
            }

            _waveDirector.ClearPendingSpawns();

            // Step 3: combat — intentionally BEFORE the math step (spec 5.1 snapshot mechanics).
            _combatService.Tick(t, deltaTime);

            // Step 4: prepared math lifecycle, after combat's resource changes.
            // The resolution/reward/analytics composition remains F3-12 work;
            // its commands stay available on the director until explicitly drained.
            _mathEventDirector.Tick(t);

            EvaluateVictoryOrDefeat(t);
        }

        public void Pause() => _clock.Pause();

        public void Resume() => _clock.Resume();

        public GameSessionRecord Finalize(SessionResult result, FailureReason reason)
        {
            if (_finalized)
            {
                return _finalizedRecord;
            }

            _finalized = true;
            _finalizedRecord = new GameSessionRecord(
                SessionId,
                _level.Id,
                _seed,
                _startedAt,
                DateTimeOffset.UtcNow,
                initialChallengeLevel: 1,
                finalChallengeLevel: 1,
                result,
                reason,
                LossDiagnosis.None,
                mathAccuracy: 1.0f,
                totalQuestions: 0,
                correctQuestions: 0,
                timedOutQuestions: 0,
                appliedRewardEventIds: Array.Empty<Guid>(),
                attempts: Array.Empty<QuestionAttempt>());

            return _finalizedRecord;
        }

        private void EvaluateVictoryOrDefeat(double gameTime)
        {
            if (!IsRunning)
            {
                return;
            }

            if (!_combatService.IsPlayerAlive)
            {
                EndSession(SessionResult.Defeat, FailureReason.HealthDepleted, gameTime);
                return;
            }

            if (_waveDirector.AllWavesCompleted && !_waveDirector.IsBossActive)
            {
                const float mathAccuracy = 1.0f;
                if (mathAccuracy >= _level.RequiredMathAccuracy)
                {
                    EndSession(SessionResult.Victory, FailureReason.None, gameTime);
                }
            }
        }

        private void EndSession(SessionResult result, FailureReason reason, double gameTime)
        {
            Result = result;
            FailureReason = reason;
            IsRunning = false;
            _mathEventDirector.OnSessionEnded(gameTime);
            _eventSink.Publish(new SessionEnded(SessionId, result, reason, gameTime));
        }
    }
}
