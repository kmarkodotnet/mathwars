using System;
using System.Collections.Generic;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// A local player's top-level progress record (contract core-domain.md
    /// 6., spec 15.). Immutable — SkillStatistics and DifficultyAdjustment
    /// history are stored alongside it by the Infrastructure persistence
    /// layer (ADR-011), not as fields on this type, so this class stays
    /// contract-identical to core-domain.md 6.'s PlayerProfile block.
    /// </summary>
    public sealed class PlayerProfile
    {
        public PlayerProfileId Id { get; }

        /// <summary>Null ⇒ anonymous local profile (spec 15.: "DisplayName vagy névtelen helyi profil").</summary>
        public string DisplayName { get; }

        public DateTimeOffset CreatedAt { get; }
        public int CurrentChallengeLevel { get; }
        public int HighestChallengeLevel { get; }
        public IReadOnlyCollection<WorldId> UnlockedWorlds { get; }
        public IReadOnlyCollection<LevelId> CompletedLevels { get; }

        public PlayerProfile(
            PlayerProfileId id,
            string displayName,
            DateTimeOffset createdAt,
            int currentChallengeLevel,
            int highestChallengeLevel,
            IReadOnlyCollection<WorldId> unlockedWorlds,
            IReadOnlyCollection<LevelId> completedLevels)
        {
            if (currentChallengeLevel < 1 || currentChallengeLevel > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(currentChallengeLevel), currentChallengeLevel, "CurrentChallengeLevel 1..10 kötelező.");
            }

            if (highestChallengeLevel < 1 || highestChallengeLevel > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(highestChallengeLevel), highestChallengeLevel, "HighestChallengeLevel 1..10 kötelező.");
            }

            if (highestChallengeLevel < currentChallengeLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(highestChallengeLevel), highestChallengeLevel, "HighestChallengeLevel nem lehet kisebb, mint CurrentChallengeLevel.");
            }

            Id = id;
            DisplayName = displayName;
            CreatedAt = createdAt;
            CurrentChallengeLevel = currentChallengeLevel;
            HighestChallengeLevel = highestChallengeLevel;
            UnlockedWorlds = unlockedWorlds ?? Array.Empty<WorldId>();
            CompletedLevels = completedLevels ?? Array.Empty<LevelId>();
        }
    }
}
