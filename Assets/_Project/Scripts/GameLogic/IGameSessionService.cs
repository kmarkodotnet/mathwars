using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Progress;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 8. — the composition root. The ONLY place the
    /// Tick order (ARCHITECTURE 6.1) is implemented; Presentation's
    /// MonoBehaviour.Update calls this and nothing else.
    ///
    /// ADR-017 freezes the Phase-1 explicit dependency signature: the caller
    /// resolves the ordered weapon definitions and enemy catalog at startup.
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
            IReadOnlyList<WeaponDefinition> weapons,
            IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog,
            int seed);
        IReadOnlyList<WeaponDefinition> AvailableWeapons { get; }
        string CurrentWeaponId { get; }
        Result<bool> SelectWeapon(string weaponId);

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
