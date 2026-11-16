using System;
using System.Collections.Generic;
using MathHorde.GameLogic.AppFlow;

namespace MathHorde.Infrastructure.SceneFlow
{
    /// <summary>
    /// F1-11 — the decision half of the Infrastructure SceneFlowManager
    /// named in ARCHITECTURE.md 4. Plain C# (no MonoBehaviour lifecycle of
    /// its own, unity-csharp skill: "MonoBehaviour-ban nincs üzleti logika")
    /// so it is directly constructible and testable with a fake
    /// ISceneLoader; SceneFlowManagerBehaviour is the 1-line MonoBehaviour
    /// shell that owns one of these and forwards Unity lifecycle callbacks
    /// to it.
    ///
    /// Two responsibilities:
    ///  1. On every IAppStateMachine.Transitioned, load the scene bound to
    ///     the destination AppState (if any binding exists for it).
    ///  2. Translate the Android OnApplicationPause(true) signal into the
    ///     AppFocusLost trigger (ARCHITECTURE.md 4.) — AppStateMachine's own
    ///     transition table already makes this a no-op outside Gameplay,
    ///     so no extra guard is needed here.
    ///
    /// Deliberately out of scope for this type (left to the scene/session
    /// composition root that wires IGameSessionService — not part of F1-11):
    /// calling IGameSessionService.Pause()/Finalize() on Pause/LevelComplete/
    /// GameOver entry, and autosave-on-AppFocusLost. A caller can still add
    /// either without touching this class, by also subscribing to the same
    /// IAppStateMachine.Transitioned event.
    /// </summary>
    public sealed class SceneFlowManager : IDisposable
    {
        private readonly IAppStateMachine _stateMachine;
        private readonly ISceneLoader _sceneLoader;
        private readonly IReadOnlyDictionary<AppState, string> _sceneByState;
        private bool _disposed;

        public SceneFlowManager(
            IAppStateMachine stateMachine,
            ISceneLoader sceneLoader,
            IReadOnlyDictionary<AppState, string> sceneByState)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            _sceneByState = sceneByState ?? throw new ArgumentNullException(nameof(sceneByState));

            _stateMachine.Transitioned += HandleTransitioned;
        }

        /// <summary>Android OnApplicationPause lifecycle forward. pauseStatus == false (focus regained) is intentionally a no-op: resuming is always the explicit ResumePressed action, never automatic.</summary>
        public void HandleApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                _stateMachine.Fire(AppTrigger.AppFocusLost);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _stateMachine.Transitioned -= HandleTransitioned;
            _disposed = true;
        }

        private void HandleTransitioned(AppStateTransition transition)
        {
            if (_sceneByState.TryGetValue(transition.To, out string sceneName) && !string.IsNullOrEmpty(sceneName))
            {
                _sceneLoader.Load(sceneName);
            }
        }
    }
}
