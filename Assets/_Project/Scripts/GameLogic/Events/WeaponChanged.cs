namespace MathHorde.GameLogic.Events
{
    public readonly struct WeaponChanged : IGameEvent
    {
        public string PreviousWeaponId { get; }
        public string WeaponId { get; }
        public double GameTime { get; }
        public WeaponChanged(string previousWeaponId, string weaponId, double gameTime)
        {
            PreviousWeaponId = previousWeaponId;
            WeaponId = weaponId;
            GameTime = gameTime;
        }
    }
}
