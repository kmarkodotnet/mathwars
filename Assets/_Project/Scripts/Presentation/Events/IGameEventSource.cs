// IGameEventSource.cs
// F1-14/F1-15 — the Presentation-side half of the GameLogic event contract
// (game-logic.md 1., rule G3: "a kibocsátó nem tud az előfizetőkről").
//
// IGameEventSink only has a one-way Publish method; nothing in Core/GameLogic
// is allowed to define a Subscribe method on the emitter side (that would
// give the business layer knowledge of its listeners). This interface is the
// Presentation-side counterpart: a typed subscription surface that a
// presenter (EnemyPresenter, GameplayHUD, ...) can depend on without caring
// which concrete broadcaster (GameEventBus) is wired up — keeping presenters
// testable against a fake event source with zero MonoBehaviour/Canvas
// involvement.
using MathHorde.GameLogic.Events;

namespace MathHorde.Presentation.Events
{
    public interface IGameEventSource
    {
        /// <summary>
        /// Registers a handler for every future Publish of TEvent. Multiple
        /// subscribers for the same event type are all invoked (multicast).
        /// </summary>
        void Subscribe<TEvent>(System.Action<TEvent> handler) where TEvent : struct, IGameEvent;

        /// <summary>
        /// Removes a previously registered handler. Safe to call with a
        /// handler that was never subscribed (no-op) — presenters call this
        /// unconditionally from OnDestroy without tracking subscription state.
        /// </summary>
        void Unsubscribe<TEvent>(System.Action<TEvent> handler) where TEvent : struct, IGameEvent;
    }
}
