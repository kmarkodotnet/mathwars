namespace MathHorde.GameLogic
{
    /// <summary>
    /// PHASE 1 PLACEHOLDER — not the contract's full interface.
    ///
    /// game-logic.md 5. specifies the full IMathEventDirector (ActiveEvents,
    /// PendingResolutions/ResolveCommand, OnPlayerLaneSelected,
    /// OnSessionEnded, ApplyTimePressure, ...), built against MathProblem,
    /// IMathProblemGenerator and DifficultyProfile — none of which exist in
    /// this codebase yet (MathEngine/Problems layer, Phase 3, a different
    /// agent's scope). Per this package's brief: "a 4–5. lépés most
    /// üres/no-op kiterjesztési pont legyen, tiszta interfész mögött" — so
    /// this narrow slice is deliberately all GameSessionService needs for
    /// its Tick order (ARCHITECTURE 6.1 step 4) today: a tick hook that is
    /// guaranteed to never touch combat state.
    ///
    /// When F3 lands the real IMathEventDirector, GameSessionService should
    /// be updated to depend on the full contract (Tick/PendingResolutions/
    /// ClearPendingResolutions) instead of this placeholder — this type
    /// should then be retired, not extended.
    /// </summary>
    public interface IMathEventDirector
    {
        /// <summary>
        /// Must NEVER modify combat state and must NEVER call
        /// IGameClock.Pause() (game-logic.md 5. independence contract) —
        /// enforced here simply by this method taking no combat-service
        /// reference at all.
        /// </summary>
        void Tick(double gameTime);
    }

    /// <summary>
    /// Meaningful no-op implementation (NOT a TODO) — correct for every
    /// level until F3 introduces real math events: there are none, so
    /// there is nothing to tick.
    /// </summary>
    public sealed class NullMathEventDirector : IMathEventDirector
    {
        public void Tick(double gameTime)
        {
            // Intentionally empty: no math-event state machine exists yet
            // (Phase 3). This is the documented extension point, not a gap.
        }
    }
}
