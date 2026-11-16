namespace MathHorde.GameLogic.AppFlow
{
    /// <summary>
    /// Every edge label in the ARCHITECTURE.md 4. state diagram. "Minden
    /// átmenet explicit AppTrigger eseményre történik" — the SceneFlowManager
    /// (Infrastructure) and UI presenters fire these; they never set
    /// AppState directly.
    /// </summary>
    public enum AppTrigger
    {
        /// <summary>Boot-only: GameConfigurationRepository finished loading.</summary>
        ConfigLoaded = 0,

        /// <summary>Boot-only: ISaveRepository.LoadProgress finished (a corrupt save still raises this, with a fresh default profile — ARCHITECTURE.md 4.).</summary>
        ProfileLoaded = 1,

        PlayPressed = 2,
        SettingsPressed = 3,
        ParentPinAccepted = 4,
        LevelSelected = 5,
        BackPressed = 6,
        PausePressed = 7,

        /// <summary>Android OnApplicationPause(true), translated by SceneFlowManager.</summary>
        AppFocusLost = 8,

        ResumePressed = 9,
        QuitToMenu = 10,
        VictoryConditionMet = 11,
        PlayerHealthZero = 12,
        Continue = 13,
        NextLevel = 14,
        Retry = 15,
        BackToWorlds = 16,
        ParentSettingsPressed = 17
    }
}
