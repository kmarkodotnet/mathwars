using System;

namespace MathHorde.GameLogic.AppFlow
{
    /// <summary>
    /// The application-level state machine named in ARCHITECTURE.md 4.
    /// ("Felelős: SceneFlowManager (Infrastructure) + IAppStateMachine
    /// (GameLogic kontrakt)"). Unity-free and deterministic — no scene
    /// loading, no I/O, no IGameClock access — so it is fully covered by
    /// EditMode/dotnet-test. SceneFlowManager (Infrastructure) is the only
    /// caller that reacts to Transitioned by loading a scene; this
    /// interface itself never touches a scene.
    ///
    /// Note (F1-11 contract gap, flagged for the architect — see Napló):
    /// ARCHITECTURE.md 4. names this type but docs/contracts/game-logic.md
    /// does not define it. This file follows the ARCHITECTURE.md 4. mermaid
    /// diagram literally; no transition, state or trigger was invented
    /// beyond what that diagram already specifies.
    /// </summary>
    public interface IAppStateMachine
    {
        AppState Current { get; }

        /// <summary>True if Fire(trigger) would currently change state. Pure — does not mutate.</summary>
        bool CanFire(AppTrigger trigger);

        /// <summary>
        /// Attempts the transition for (Current, trigger) from the
        /// ARCHITECTURE.md 4. table. Returns false and leaves Current
        /// unchanged if no such edge exists from the current state (e.g. a
        /// stray BackPressed outside WorldSelection/ParentDashboard/
        /// Settings) — this mirrors the idempotent-no-throw style used by
        /// IGateResolutionService/IRewardService elsewhere in GameLogic
        /// rather than introducing a new exception type.
        /// </summary>
        bool Fire(AppTrigger trigger);

        /// <summary>Raised synchronously, after Current has already been updated, exactly once per successful Fire.</summary>
        event Action<AppStateTransition> Transitioned;
    }
}
