// GameEventBus.cs
// F1-14/F1-15 — the single concrete IGameEventSink implementation that
// GameLogic services publish through, and that Presentation presenters
// subscribe to (IGameEventSource half). Pure routing/plumbing: it does not
// interpret, filter, or act on any event — it only fans a Publish<TEvent> out
// to every handler registered for that TEvent (contract game-logic.md 1.,
// rule G3). No business logic lives here.
//
// No UnityEngine dependency — this class could run headless — but it lives
// in the Presentation assembly (it is Presentation's wiring concern, not
// GameLogic's), so its NUnit coverage lives under Tests/PlayMode, the only
// test assembly that references MathHorde.Presentation (see
// MathHorde.Tests.EditMode.asmdef vs MathHorde.Tests.PlayMode.asmdef).
using System;
using System.Collections.Generic;
using MathHorde.GameLogic.Events;

namespace MathHorde.Presentation.Events
{
    public sealed class GameEventBus : IGameEventSink, IGameEventSource
    {
        private readonly Dictionary<Type, Delegate> _handlersByEventType = new Dictionary<Type, Delegate>();

        public void Publish<TEvent>(in TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            if (_handlersByEventType.TryGetValue(typeof(TEvent), out Delegate raw) && raw is Action<TEvent> handler)
            {
                handler.Invoke(gameEvent);
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Type key = typeof(TEvent);
            if (_handlersByEventType.TryGetValue(key, out Delegate existing))
            {
                _handlersByEventType[key] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlersByEventType[key] = handler;
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent
        {
            if (handler == null)
            {
                return;
            }

            Type key = typeof(TEvent);
            if (!_handlersByEventType.TryGetValue(key, out Delegate existing))
            {
                return;
            }

            Delegate updated = Delegate.Remove(existing, handler);
            if (updated == null)
            {
                _handlersByEventType.Remove(key);
            }
            else
            {
                _handlersByEventType[key] = updated;
            }
        }
    }
}
