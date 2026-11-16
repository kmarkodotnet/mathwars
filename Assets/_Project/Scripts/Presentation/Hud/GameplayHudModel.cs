// GameplayHudModel.cs
// F1-15 — the testable half of GameplayHUD: holds only display-ready values
// and localized strings, never computes rewards/clamps/game-business state
// itself (unity-ui skill: "a döntési logika a GameLogic rétegben marad").
// Deliberately free of MonoBehaviour/Canvas so NUnit can exercise it
// directly (see Tests/PlayMode/Hud/GameplayHudModelTests.cs — PlayMode
// because this type lives in the Presentation assembly, which
// MathHorde.Tests.EditMode.asmdef/the dotnet mirror do not reference).
//
// Resource numbers are re-read from IResourceService (GetCurrent/GetMax) on
// every ResourceChanged — the model never mutates a resource (ApplyDelta/
// TryConsume/SetMax/Initialize are forbidden from Presentation, contract
// game-logic.md 2.1) and never polls once per frame; it only re-reads in
// direct response to the event that says something changed.
//
// Every player-facing string comes from ILocalizationService.Resolve, never
// a Hungarian literal in this file (ADR-007).
using System;
using MathHorde.Core.Common;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;

namespace MathHorde.Presentation.Hud
{
    public sealed class GameplayHudModel
    {
        private readonly ILocalizationService _localization;

        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Ammo { get; private set; }
        public int MaxAmmo { get; private set; }
        public int Shield { get; private set; }
        public int MaxShield { get; private set; }
        public int WaveIndex { get; private set; }
        public int LevelNumber { get; private set; } = 1;

        /// <summary>
        /// Contract core-domain.md 2.2: MaxShield == 0 means the level has no
        /// shield mechanic at all (no pool was ever created) — the HUD hides
        /// the shield readout rather than showing a permanent "0/0".
        /// </summary>
        public bool HasShield => MaxShield > 0;

        public string HealthLabel => _localization.Resolve("hud.health");
        public string AmmoLabel => _localization.Resolve("hud.ammo");
        public string ShieldLabel => _localization.Resolve("hud.shield");
        public string PauseLabel => _localization.Resolve("hud.pause");
        public string WaveText => _localization.Resolve("hud.wave", WaveIndex);
        public string LevelText => _localization.Resolve("hud.level", LevelNumber);

        public string HealthValueText => FormatCurrentOverMax(Health, MaxHealth);
        public string AmmoValueText => FormatCurrentOverMax(Ammo, MaxAmmo);
        public string ShieldValueText => FormatCurrentOverMax(Shield, MaxShield);

        public GameplayHudModel(ILocalizationService localization)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>One-time read of the starting resource state at session start (not a per-frame poll).</summary>
        public void Initialize(IResourceService resourceService)
        {
            SyncResource(resourceService, ResourceType.Health);
            SyncResource(resourceService, ResourceType.Ammo);
            SyncResource(resourceService, ResourceType.Shield);
        }

        /// <summary>Called from the ResourceChanged event handler — re-reads only the changed resource's Current/Max.</summary>
        public void ApplyResourceChanged(IResourceService resourceService, ResourceType changedType)
        {
            SyncResource(resourceService, changedType);
        }

        public void ApplyWaveStarted(int waveIndex)
        {
            WaveIndex = waveIndex;
        }

        public void SetLevelNumber(int levelNumber)
        {
            LevelNumber = levelNumber;
        }

        private void SyncResource(IResourceService resourceService, ResourceType type)
        {
            int current = resourceService.GetCurrent(type);
            int max = resourceService.GetMax(type);

            switch (type)
            {
                case ResourceType.Health:
                    Health = current;
                    MaxHealth = max;
                    break;
                case ResourceType.Ammo:
                    Ammo = current;
                    MaxAmmo = max;
                    break;
                case ResourceType.Shield:
                    Shield = current;
                    MaxShield = max;
                    break;
            }
        }

        private static string FormatCurrentOverMax(int current, int max)
        {
            return current + "/" + max;
        }
    }
}
