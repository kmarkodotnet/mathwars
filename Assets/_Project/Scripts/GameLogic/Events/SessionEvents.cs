using MathHorde.Core.Progress;

namespace MathHorde.GameLogic.Events
{
    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-08) GameSessionService.</summary>
    public readonly struct SessionStarted : IGameEvent
    {
        public GameSessionId SessionId { get; }
        public LevelId LevelId { get; }
        public int Seed { get; }
        public double GameTime { get; }

        public SessionStarted(GameSessionId sessionId, LevelId levelId, int seed, double gameTime)
        {
            SessionId = sessionId;
            LevelId = levelId;
            Seed = seed;
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-08) GameSessionService.</summary>
    public readonly struct SessionEnded : IGameEvent
    {
        public GameSessionId SessionId { get; }
        public SessionResult Result { get; }
        public FailureReason Reason { get; }
        public double GameTime { get; }

        public SessionEnded(GameSessionId sessionId, SessionResult result, FailureReason reason, double gameTime)
        {
            SessionId = sessionId;
            Result = result;
            Reason = reason;
            GameTime = gameTime;
        }
    }
}
