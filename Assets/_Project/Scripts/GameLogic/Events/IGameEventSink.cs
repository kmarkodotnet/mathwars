namespace MathHorde.GameLogic.Events
{
    /// <summary>
    /// Contract game-logic.md 1. Event emission goes through this sink; the
    /// emitter never knows who (if anyone) is subscribed (rule G3). No
    /// concrete production implementation lives in GameLogic — Presentation /
    /// Infrastructure wire a real subscriber bridge; tests use a small
    /// recording fake (see Tests/EditMode/TestSupport/RecordingGameEventSink.cs).
    /// </summary>
    public interface IGameEventSink
    {
        void Publish<TEvent>(in TEvent gameEvent) where TEvent : struct, IGameEvent;
    }
}
