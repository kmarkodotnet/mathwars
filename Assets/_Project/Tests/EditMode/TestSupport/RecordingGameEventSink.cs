using System.Collections.Generic;
using MathHorde.GameLogic.Events;

namespace MathHorde.Tests.EditMode.TestSupport
{
    /// <summary>
    /// Shared test double for IGameEventSink (contract game-logic.md 1.).
    /// Boxes every published struct event so fixtures can assert both that a
    /// publish happened at all, and inspect the payload of a specific event
    /// type afterwards.
    /// </summary>
    public sealed class RecordingGameEventSink : IGameEventSink
    {
        private readonly List<object> _published = new List<object>();

        public int PublishedCount => _published.Count;

        public void Publish<TEvent>(in TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            _published.Add(gameEvent);
        }

        public IReadOnlyList<TEvent> OfType<TEvent>() where TEvent : struct, IGameEvent
        {
            var result = new List<TEvent>();
            foreach (var published in _published)
            {
                if (published is TEvent typed)
                {
                    result.Add(typed);
                }
            }

            return result;
        }
    }
}
