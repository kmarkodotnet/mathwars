using System.Collections.Generic;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 8. — the composition root. The ONLY place the
    /// Tick order (ARCHITECTURE 6.1) is implemented; Presentation's
    /// MonoBehaviour.Update calls this and nothing else.
    ///
    /// FLAGGED DEVIATION from the contracted signature
    /// (`StartSession(LevelDefinition level, PlayerProgress progress, int seed)`):
    /// `PlayerProgress` does not exist in this codebase yet — it depends on
    /// SkillProfile/DifficultyProfile (core-domain.md 4.), which in turn
    /// depend on SkillTag/BinaryOperator enums owned by the MathEngine/
    /// Problems layer (a different, not-yet-landed task). Phase 1 also has
    /// no consumer for it (IAdaptiveDifficultyService is Phase 4 vapor).
    /// In its place, StartSession takes the already-resolved
    /// WeaponDefinition and enemy catalog that ICombatService.Initialize
    /// needs — the contract does not say how GameSessionService is
    /// supposed to obtain these (presumably IConfigurationProvider,
    /// game-logic.md 11., which itself is not implementable yet for the
    /// same reason). This keeps GameSessionService fully testable without
    /// fabricating that chain. Recommend the architect revisit once
    /// PlayerProgress/IConfigurationProvider land.
    /// </summary>
    public interface IGameSessionService
    {
        GameSessionId SessionId { get; }

        SessionResult Result { get; }

        bool IsRunning { get; }

        /// <summary>
        /// Non-contracted but necessary addition: the reason the session
        /// ended (None while InProgress/Victory). Exposed so a caller can
        /// pass it straight into Finalize without separately tracking it —
        /// see the FailureReason/LossDiagnosis distinction in game-logic.md 8.
        /// </summary>
        FailureReason FailureReason { get; }

        void StartSession(
            LevelDefinition level,
            WeaponDefinition weapon,
            IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog,
            int seed);

        /// <summary>
        /// The mandatory Tick order (ARCHITECTURE 6.1):
        /// 1. clock.Advance  2. waveDirector.Tick + spawn drain
        /// 3. combatService.Tick (combat, independent of math state)
        /// 4. mathEventDirector.Tick (no-op placeholder, Phase 1)
        /// 5. victory/defeat evaluation.
        /// A paused clock (IGameClock.IsPaused) skips 2-5 entirely for this
        /// call — "szünetben sem harc, sem határidő nem halad".
        /// </summary>
        void Tick(float deltaTime);

        void Pause();

        void Resume();

        /// <summary>
        /// Produces the session's GameSessionRecord. Idempotent: every call
        /// after the first returns the SAME cached record, no re-mutation.
        /// </summary>
        GameSessionRecord Finalize(SessionResult result, FailureReason reason);
    }
}
