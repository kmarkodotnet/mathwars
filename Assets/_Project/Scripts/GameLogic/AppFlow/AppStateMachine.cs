using System;
using System.Collections.Generic;

namespace MathHorde.GameLogic.AppFlow
{
    /// <summary>
    /// Default IAppStateMachine. Table-driven transcription of the
    /// ARCHITECTURE.md 4. mermaid diagram — every edge in that diagram is
    /// one entry in _transitions below, keyed by (fromState, trigger). Boot
    /// is the one special case: it needs BOTH ConfigLoaded AND ProfileLoaded
    /// (in either order) before advancing to MainMenu, so it cannot be a
    /// single table lookup like every other state.
    /// </summary>
    public sealed class AppStateMachine : IAppStateMachine
    {
        private static readonly Dictionary<(AppState From, AppTrigger Trigger), AppState> Transitions =
            new Dictionary<(AppState From, AppTrigger Trigger), AppState>
            {
                [(AppState.MainMenu, AppTrigger.PlayPressed)] = AppState.WorldSelection,
                [(AppState.MainMenu, AppTrigger.SettingsPressed)] = AppState.Settings,
                [(AppState.MainMenu, AppTrigger.ParentPinAccepted)] = AppState.ParentDashboard,

                [(AppState.WorldSelection, AppTrigger.LevelSelected)] = AppState.Gameplay,
                [(AppState.WorldSelection, AppTrigger.BackPressed)] = AppState.MainMenu,

                [(AppState.Gameplay, AppTrigger.PausePressed)] = AppState.Pause,
                [(AppState.Gameplay, AppTrigger.AppFocusLost)] = AppState.Pause,
                [(AppState.Gameplay, AppTrigger.VictoryConditionMet)] = AppState.LevelComplete,
                [(AppState.Gameplay, AppTrigger.PlayerHealthZero)] = AppState.GameOver,

                [(AppState.Pause, AppTrigger.ResumePressed)] = AppState.Gameplay,
                [(AppState.Pause, AppTrigger.QuitToMenu)] = AppState.MainMenu,

                [(AppState.LevelComplete, AppTrigger.Continue)] = AppState.WorldSelection,
                [(AppState.LevelComplete, AppTrigger.NextLevel)] = AppState.Gameplay,

                [(AppState.GameOver, AppTrigger.Retry)] = AppState.Gameplay,
                [(AppState.GameOver, AppTrigger.BackToWorlds)] = AppState.WorldSelection,

                [(AppState.ParentDashboard, AppTrigger.BackPressed)] = AppState.MainMenu,
                [(AppState.ParentDashboard, AppTrigger.ParentSettingsPressed)] = AppState.Settings,

                [(AppState.Settings, AppTrigger.BackPressed)] = AppState.MainMenu,
            };

        private bool _configLoaded;
        private bool _profileLoaded;

        public AppState Current { get; private set; } = AppState.Boot;

        public event Action<AppStateTransition> Transitioned;

        public bool CanFire(AppTrigger trigger)
        {
            if (Current == AppState.Boot)
            {
                return trigger == AppTrigger.ConfigLoaded || trigger == AppTrigger.ProfileLoaded;
            }

            return Transitions.ContainsKey((Current, trigger));
        }

        public bool Fire(AppTrigger trigger)
        {
            if (Current == AppState.Boot)
            {
                return FireBootTrigger(trigger);
            }

            if (!Transitions.TryGetValue((Current, trigger), out AppState next))
            {
                return false;
            }

            TransitionTo(next, trigger);
            return true;
        }

        private bool FireBootTrigger(AppTrigger trigger)
        {
            switch (trigger)
            {
                case AppTrigger.ConfigLoaded:
                    _configLoaded = true;
                    break;
                case AppTrigger.ProfileLoaded:
                    _profileLoaded = true;
                    break;
                default:
                    // Boot accepts nothing else (contract: "Boot kötelezően
                    // sikeresen fut sérült mentés esetén is" — but a corrupt
                    // save still raises ProfileLoaded with a default
                    // profile; it never skips straight to another trigger).
                    return false;
            }

            if (_configLoaded && _profileLoaded)
            {
                TransitionTo(AppState.MainMenu, trigger);
            }

            return true;
        }

        private void TransitionTo(AppState next, AppTrigger trigger)
        {
            var transition = new AppStateTransition(Current, next, trigger);
            Current = next;
            Transitioned?.Invoke(transition);
        }
    }
}
