// LevelResultScreen.cs
// F1-16 — GameOver/LevelComplete minimal result screen (math-horde.md 19./1.,
// kritérium A, G, részben K). Thin MonoBehaviour shell around
// LevelResultModel (unity-ui skill: "a döntési logika a GameLogic/Core
// rétegben marad" — here the model only picks text/trigger pairs, it makes
// no gameplay decision). Shows/hides itself purely in reaction to
// IAppStateMachine.Transitioned (ARCHITECTURE.md 4.: "a SceneFlowManager és
// UI presenterek fire-elik [az AppTrigger-eket]; soha nem állítják az
// AppState-et direktben") — this class never sets AppState itself, it only
// forwards a button tap to the already-defined AppTrigger for the current
// state, exactly like any other input-forwarding UI control.
//
// Deliberately does NOT depend on IGameSessionService/GameSessionRecord —
// see LevelResultModel.cs header for the scope note (F1-17/Fázis 5 wires
// Finalize() and session stats onto this screen later).
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MathHorde.Core.Common;
using MathHorde.GameLogic.AppFlow;

namespace MathHorde.Presentation.Result
{
    public sealed class LevelResultScreen : MonoBehaviour
    {
        [Header("Panel (meg-/elrejtve, nem a gyökér Canvas)")]
        [SerializeField] private GameObject _panelRoot;

        [Header("Szöveg")]
        [SerializeField] private TMP_Text _headlineText;
        [SerializeField] private TMP_Text _encouragementText;

        [Header("Elsődleges gomb (Retry / NextLevel)")]
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TMP_Text _primaryButtonText;

        [Header("Másodlagos gomb (BackToWorlds / Continue)")]
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private TMP_Text _secondaryButtonText;

        private LevelResultModel _model;
        private IAppStateMachine _appStateMachine;

        /// <summary>
        /// Wires localization and the app state machine. Call once from the
        /// scene's composition root at scene start (not from Update()).
        /// </summary>
        public void Initialize(ILocalizationService localization, IAppStateMachine appStateMachine)
        {
            _model = new LevelResultModel(localization);
            _appStateMachine = appStateMachine ?? throw new ArgumentNullException(nameof(appStateMachine));
            _appStateMachine.Transitioned += HandleTransitioned;

            ApplyState(_appStateMachine.Current);
        }

        private void Awake()
        {
            if (_primaryButton != null)
            {
                _primaryButton.onClick.AddListener(FirePrimaryTrigger);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.onClick.AddListener(FireSecondaryTrigger);
            }
        }

        private void OnDestroy()
        {
            if (_primaryButton != null)
            {
                _primaryButton.onClick.RemoveListener(FirePrimaryTrigger);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.onClick.RemoveListener(FireSecondaryTrigger);
            }

            if (_appStateMachine != null)
            {
                _appStateMachine.Transitioned -= HandleTransitioned;
            }
        }

        private void HandleTransitioned(AppStateTransition transition)
        {
            ApplyState(transition.To);
        }

        private void ApplyState(AppState state)
        {
            bool isResultState = _model.TryBuild(state, out LevelResultContent content);

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(isResultState);
            }

            if (!isResultState)
            {
                return;
            }

            SetText(_headlineText, content.Headline);
            SetText(_encouragementText, content.Encouragement);
            SetText(_primaryButtonText, content.PrimaryLabel);
            SetText(_secondaryButtonText, content.SecondaryLabel);
        }

        private void FirePrimaryTrigger()
        {
            if (_appStateMachine == null || _model == null)
            {
                return;
            }

            if (_model.TryBuild(_appStateMachine.Current, out LevelResultContent content))
            {
                _appStateMachine.Fire(content.PrimaryTrigger);
            }
        }

        private void FireSecondaryTrigger()
        {
            if (_appStateMachine == null || _model == null)
            {
                return;
            }

            if (_model.TryBuild(_appStateMachine.Current, out LevelResultContent content))
            {
                _appStateMachine.Fire(content.SecondaryTrigger);
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
