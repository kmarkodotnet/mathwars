using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;
using MathHorde.Core.Problems;
using MathHorde.GameLogic.Events;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 8. — composition root. Owns the single
    /// authoritative Tick order (ARCHITECTURE 6.1); see IGameSessionService
    /// for the ADR-017 explicit startup dependencies.
    ///
    /// Victory/defeat (spec 1.2, 1.3):
    ///   Victory = AllWavesCompleted &amp;&amp; !IsBossActive &amp;&amp; IsPlayerAlive &amp;&amp;
    ///             MathAccuracy >= LevelDefinition.RequiredMathAccuracy.
    ///   Defeat  = !IsPlayerAlive  =>  FailureReason.HealthDepleted.
    ///   Ammo running out is explicitly NOT a defeat condition (spec 1.3) —
    ///   there is no code path here that ends the session for ammo being 0;
    ///   the player can keep surviving and reach another resource gate.
    ///
    /// Real resolutions determine accuracy; immutable answer metadata is buffered during Tick.
    /// </summary>
    public sealed class GameSessionService : IGameSessionService
    {
        private const string SessionNotRunning = "SESSION_NOT_RUNNING";
        private const string SessionPaused = "SESSION_PAUSED";
        private readonly IGameClock _clock;
        private readonly IWaveDirector _waveDirector;
        private readonly ICombatService _combatService;
        private readonly IMathEventDirector _mathEventDirector;
        private readonly IResourceService _resourceService;
        private readonly IGameEventSink _eventSink;

        private readonly IGateResolutionService _resolution;
        private readonly IRewardService _rewards;
        private readonly Action<MathAnswerSnapshot, MathProblem> _recordAnswer;
        private readonly Func<IReadOnlyList<QuestionAttempt>> _getAttempts;
        private int _totalQuestions, _correctQuestions, _timedOutQuestions;
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
            IGameEventSink eventSink,
            IGateResolutionService resolution = null, IRewardService rewards = null,
            Action<MathAnswerSnapshot, MathProblem> recordAnswer = null,
            Func<IReadOnlyList<QuestionAttempt>> getAttempts = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _waveDirector = waveDirector ?? throw new ArgumentNullException(nameof(waveDirector));
            _combatService = combatService ?? throw new ArgumentNullException(nameof(combatService));
            _mathEventDirector = mathEventDirector ?? throw new ArgumentNullException(nameof(mathEventDirector));
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            if ((resolution == null) != (rewards == null)) throw new ArgumentException("Resolution and reward services must be composed together.");
            _resolution = resolution; _rewards = rewards; _recordAnswer = recordAnswer; _getAttempts = getAttempts;
        }

        public GameSessionId SessionId { get; private set; }

        public SessionResult Result { get; private set; } = SessionResult.InProgress;

        public bool IsRunning { get; private set; }
        public IReadOnlyList<WeaponDefinition> AvailableWeapons { get; private set; } = Array.Empty<WeaponDefinition>();
        public string CurrentWeaponId => _combatService.CurrentWeaponId;

        public Result<bool> SelectWeapon(string weaponId)
        {
            if (!IsRunning || _finalized || !_combatService.IsPlayerAlive) return Core.Common.Result<bool>.Fail(SessionNotRunning);
            if (_clock.IsPaused) return Core.Common.Result<bool>.Fail(SessionPaused);
            return _combatService.SelectWeapon(weaponId, _clock.GameTime);
        }

        public FailureReason FailureReason { get; private set; } = FailureReason.None;

        public void StartSession(
            LevelDefinition level,
            IReadOnlyList<WeaponDefinition> weapons,
            IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog,
            int seed)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            var catalog = WeaponCatalog.Copy(level, weapons);
            _level = level;
            AvailableWeapons = catalog;
            _seed = seed;
            SessionId = GameSessionId.New();
            _startedAt = DateTimeOffset.UtcNow;
            Result = SessionResult.InProgress;
            FailureReason = FailureReason.None;
            _finalized = false;
            _finalizedRecord = null;
            _totalQuestions = _correctQuestions = _timedOutQuestions = 0;
            IsRunning = true;

            _resourceService.Initialize(level);

            var random = new DeterministicRandomSource(seed);
            _waveDirector.Initialize(level, random);
            _combatService.Initialize(level, catalog, enemyCatalog);

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

            // Step 3: combat precedes math, independently of question state.
            _combatService.Tick(t, deltaTime);

            // Step 4: prepared math lifecycle, after combat's resource changes.
            // Resolve/reward/record below drain every command exactly once.
            _mathEventDirector.Tick(t);

            if (_resolution != null)
            {
                var pending = _mathEventDirector.PendingResolutions;
                for (int i = 0; i < pending.Count; i++)
                {
                    ActiveMathEvent item = null;
                    for (int j = 0; j < _mathEventDirector.ActiveEvents.Count; j++)
                        if (_mathEventDirector.ActiveEvents[j].EventId == pending[i].EventId) { item = _mathEventDirector.ActiveEvents[j]; break; }
                    var resolved = _resolution.Resolve(pending[i], item);
                    var application = _rewards.ApplyReward(resolved);
                    if (resolved.Outcome != GateResolutionOutcome.Duplicate)
                    {
                        _totalQuestions++;
                        if (resolved.WasCorrect) _correctQuestions++;
                        if (resolved.Answer.TimedOut) _timedOutQuestions++;
                        _recordAnswer?.Invoke(resolved.Answer, item.Problem);
                    }
                    _mathEventDirector.OnRewardProcessed(application, t);
                }
                _mathEventDirector.ClearPendingResolutions();
            }
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
            IsRunning = false;
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
                mathAccuracy: _totalQuestions == 0 ? 1f : (float)_correctQuestions / _totalQuestions,
                totalQuestions: _totalQuestions,
                correctQuestions: _correctQuestions,
                timedOutQuestions: _timedOutQuestions,
                appliedRewardEventIds: _rewards?.AppliedRewardEventIds ?? (IReadOnlyCollection<Guid>)Array.Empty<Guid>(),
                attempts: _getAttempts?.Invoke() ?? Array.Empty<QuestionAttempt>());

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
                float mathAccuracy = _totalQuestions == 0 ? 1f : (float)_correctQuestions / _totalQuestions;
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
