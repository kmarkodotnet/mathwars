// GameplayHUD.cs
// F1-15 — the always-on combat HUD (math-horde.md 3.2): health, ammo,
// shield, wave/pálya, pause button. Thin MonoBehaviour shell around
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

        [Header("Lőszer")]
        [SerializeField] private TMP_Text _ammoLabelText;
        [SerializeField] private TMP_Text _ammoValueText;

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

        private GameplayHudModel _model;
        private IGameEventSource _eventSource;
        private IResourceService _resourceService;

        public event System.Action PauseRequested;

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
            RefreshValueFor(e.Change.Type);
        }

        private void HandleWaveStarted(WaveStarted e)
        {
            _model.ApplyWaveStarted(e.WaveIndex);
            if (_waveText != null)
            {
                _waveText.text = _model.WaveText;
            }
        }

        private void RefreshStaticLabels()
        {
            SetText(_healthLabelText, _model.HealthLabel);
            SetText(_ammoLabelText, _model.AmmoLabel);
            SetText(_shieldLabelText, _model.ShieldLabel);
            SetText(_pauseButtonText, _model.PauseLabel);
        }

        private void RefreshAll(IResourceService resourceService)
        {
            _resourceService = resourceService;
            RefreshValueFor(ResourceType.Health);
            RefreshValueFor(ResourceType.Ammo);
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
                case ResourceType.Ammo:
                    SetText(_ammoValueText, _model.AmmoValueText);
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
