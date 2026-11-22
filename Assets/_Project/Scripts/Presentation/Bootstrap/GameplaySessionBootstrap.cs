// GameplaySessionBootstrap.cs
// F1-17 — the gameplay scene's composition root (math-horde.md 19./1.,
// kritérium A, G, részben K). This is the "egy hely, ahol a szolgáltatások
// példányosodnak és összekötődnek" the task brief calls for: every
// GameLogic service, every Infrastructure adapter and every Presentation
// presenter this scene needs is constructed and wired here, exactly once,
// at scene start.
//
// This class is a thin MonoBehaviour shell in the unity-csharp skill sense
// — every line is either "new X(...)" composition or an Initialize/event
// forward, never a gameplay DECISION (every decision already lives in the
// GameLogic/Core types it wires together, or in the two small helper
// classes this task also introduces: GameLogic.AppFlow.
// GameSessionAppFlowBinder and Presentation.AppFlow.SessionOutcomeBridge).
//
// PHASE 1 SCOPE NOTE (read before extending): there is no MainMenu/
// WorldSelection scene or UI yet (those are later-phase features), so this
// bootstrap drives IAppStateMachine straight from Boot to Gameplay with a
// synthetic trigger sequence instead of real menu input. This is
// deliberate, not a shortcut around the state machine: every trigger fired
// here is a REAL edge from the ARCHITECTURE.md 4. table (ConfigLoaded,
// ProfileLoaded, PlayPressed, LevelSelected) — a later phase's MainMenu/
// WorldSelection screens replace this synthetic sequence with the same
// triggers fired from real button taps, without touching this class's
// Gameplay-state wiring at all.
//
// Also Phase-1-scoped: the session seed is a fixed serialized field (no
// level-select flow exists yet to source one from), and the profile
// snapshot the AutosaveScheduler persists is loaded once and otherwise
// unmodified in this phase (PlayerProgress/challenge-level progression is
// F5 scope, per the IGameSessionService header note) — the autosave WIRING
// (periodic tick + the AppFocusLost/level-end key events) is nonetheless
// fully real and exercised, which is this task's actual brief.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MathHorde.Core.Common;
using MathHorde.Analytics;
using MathHorde.Presentation.Gates;
using MathHorde.Core.Levels;
using MathHorde.GameLogic;
using MathHorde.GameLogic.AppFlow;
using MathHorde.Infrastructure;
using MathHorde.Infrastructure.Common;
using MathHorde.Infrastructure.Configuration;
using MathHorde.Infrastructure.Input;
using MathHorde.Infrastructure.Persistence;
using MathHorde.Infrastructure.SceneFlow;
using MathHorde.Presentation.Arena;
using MathHorde.Presentation.AppFlow;
using MathHorde.Presentation.Enemies;
using MathHorde.Presentation.Events;
using MathHorde.Presentation.Hud;
using MathHorde.Presentation.Player;
using MathHorde.Presentation.Result;

namespace MathHorde.Presentation.Bootstrap
{
    public sealed class GameplaySessionBootstrap : MonoBehaviour
    {
        [Header("Konfiguráció (ScriptableObject -> Core DTO, GameConfigurationRepository)")]
        [SerializeField] private EnemyDefinitionSO[] _enemyAssets = Array.Empty<EnemyDefinitionSO>();
        [SerializeField] private WeaponConfigSO[] _weaponAssets = Array.Empty<WeaponConfigSO>();
        [SerializeField] private LevelDefinitionSO _levelAsset;
        [SerializeField] private CombatBalanceSO _combatBalance;

        [Header("Pálya (a LaneAnchor-okat innen olvassuk ki, nem ismételjük meg a sávszámot)")]
        [SerializeField] private Transform _arenaRoot;

        [Header("Presentation — ugyanebben a scene-ben élő, már bekötésre váró komponensek")]
        [SerializeField] private GameplayHUD _hud;
        [SerializeField] private LevelResultScreen _resultScreen;
        [SerializeField] private EnemyPresenter _enemyPresenter;
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private PlayerView _playerView;
        [SerializeField] private InputAdapter _inputAdapter;
        [SerializeField] private SceneFlowManagerBehaviour _sceneFlow;

        [Header("Session")]
        [Tooltip("Fix, determinisztikus seed (ADR-006) — Fázis 1-ben nincs világ-/pályaválasztó, ami egyet adna.")]
        [SerializeField] private int _seed = 20261117;

        [SerializeField] private MathDifficultyTableSO _mathDifficulty;
        [SerializeField] private TimePressureTableSO _timePressure;
        [SerializeField] private RewardTableSO _rewardTable;
        [SerializeField] private MathEventConfigSO _mathConfig;
        [SerializeField] private GatePresenter _gatePresenter;
        [SerializeField] private MathQuestionPresenter _questionPresenter;
        [SerializeField] private MathFeedbackPresenter _feedbackPresenter;
        [SerializeField] private LanePresenter _lanePresenter;
        // Empty uses the platform's normal offline save directory. Composition
        // may bind an isolated directory for replay/QA sessions before Start.
        [SerializeField] private string _localDataDirectory;
        private SessionAttemptRecorder _attemptRecorder;
        private bool _statisticsWritten;
        private MathHorde.Core.Progress.GameSessionRecord _pendingStatistics;
        private bool _profileSaveWarned, _statisticsSaveWarned;
        public IMathEventDirector MathDirector => _mathEventDirector;
        public IGameClock Clock => _clock;
        public IReadOnlyList<MathHorde.Core.Progress.QuestionAttempt> Attempts => _attemptRecorder?.Attempts;
        private IGameSessionService _session;
        private IMathEventDirector _mathEventDirector;
        private IGameClock _clock;
        private bool _mathPreparedAtStartup;
        private string _pendingMathPreparationError;
        public string LastMathPreparationError { get; private set; }
        private AutosaveScheduler _autosave;
        private GameSessionAppFlowBinder _appFlowBinder;
        private SessionOutcomeBridge _outcomeBridge;
        private GameEventBus _eventBus;
        private ILocalSaveRepository _saveRepository;
        private LocalProfileSnapshot _profileSnapshot;

        /// <summary>Exposed read-only for PlayMode integration tests (criterion A) — never written to from outside.</summary>
        public IGameEventSource Events => _eventBus;

        public IResourceService Resources { get; private set; }

        public ICombatService Combat { get; private set; }

        public IGameSessionService Session => _session;

        public IAppStateMachine AppStateMachine { get; private set; }

        private void Start()
        {
            try { Compose(); }
            catch (Exception error)
            {
                _mathPreparedAtStartup = false;
                LastMathPreparationError = error.Message;
                Debug.LogError(error.Message, this);
            }
        }

        private void Compose()
        {
            _eventBus = new GameEventBus();
            _eventBus.Subscribe<MathHorde.GameLogic.Events.MathEventPreparationFailed>(HandleMathPreparationFailed);
            var localization = new ResourcesLocalizationService();
            var clock = new UnityGameClock();
            _clock = clock;

            var configRepository = new GameConfigurationRepository(
                _enemyAssets, _weaponAssets, new[] { _levelAsset }, _combatBalance, mathDifficultyTable: _mathDifficulty);

            LevelDefinition level = configRepository.GetLevel(new MathHorde.Core.Progress.LevelId(_levelAsset.Id));
            var weapons = new WeaponDefinition[level.AvailableWeaponIds.Count];
            for (int i = 0; i < weapons.Length; i++) weapons[i] = configRepository.GetWeapon(level.AvailableWeaponIds[i]);
            IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog = configRepository.GetAllEnemies();

            Resources = new ResourceService(_eventBus, clock);
            var waveDirector = new WaveDirector();
            var combatService = new CombatService(Resources, _eventBus);
            Combat = combatService;
            var settings = MathEventConfigurationAdapter.CreateDirectorSettings(_mathConfig, _levelAsset,
                _mathDifficulty, _timePressure, _rewardTable, 1);
            var mathEventDirector = MathEventConfigurationAdapter.CreateDirector(Resources, _eventBus, settings,
                level, configRepository.GetDifficultyForLevel(1), _seed);
            _attemptRecorder = new SessionAttemptRecorder(level.MathEvents.Count);
            _mathEventDirector = mathEventDirector;

            _session = new GameSessionService(clock, waveDirector, combatService, mathEventDirector, Resources, _eventBus,
                new GateResolutionService(_eventBus, level.MathEvents.Count),
                new RewardService(Resources, _eventBus, clock, level.MathEvents.Count),
                _attemptRecorder.Record, () => _attemptRecorder.Attempts);

            AppStateMachine = new AppStateMachine();

            _saveRepository = new LocalSaveRepository(string.IsNullOrEmpty(_localDataDirectory) ? null : _localDataDirectory);
            _profileSnapshot = _saveRepository.Load();
            _autosave = new AutosaveScheduler(SaveProfileSnapshot);

            _appFlowBinder = new GameSessionAppFlowBinder(AppStateMachine, _session, NotifyAutosaveKeyEvent);
            _outcomeBridge = new SessionOutcomeBridge(_eventBus, AppStateMachine, defer: true);

            IReadOnlyList<LaneAnchor> laneAnchors = _arenaRoot != null
                ? _arenaRoot.GetComponentsInChildren<LaneAnchor>(true)
                : Array.Empty<LaneAnchor>();

            _enemyPresenter.Initialize(_eventBus);
            _hud.Initialize(localization, Resources, _eventBus);
            _hud.PauseRequested += HandlePauseRequested;
            _resultScreen.Initialize(localization, AppStateMachine);
            _sceneFlow.Initialize(AppStateMachine);
            _playerController.Initialize(combatService, _inputAdapter, level.LaneCount);
            _playerView.Initialize(_eventBus, laneAnchors, combatService.PlayerLane);
            _inputAdapter.TapDetected += OnLaneTap;
            _inputAdapter.SwipeDetected += OnLaneSwipe;
            _gatePresenter.Initialize(mathEventDirector, _eventBus);
            _questionPresenter.BindClock(clock);
            _questionPresenter.Initialize(mathEventDirector, _eventBus);
            _feedbackPresenter.BindLayout(level.LaneCount, level.MathEvents.Count);
            _feedbackPresenter.Initialize(mathEventDirector, _eventBus);
            _lanePresenter.BindGeometry(laneAnchors, settings.DecisionZoneDepth);
            _lanePresenter.Initialize(combatService, mathEventDirector, _eventBus);

            // Phase-1 vertical slice (see class header): drive the state
            // machine straight to Gameplay with real, contracted triggers.
            AppStateMachine.Fire(AppTrigger.ConfigLoaded);
            AppStateMachine.Fire(AppTrigger.ProfileLoaded);
            AppStateMachine.Fire(AppTrigger.PlayPressed);
            AppStateMachine.Fire(AppTrigger.LevelSelected);

            _session.StartSession(level, weapons, enemyCatalog, _seed);
            _attemptRecorder.Begin(_session.SessionId, DateTimeOffset.UtcNow);
            _hud.InitializeWeaponSelection(_session, _eventBus);
            _inputAdapter.SetUiInputBlocker(_hud.BlocksGameplayInput);
            // Startup preparation is a hard gate before the first gameplay frame.
            // Resource initialization must precede this external startup gate.
            var preparation = _mathEventDirector.PrepareUpcomingEvents(clock.GameTime);
            _mathPreparedAtStartup = preparation.IsSuccess;
            ReportMathPreparation(preparation);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (_mathPreparedAtStartup && _session != null && _session.IsRunning && !_clock.IsPaused)
            {
                var preparation = _mathEventDirector.PrepareUpcomingEvents(_clock.GameTime);
                _session.Tick(deltaTime);
                _attemptRecorder.MaterializePending();
                _gatePresenter.RenderPending();
                _questionPresenter.RenderPending();
                _feedbackPresenter.RenderPending();
                _lanePresenter.RenderPending();
                _outcomeBridge.RenderPending();
                ReportMathPreparation(preparation);
            }
            _autosave?.Tick(deltaTime);
            if (_clock != null) _hud?.SetPaused(_clock.IsPaused);
            _hud?.RenderPending();
        }

        private void OnLaneTap(float _) => SubmitLane();
        private void OnLaneSwipe(SwipeDirection _) => SubmitLane();
        private void SubmitLane()
        {
            if (_mathPreparedAtStartup && _session.IsRunning && !_clock.IsPaused)
                _mathEventDirector.OnPlayerLaneSelected(Combat.PlayerLane, _clock.GameTime);
        }

        private void ReportMathPreparation(Result<bool> preparation)
        {
            string error = _pendingMathPreparationError ?? (preparation.IsSuccess ? null : preparation.ErrorCode);
            _pendingMathPreparationError = null;
            if (error != null && error != LastMathPreparationError) Debug.LogError(error, this);
            LastMathPreparationError = error;
        }

        private void HandleMathPreparationFailed(MathHorde.GameLogic.Events.MathEventPreparationFailed failure)
        {
            // Synchronous Tick callback: retain only immutable metadata. Log/UI
            // work is deferred until the session Tick has returned.
            _pendingMathPreparationError = failure.ErrorCode;
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<MathHorde.GameLogic.Events.MathEventPreparationFailed>(HandleMathPreparationFailed);
            if (_hud != null)
            {
                _hud.PauseRequested -= HandlePauseRequested;
            }

            if (_inputAdapter != null)
            {
                _inputAdapter.TapDetected -= OnLaneTap;
                _inputAdapter.SwipeDetected -= OnLaneSwipe;
            }
            _appFlowBinder?.Dispose();
            _outcomeBridge?.Dispose();
        }

        private void HandlePauseRequested()
        {
            AppStateMachine.Fire(AppTrigger.PausePressed);
        }

        private void NotifyAutosaveKeyEvent()
        {
            if (_session != null && !_session.IsRunning && !_statisticsWritten)
            {
                _pendingStatistics = _session.Finalize(_session.Result, _session.FailureReason);
            }
            _autosave.NotifyKeyEvent();
        }

        private void SaveProfileSnapshot()
        {
            // Independent writes: one transient IO failure must not discard the
            // finalized session or prevent the other offline save from running.
            // The existing key-event/periodic scheduler retries pending writes.
            try
            {
                _saveRepository.Save(_profileSnapshot);
                _profileSaveWarned = false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (!_profileSaveWarned) Debug.LogWarning("Local profile save failed; retained for autosave retry: " + error.Message, this);
                _profileSaveWarned = true;
            }
            if (_pendingStatistics == null || _statisticsWritten) return;
            try
            {
                new LocalStatisticsRepository(string.IsNullOrEmpty(_localDataDirectory) ? null : _localDataDirectory)
                    .Append(_pendingStatistics);
                _statisticsWritten = true;
                _pendingStatistics = null;
                _statisticsSaveWarned = false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (!_statisticsSaveWarned) Debug.LogWarning("Local statistics save failed; retained for autosave retry: " + error.Message, this);
                _statisticsSaveWarned = true;
            }
        }
    }
}
