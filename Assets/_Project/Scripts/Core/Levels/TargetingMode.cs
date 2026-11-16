namespace MathHorde.Core.Levels
{
    /// <summary>Contract core-domain.md 7. / game-logic.md 3.2. Default is OwnLaneThenAdjacent.</summary>
    public enum TargetingMode
    {
        OwnLaneNearestOnly,
        OwnLaneThenAdjacent,
        GlobalNearest
    }
}
