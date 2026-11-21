// GameplayHUD.cs
// F1-15 / ADR-017 — the always-on combat HUD: health, shield,
// wave/level and pause button. Thin MonoBehaviour shell around
// GameplayHudModel — this class only wires Canvas/TMP references to model
// values and forwards the pause button tap; it makes no gameplay decisions
// (unity-ui skill). The math-question/challenge-level areas are
// DELIBERATELY not built yet (later phase) — GameplayHudBuilder reserves
// empty layout slots for them so they don't need to be retrofitted.
//
// Updates are event-driven (IGameEventSource.Subscribe), not polled from
// Update() — the only per-frame work in this file is none at all.
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Core.Common;
using MathHorde.Core.Resources;
using MathHorde.Presentation.Events;

namespace MathHorde.Presentation.Hud
{
    public sealed class GameplayHUD : MonoBehaviour
    {
        [Header("Élet")]
        [SerializeField] private TMP_Text _healthLabelText;
        [SerializeField] private TMP_Text _healthValueText;

        [Header("Pajzs (elrejtve, ha a pályán nincs pajzs-mechanika)")]
        [SerializeField] private GameObject _shieldGroup;
        [SerializeField] private TMP_Text _shieldLabelText;
        [SerializeField] private TMP_Text _shieldValueText;

        [Header("Hullám / pálya")]
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _levelText;

        [Header("Szünet")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private TMP_Text _pauseButtonText;
        [Header("Weapon selector")]
        [SerializeField] private GameObject _weaponGroup;
        [SerializeField] private Button[] _weaponButtons = System.Array.Empty<Button>();
        [SerializeField] private TMP_Text[] _weaponLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] _weaponActiveLabels = System.Array.Empty<TMP_Text>();
        private IGameSessionService _session;
        private IGameEventSource _weaponEvents;
        private bool _lastCanSelect;
        private bool _healthDirty;
        private bool _shieldDirty;
        private bool _waveDirty;

        private GameplayHudModel _model;
        private IGameEventSource _eventSource;
        private IResourceService _resourceService;

        public event System.Action PauseRequested;

        public void InitializeWeaponSelection(IGameSessionService session, IGameEventSource events)
        {
            _session = session;
            _weaponEvents = events;
            _model.InitializeWeaponSelection(session);
            events.Subscribe<WeaponChanged>(HandleWeaponChanged);
            for (int i = 0; i < _weaponButtons.Length; i++)
            {
                int index = i;
                bool visible = i < session.AvailableWeapons.Count;
                _weaponButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                _weaponLabels[i].text = _model.WeaponLabel(i);
                _weaponActiveLabels[i].text = _model.ActiveWeaponLabel;
                _weaponButtons[i].onClick.AddListener(() => _session.SelectWeapon(_session.AvailableWeapons[index].WeaponId));
            }
            if (_weaponGroup != null) _weaponGroup.SetActive(session.AvailableWeapons.Count > 1);
            RenderPending();
        }

        private void HandleWeaponChanged(WeaponChanged change) => _model.ApplyWeaponChanged(change);
        public void SetPaused(bool isPaused) => _model?.SetPaused(isPaused);

        // Called by the bootstrap after simulation Tick, including pause/end frames.
        public void RenderPending()
        {
            if (_healthDirty) { RefreshValueFor(ResourceType.Health); _healthDirty = false; }
            if (_shieldDirty) { RefreshValueFor(ResourceType.Shield); _shieldDirty = false; }
            if (_waveDirty) { SetText(_waveText, _model.WaveText); _waveDirty = false; }
            if (_session == null) return;
            bool canSelect = _model.CanSelectWeapon;
            if (!_model.WeaponsDirty && canSelect == _lastCanSelect) return;
            for (int i = 0; i < _session.AvailableWeapons.Count; i++)
            {
                _weaponButtons[i].interactable = canSelect;
                _weaponActiveLabels[i].gameObject.SetActive(_session.AvailableWeapons[i].WeaponId == _model.CurrentWeaponId);
            }
            _lastCanSelect = canSelect;
            _model.MarkWeaponsRendered();
        }

        public bool BlocksGameplayInput(Vector2 screenPosition)
        {
            if (_pauseButton != null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_pauseButton.transform, screenPosition)) return true;
            return _weaponGroup != null && _weaponGroup.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_weaponGroup.transform, screenPosition);
        }

        /// <summary>
        /// Wires localization, the live resource service and the event
        /// source. Call once from the scene's composition root at session
        /// start (not from Update()).
        /// </summary>
        public void Initialize(ILocalizationService localization, IResourceService resourceService, IGameEventSource eventSource)
        {
            _model = new GameplayHudModel(localization);
            _model.Initialize(resourceService);

            _eventSource = eventSource;
            _eventSource.Subscribe<ResourceChanged>(HandleResourceChanged);
            _eventSource.Subscribe<WaveStarted>(HandleWaveStarted);

            RefreshStaticLabels();
            RefreshAll(resourceService);
        }

        private void Awake()
        {
            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(RaisePauseRequested);
            }
        }

        private void OnDestroy()
        {
            _weaponEvents?.Unsubscribe<WeaponChanged>(HandleWeaponChanged);
            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(RaisePauseRequested);
            }

            if (_eventSource == null)
            {
                return;
            }

            _eventSource.Unsubscribe<ResourceChanged>(HandleResourceChanged);
            _eventSource.Unsubscribe<WaveStarted>(HandleWaveStarted);
        }

        private void RaisePauseRequested()
        {
            PauseRequested?.Invoke();
        }

        private void HandleResourceChanged(ResourceChanged e)
        {
            _model.ApplyResourceChanged(_resourceService, e.Change.Type);
            if (e.Change.Type == ResourceType.Health) _healthDirty = true;
            if (e.Change.Type == ResourceType.Shield) _shieldDirty = true;
        }

        private void HandleWaveStarted(WaveStarted e)
        {
            _model.ApplyWaveStarted(e.WaveIndex);
            _waveDirty = true;
        }

        private void RefreshStaticLabels()
        {
            SetText(_healthLabelText, _model.HealthLabel);
            SetText(_shieldLabelText, _model.ShieldLabel);
            SetText(_pauseButtonText, _model.PauseLabel);
        }

        private void RefreshAll(IResourceService resourceService)
        {
            _resourceService = resourceService;
            RefreshValueFor(ResourceType.Health);
            RefreshValueFor(ResourceType.Shield);

            SetText(_waveText, _model.WaveText);
            SetText(_levelText, _model.LevelText);
        }

        private void RefreshValueFor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Health:
                    SetText(_healthValueText, _model.HealthValueText);
                    break;
                case ResourceType.Shield:
                    SetText(_shieldValueText, _model.ShieldValueText);
                    if (_shieldGroup != null)
                    {
                        _shieldGroup.SetActive(_model.HasShield);
                    }
                    break;
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
