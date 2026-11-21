namespace MathHorde.Core.Resources
{
    /// <summary>
    /// Generic resource model (spec 4.3): adding a new type here never
    /// requires a code change in ResourceService — the service only ever
    /// looks up ResourcePool entries by this key.
    /// Ammo = 0 is an inactive legacy tombstone; MVP uses Health/Shield; EnemyArmor/Energy are reserved for later
    /// phases (contract core-domain.md 5.).
    /// </summary>
    public enum ResourceType
    {
        Ammo = 0,
        Health = 1,
        Shield = 2,
        EnemyArmor = 3,
        Energy = 4
    }
}
