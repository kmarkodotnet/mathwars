// GameSessionAppFlowBinder.cs
// F1-17 — closes the gap F1-11's Napló flagged for this exact task: "a
// Pause/LevelComplete/GameOver állapotba lépéskor senki nem hívja még a
// GameSessionService.Pause/Resume/Finalize-t" and "az autosave nincs
// AppFocusLost-ra kötve" (spec 14./15., factory-docs/contract-findings.md
// #5). This is the decision logic for both: subscribe to
// IAppStateMachine.Transitioned once, and
//   - Pause()  on entering AppState.Pause (any trigger — PausePressed or
//     the Android AppFocusLost translation, SceneFlowManager already makes
//     both land here identically),
//   - Resume() on returning to AppState.Gameplay FROM AppState.Pause
//     (ResumePressed) — NOT from GameOver/LevelComplete (Retry/NextLevel),
//     where the old session is already finalized and a brand new
//     StartSession is what is actually needed (out of scope here — Phase 1
//     has no level-select flow yet to source the next LevelDefinition from;
//     see this task's Napló),
//   - Finalize(Result, FailureReason) exactly once on entering
//     LevelComplete/GameOver (idempotent — IGameSessionService.Finalize
//     itself no-ops on a second call, see game-logic.md 8.),
//   - the supplied onKeyPersistEvent callback on AppFocusLost AND on
//     LevelComplete/GameOver entry — the two "key events" explicit in spec
//     15./AutosaveScheduler's own header note ("level end" is one of the
//     three named key events; AppFocusLost is the save-is-not-only-on-clean-
//     shutdown case).
//
// Deliberately Unity-free despite living next to a MonoBehaviour-driven
// game: IAppStateMachine, IGameSessionService and the Action callback are
// all plain C#/GameLogic types, so this class — the actual decision logic
// the composition root needs — gets dotnet-test coverage (same reasoning as
// Core.Common.LaneGestureResolver, unity-csharp skill "Buktatók" F1-13
// entry). The MonoBehaviour composition root (Presentation.Bootstrap.
// GameplaySessionBootstrap) only constructs this and passes it a lambda
// that forwards to the Infrastructure AutosaveScheduler — it contains no
// decision logic of its own.
using System;

namespace MathHorde.GameLogic.AppFlow
{
    public sealed class GameSessionAppFlowBinder : IDisposable
    {
        private readonly IAppStateMachine _stateMachine;
        private readonly IGameSessionService _session;
        private readonly Action _onKeyPersistEvent;
        private bool _disposed;

        public GameSessionAppFlowBinder(
            IAppStateMachine stateMachine,
            IGameSessionService session,
            Action onKeyPersistEvent)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _onKeyPersistEvent = onKeyPersistEvent ?? throw new ArgumentNullException(nameof(onKeyPersistEvent));

            _stateMachine.Transitioned += HandleTransitioned;
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
            if (transition.To == AppState.Pause)
            {
                _session.Pause();
            }
            else if (transition.To == AppState.Gameplay && transition.From == AppState.Pause)
            {
                _session.Resume();
            }
            else if (transition.To == AppState.LevelComplete || transition.To == AppState.GameOver)
            {
                // Idempotent (game-logic.md 8.) — safe even if something else
                // already finalized this session on the same tick.
                _session.Finalize(_session.Result, _session.FailureReason);
                _onKeyPersistEvent();
            }

            if (transition.Trigger == AppTrigger.AppFocusLost)
            {
                _onKeyPersistEvent();
            }
        }
    }
}
