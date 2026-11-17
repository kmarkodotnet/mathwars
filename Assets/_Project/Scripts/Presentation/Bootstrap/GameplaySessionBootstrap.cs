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
using UnityEngine;
using MathHorde.Core.Common;
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

        private IGameSessionService _session;
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
            _eventBus = new GameEventBus();
            var localization = new ResourcesLocalizationService();
            var clock = new UnityGameClock();

            var configRepository = new GameConfigurationRepository(
                _enemyAssets, _weaponAssets, new[] { _levelAsset }, _combatBalance);

            LevelDefinition level = configRepository.GetLevel(new MathHorde.Core.Progress.LevelId(_levelAsset.Id));
            WeaponDefinition weapon = configRepository.GetWeapon(level.WeaponId);
            IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog = configRepository.GetAllEnemies();

            Resources = new ResourceService(_eventBus, clock);
            var waveDirector = new WaveDirector();
            var combatService = new CombatService(Resources, _eventBus);
            Combat = combatService;
            var mathEventDirector = new NullMathEventDirector();

            _session = new GameSessionService(clock, waveDirector, combatService, mathEventDirector, Resources, _eventBus);

            AppStateMachine = new AppStateMachine();

            _saveRepository = new LocalSaveRepository();
            _profileSnapshot = _saveRepository.Load();
            _autosave = new AutosaveScheduler(SaveProfileSnapshot);

            _appFlowBinder = new GameSessionAppFlowBinder(AppStateMachine, _session, NotifyAutosaveKeyEvent);
            _outcomeBridge = new SessionOutcomeBridge(_eventBus, AppStateMachine);

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

            // Phase-1 vertical slice (see class header): drive the state
            // machine straight to Gameplay with real, contracted triggers.
            AppStateMachine.Fire(AppTrigger.ConfigLoaded);
            AppStateMachine.Fire(AppTrigger.ProfileLoaded);
            AppStateMachine.Fire(AppTrigger.PlayPressed);
            AppStateMachine.Fire(AppTrigger.LevelSelected);

            _session.StartSession(level, weapon, enemyCatalog, _seed);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _session?.Tick(deltaTime);
            _autosave?.Tick(deltaTime);
        }

        private void OnDestroy()
        {
            if (_hud != null)
            {
                _hud.PauseRequested -= HandlePauseRequested;
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
            _autosave.NotifyKeyEvent();
        }

        private void SaveProfileSnapshot()
        {
            _saveRepository.Save(_profileSnapshot);
        }
    }
}
