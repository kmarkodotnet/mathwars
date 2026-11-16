namespace MathHorde.Core.Common
{
    /// <summary>
    /// Monotonic game clock contract (core-domain.md 1.). Exactly one instance
    /// lives per GameSession. Every GameLogic service that needs "now" reads
    /// it from here (or from a Tick(double gameTime) parameter) — never from
    /// UnityEngine.Time, per rule G1 (game-logic.md 0.).
    /// </summary>
    public interface IGameClock
    {
        /// <summary>Elapsed game time in seconds since session start. Does not advance while paused.</summary>
        double GameTime { get; }

        bool IsPaused { get; }

        /// <summary>Default 1.0f; a dev tool may change it (see GameClock.SetTimeScale).</summary>
        float TimeScale { get; }

        /// <summary>Advances GameTime by deltaTime * TimeScale. Only the GameSessionService calls this.</summary>
        void Advance(float deltaTime);

        void Pause();

        void Resume();
    }
}
