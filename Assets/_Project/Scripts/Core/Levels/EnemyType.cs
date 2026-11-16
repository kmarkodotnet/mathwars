namespace MathHorde.Core.Levels
{
    /// <summary>
    /// Contract core-domain.md 7. Closed enum with explicit numeric values
    /// (core-domain.md 0. enum rule — Unity serializes enums as int, so a
    /// reordering would silently rewrite saved assets/sessions). New
    /// archetypes are appended at the end with a new number only (see the
    /// contract's F5+ roadmap table: Splitter=5, Shifter=6, Mirror=7,
    /// Prime=8, Cipher=9 — none of those exist yet in Phase 1-4).
    ///
    /// Swarmlet (ADR-008) is the fifth value, present from F1: the
    /// horde-survival "mow down a mass of enemies" experience needs a
    /// 1-HP, 4-6-unit swarm archetype, and balance/difficulty-model.md
    /// already models a "swarm ratio" column starting at CL2 — without this
    /// value that accepted balance model has nothing to implement against.
    /// </summary>
    public enum EnemyType
    {
        Basic = 0,
        Fast = 1,
        Armored = 2,
        Boss = 3,

        /// <summary>
        /// Tiny, 1-HP "cannon fodder" that spawns in packs of 4-6
        /// (ADR-008, GAME_DESIGN.md 5.).
        /// </summary>
        Swarmlet = 4
    }
}
