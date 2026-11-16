using System;

// local-persistence skill: "Mentési gyakoriság: periodikus autosave ÉS
// kulcs-eseményekre kötött mentés — nem kizárólag alkalmazás-bezáráskor."
// (ADR-011 5.) This class is the Unity-free mechanism; wiring a
// MonoBehaviour.Update() tick and the actual key events (level end,
// challenge-level change, parent settings change) to it is a later
// integration task's scope.
namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// Triggers a caller-supplied save action either after IntervalSeconds
    /// of accumulated Tick(deltaTime) calls, or immediately on
    /// NotifyKeyEvent(). Deliberately has no dependency on
    /// Application.quit/OnApplicationPause — this is the whole point: the
    /// save must not depend on a clean shutdown ever happening.
    /// </summary>
    public sealed class AutosaveScheduler
    {
        private readonly Action _save;
        private double _secondsSinceLastSave;

        public float IntervalSeconds { get; }

        public AutosaveScheduler(Action save, float intervalSeconds = 30f)
        {
            if (intervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), intervalSeconds, "IntervalSeconds pozitív kell legyen.");
            }

            _save = save ?? throw new ArgumentNullException(nameof(save));
            IntervalSeconds = intervalSeconds;
        }

        /// <summary>Call once per frame/update with the elapsed time. Triggers a save and resets the timer once IntervalSeconds has accumulated.</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "deltaTime nem lehet negatív.");
            }

            _secondsSinceLastSave += deltaTime;
            if (_secondsSinceLastSave >= IntervalSeconds)
            {
                _secondsSinceLastSave = 0;
                _save();
            }
        }

        /// <summary>
        /// Call on a key event (level end, challenge-level change, parent
        /// settings change) — saves immediately and resets the periodic
        /// timer, so a key-event save never gets duplicated a few seconds
        /// later by the periodic tick.
        /// </summary>
        public void NotifyKeyEvent()
        {
            _secondsSinceLastSave = 0;
            _save();
        }
    }
}
