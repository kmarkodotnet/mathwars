namespace MathHorde.Core.Common
{
    /// <summary>
    /// Engine-free 2D vector for simulation math (contract: core-domain.md 1.).
    /// X = lane axis, Y = depth/forward. Deliberately has no arithmetic
    /// operators beyond construction — the simulation code that needs vector
    /// math (CombatService, WaveDirector) is out of scope for this package;
    /// add operators there if/when needed rather than growing this type
    /// speculatively.
    /// </summary>
    public readonly struct Vec2
    {
        public float X { get; }
        public float Y { get; }

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";
    }
}
