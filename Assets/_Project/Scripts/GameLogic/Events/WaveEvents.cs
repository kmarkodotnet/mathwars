using MathHorde.Core.Common;

namespace MathHorde.GameLogic.Events
{
    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-07) WaveDirector.</summary>
    public readonly struct WaveStarted : IGameEvent
    {
        public int WaveIndex { get; }
        public double GameTime { get; }

        public WaveStarted(int waveIndex, double gameTime)
        {
            WaveIndex = waveIndex;
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-07) WaveDirector.</summary>
    public readonly struct WaveCleared : IGameEvent
    {
        public int WaveIndex { get; }
        public double GameTime { get; }

        public WaveCleared(int waveIndex, double gameTime)
        {
            WaveIndex = waveIndex;
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-07) WaveDirector.</summary>
    public readonly struct EnemySpawned : IGameEvent
    {
        public int EnemyInstanceId { get; }
        public string EnemyId { get; }
        public LaneIndex Lane { get; }
        public Vec2 Position { get; }
        public double GameTime { get; }

        public EnemySpawned(int enemyInstanceId, string enemyId, LaneIndex lane, Vec2 position, double gameTime)
        {
            EnemyInstanceId = enemyInstanceId;
            EnemyId = enemyId;
            Lane = lane;
            Position = position;
            GameTime = gameTime;
        }
    }
}
