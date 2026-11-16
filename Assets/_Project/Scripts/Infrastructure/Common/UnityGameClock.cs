using MathHorde.Core.Common;
using UnityEngine;

namespace MathHorde.Infrastructure.Common
{
    /// <summary>
    /// The Unity-side IGameClock (F1-11 — "UnityGameClock az IGameClock
    /// (Core) Unity-oldali implementációja"). GameTime bookkeeping itself is
    /// delegated to the already-tested Core.Common.GameClock (composition,
    /// not reimplementation) — GameSessionService.Tick still owns calling
    /// Advance/Pause/Resume (ARCHITECTURE.md 6.1); this class is driven the
    /// same way, never by its own Update().
    ///
    /// What this class adds over the plain Core clock: Pause()/Resume() also
    /// mirror onto UnityEngine.Time.timeScale, so Animator/ParticleSystem/
    /// Physics — none of which read IGameClock — freeze in lockstep with
    /// the GameLogic session instead of only the simulation steps
    /// GameSessionService itself skips while paused. This is why the type
    /// has to live in Infrastructure (UnityEngine-referencing) rather than
    /// just reusing Core.Common.GameClock directly for the real game.
    /// </summary>
    public sealed class UnityGameClock : IGameClock
    {
        private readonly GameClock _inner = new GameClock();
        private float _timeScaleBeforePause = 1f;

        public double GameTime => _inner.GameTime;

        public bool IsPaused => _inner.IsPaused;

        public float TimeScale => _inner.TimeScale;

        public void Advance(float deltaTime) => _inner.Advance(deltaTime);

        public void Pause()
        {
            if (_inner.IsPaused)
            {
                return;
            }

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            _inner.Pause();
        }

        public void Resume()
        {
            if (!_inner.IsPaused)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforePause;
            _inner.Resume();
        }

        /// <summary>Dev-only override, forwarded to the inner clock (see GameClock.SetTimeScale).</summary>
        public void SetTimeScale(float timeScale) => _inner.SetTimeScale(timeScale);
    }
}
