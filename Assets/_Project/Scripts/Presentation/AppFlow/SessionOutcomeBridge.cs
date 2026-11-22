// SessionOutcomeBridge.cs
// F1-17 — the other half of wiring GameSessionService's outcome into the
// application-level state machine (ARCHITECTURE.md 4.: "a SceneFlowManager
// és UI presenterek fire-elik [az AppTrigger-eket]; soha nem állítják az
// AppState-et direktben" — GameSessionService itself must NOT depend on
// IAppStateMachine, so something on the Presentation side has to listen for
// the GameLogic-level SessionEnded event and translate it into the matching
// trigger).
//
// Lives in Presentation, not GameLogic/AppFlow, because the only way to
// listen for SessionEnded from outside GameLogic is through
// IGameEventSource — and that subscribe-side interface is deliberately
// Presentation-owned (game-logic.md 1., rule G3: "a kibocsátó nem tud az
// előfizetőkről"; see Presentation/Events/IGameEventSource.cs header).
// Infrastructure is not an option either — ARCHITECTURE.md 2. forbids
// Infrastructure from referencing Presentation. Deliberately plain C# (no
// MonoBehaviour): the composition root (GameplaySessionBootstrap) owns its
// lifetime and disposes it on scene teardown, same pattern as
// MathHorde.Infrastructure.SceneFlow.SceneFlowManager.
using System;
using MathHorde.Core.Progress;
using MathHorde.GameLogic.AppFlow;
using MathHorde.GameLogic.Events;
using MathHorde.Presentation.Events;

namespace MathHorde.Presentation.AppFlow
{
    public sealed class SessionOutcomeBridge : IDisposable
    {
        private readonly IGameEventSource _eventSource;
        private readonly IAppStateMachine _stateMachine;
        private bool _disposed;
        private readonly bool _defer;
        private SessionEnded? _pending;

        public SessionOutcomeBridge(IGameEventSource eventSource, IAppStateMachine stateMachine, bool defer = false)
        {
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _defer = defer;

            _eventSource.Subscribe<SessionEnded>(HandleSessionEnded);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _eventSource.Unsubscribe<SessionEnded>(HandleSessionEnded);
            _disposed = true;
        }

        private void HandleSessionEnded(SessionEnded e)
        {
            if (_defer) { _pending = e; return; }
            Apply(e);
        }

        public void RenderPending()
        {
            if (!_pending.HasValue) return;
            var ended = _pending.Value;
            Apply(ended);
            _pending = null;
        }

        private void Apply(SessionEnded e)
        {
            AppTrigger trigger = e.Result == SessionResult.Victory
                ? AppTrigger.VictoryConditionMet
                : AppTrigger.PlayerHealthZero;

            _stateMachine.Fire(trigger);
        }
    }
}
