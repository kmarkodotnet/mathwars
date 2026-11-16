using MathHorde.Core.Common;

namespace MathHorde.GameLogic.Events
{
    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-06) CombatService.</summary>
    public readonly struct EnemyDamaged : IGameEvent
    {
        public int EnemyInstanceId { get; }
        public int Damage { get; }
        public int RemainingHealth { get; }
        public double GameTime { get; }

        public EnemyDamaged(int enemyInstanceId, int damage, int remainingHealth, double gameTime)
        {
            EnemyInstanceId = enemyInstanceId;
            Damage = damage;
            RemainingHealth = remainingHealth;
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-06) CombatService.</summary>
    public readonly struct EnemyKilled : IGameEvent
    {
        public int EnemyInstanceId { get; }
        public string EnemyId { get; }
        public int RewardValue { get; }
        public double GameTime { get; }

        public EnemyKilled(int enemyInstanceId, string enemyId, int rewardValue, double gameTime)
        {
            EnemyInstanceId = enemyInstanceId;
            EnemyId = enemyId;
            RewardValue = rewardValue;
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-06) CombatService.</summary>
    public readonly struct EnemyReachedPlayer : IGameEvent
    {
        public int EnemyInstanceId { get; }
        public int ContactDamage { get; }
        public double GameTime { get; }

        public EnemyReachedPlayer(int enemyInstanceId, int contactDamage, double gameTime)
        {
            EnemyInstanceId = enemyInstanceId;
            ContactDamage = contactDamage;
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-06) CombatService.</summary>
    public readonly struct WeaponFired : IGameEvent
    {
        public int TargetEnemyInstanceId { get; }
        public int AmmoUsed { get; }
        public double GameTime { get; }

        public WeaponFired(int targetEnemyInstanceId, int ammoUsed, double gameTime)
        {
            TargetEnemyInstanceId = targetEnemyInstanceId;
            AmmoUsed = ammoUsed;
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-06) CombatService when there is no ammo.</summary>
    public readonly struct WeaponDryFire : IGameEvent
    {
        public double GameTime { get; }

        public WeaponDryFire(double gameTime)
        {
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-06) CombatService.</summary>
    public readonly struct PlayerDamaged : IGameEvent
    {
        public int Damage { get; }
        public int RemainingHealth { get; }
        public double GameTime { get; }

        public PlayerDamaged(int damage, int remainingHealth, double gameTime)
        {
            Damage = damage;
            RemainingHealth = remainingHealth;
            GameTime = gameTime;
        }
    }

    /// <summary>Contract game-logic.md 1. Emitted by the (future, F1-06) CombatService.</summary>
    public readonly struct PlayerLaneChanged : IGameEvent
    {
        public LaneIndex From { get; }
        public LaneIndex To { get; }
        public double GameTime { get; }

        public PlayerLaneChanged(LaneIndex from, LaneIndex to, double gameTime)
        {
            From = from;
            To = to;
            GameTime = gameTime;
        }
    }
}
