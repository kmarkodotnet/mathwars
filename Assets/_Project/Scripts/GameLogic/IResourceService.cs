using MathHorde.Core.Levels;
using MathHorde.Core.Resources;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 2. (spec 4.3, 5.1) — the single mutation path
    /// for every resource. Combat, math gates and the HUD all read through
    /// this service so they can never disagree about the current value.
    ///
    /// Deliberate omissions (contract 2.1 TILTÁSOK): no public
    /// SetCurrent(type, absoluteValue) — a gate can never overwrite whatever
    /// the actual value is; it can only request a relative delta.
    /// </summary>
    public interface IResourceService
    {
        ResourceState State { get; }

        int GetCurrent(ResourceType type);

        int GetMax(ResourceType type);

        /// <summary>
        /// The one mutation path. Positive or negative delta. Clamps to
        /// [Min, Max], never throws. reasonCode: "WEAPON_FIRE" | "MATH_GATE" |
        /// "ENEMY_CONTACT" | "PICKUP" | "LEVEL_INIT" (or a new caller-defined code).
        /// </summary>
        ResourceChange ApplyDelta(ResourceType type, int delta, string reasonCode);

        /// <summary>
        /// Reserves an exact amount: only deducts if the full amount is
        /// available. No partial consumption — this is what the weapon uses.
        /// </summary>
        bool TryConsume(ResourceType type, int amount, string reasonCode, out ResourceChange change);

        /// <summary>Changes the Max (upgrade). Current is clamped down if it would exceed the new Max.</summary>
        ResourceChange SetMax(ResourceType type, int newMax, string reasonCode);

        /// <summary>
        /// Records the math-event baseline (spec 5.1). The snapshot is a
        /// read-only copy — nothing written afterwards is ever overwritten by
        /// a later gate resolution; the reward is always computed as a delta
        /// against this captured value.
        /// </summary>
        int CaptureSnapshot(ResourceType type);

        /// <summary>Initial fill for a level, from its LevelDefinition. Only the GameSessionService calls this.</summary>
        void Initialize(LevelDefinition level);
    }
}
