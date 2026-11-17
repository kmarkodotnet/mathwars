// LevelResultModel.cs
// F1-16 — the testable half of LevelResultScreen (math-horde.md 12./7.1:
// "a rossz eredmény soha nem lehet megalázó" — the defeat screen never
// frames the outcome as "kudarc"/shame, only an encouraging, Hungarian,
// key-based message). Pure mapping from AppState -> display content; it
// decides NEITHER whether the session ended (that is the GameLogic state
// machine's job) NOR what the actual AppTrigger does (that is
// IAppStateMachine.Fire) — only which localized text and which already-
// defined trigger each of the two buttons should show/fire.
//
// Deliberately does not read GameSessionRecord/SessionResult/FailureReason/
// LossDiagnosis (core-domain.md 6., game-logic.md 8.): this is the Fázis 1
// MINIMAL version of the result screen (feladat szöveg), and no composition
// root calls IGameSessionService.Finalize() on the Transitioned event yet
// (flagged in F1-11's Napló as F1-17's job) — adding a stats readout now
// would either show meaningless zeros (MathEngine/Fázis 2 not integrated
// yet) or require this task to invent that wiring itself, which is out of
// scope for F1-16. See the F1-16 Napló for this scope note.
//
// Lives in the Presentation assembly (like GameplayHudModel) even though it
// has zero UnityEngine dependency — see unity-csharp skill "Buktatók
// (Presentation-logika az EditMode gyors kapuba, F1-13)": the FILE'S
// ASSEMBLY decides test placement, not whether the code happens to touch
// UnityEngine. Tests therefore live under Tests/PlayMode (see
// LevelResultModelTests.cs), not Tests/EditMode.
using System;
using MathHorde.Core.Common;
using MathHorde.GameLogic.AppFlow;

namespace MathHorde.Presentation.Result
{
    /// <summary>
    /// Everything LevelResultScreen needs to render for one end-of-level
    /// AppState: a non-shaming headline + encouragement, and two buttons
    /// each paired with the exact AppTrigger that is valid from that state
    /// (per AppStateMachine's transition table) — so a rendered button is
    /// never decorative/non-functional (spec 22.: "ne hozz létre pusztán
    /// dekoratív, működésképtelen gombot").
    /// </summary>
    public readonly struct LevelResultContent
    {
        public string Headline { get; }
        public string Encouragement { get; }
        public string PrimaryLabel { get; }
        public AppTrigger PrimaryTrigger { get; }
        public string SecondaryLabel { get; }
        public AppTrigger SecondaryTrigger { get; }

        public LevelResultContent(
            string headline,
            string encouragement,
            string primaryLabel,
            AppTrigger primaryTrigger,
            string secondaryLabel,
            AppTrigger secondaryTrigger)
        {
            Headline = headline;
            Encouragement = encouragement;
            PrimaryLabel = primaryLabel;
            PrimaryTrigger = primaryTrigger;
            SecondaryLabel = secondaryLabel;
            SecondaryTrigger = secondaryTrigger;
        }
    }

    public sealed class LevelResultModel
    {
        private readonly ILocalizationService _localization;

        public LevelResultModel(ILocalizationService localization)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>
        /// Builds the display content for <paramref name="state"/>. Returns
        /// false (content = default) for every AppState that is not an
        /// end-of-level state — the screen must stay hidden for those, it
        /// never guesses content for a state it was not built for.
        /// </summary>
        public bool TryBuild(AppState state, out LevelResultContent content)
        {
            switch (state)
            {
                case AppState.GameOver:
                    content = new LevelResultContent(
                        headline: _localization.Resolve("result.gameOver"),
                        encouragement: _localization.Resolve("result.gameOverEncouragement"),
                        primaryLabel: _localization.Resolve("result.retry"),
                        primaryTrigger: AppTrigger.Retry,
                        secondaryLabel: _localization.Resolve("result.backToWorlds"),
                        secondaryTrigger: AppTrigger.BackToWorlds);
                    return true;

                case AppState.LevelComplete:
                    content = new LevelResultContent(
                        headline: _localization.Resolve("result.victory"),
                        encouragement: _localization.Resolve("result.victoryEncouragement"),
                        primaryLabel: _localization.Resolve("result.nextLevel"),
                        primaryTrigger: AppTrigger.NextLevel,
                        secondaryLabel: _localization.Resolve("result.backToWorlds"),
                        secondaryTrigger: AppTrigger.Continue);
                    return true;

                default:
                    content = default;
                    return false;
            }
        }
    }
}
