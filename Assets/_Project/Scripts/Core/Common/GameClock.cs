namespace MathHorde.Core.Common
{
    /// <summary>
    /// Default IGameClock implementation. Unity-free so it can be driven
    /// headlessly in tests and by the future GameSessionService (F1-08) alike.
    /// </summary>
    public sealed class GameClock : IGameClock
    {
        private double _gameTime;
        private float _timeScale = 1.0f;

        public double GameTime => _gameTime;

        public bool IsPaused { get; private set; }

        public float TimeScale => _timeScale;

        public void Advance(float deltaTime)
        {
            if (IsPaused)
            {
                return;
            }

            if (deltaTime < 0f)
            {
                // Defensive: a negative frame delta is a programmer/engine error
                // upstream, not something this clock should propagate backwards.
                deltaTime = 0f;
            }

            _gameTime += deltaTime * _timeScale;
        }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;

        /// <summary>
        /// Dev-only time-scale override (contract comment: "dev-eszköz
        /// állíthatja"). Not part of IGameClock — a gameplay service only
        /// ever reads TimeScale, it never sets it.
        /// </summary>
        public void SetTimeScale(float timeScale)
        {
            _timeScale = timeScale < 0f ? 0f : timeScale;
        }
    }
}
