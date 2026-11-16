namespace MathHorde.Core.Resources
{
    /// <summary>
    /// Generic resource model (spec 4.3): adding a new type here never
    /// requires a code change in ResourceService — the service only ever
    /// looks up ResourcePool entries by this key.
    /// MVP uses Ammo/Health/Shield; EnemyArmor/Energy are reserved for later
    /// phases (contract core-domain.md 5.).
    /// </summary>
    public enum ResourceType
    {
        Ammo,
        Health,
        Shield,
        EnemyArmor,
        Energy
    }
}
