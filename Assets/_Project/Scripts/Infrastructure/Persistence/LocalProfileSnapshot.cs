using System.Collections.Generic;
using MathHorde.Core.Problems;
using MathHorde.Core.Progress;
using MathHorde.Core.Skills;

namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// Everything ILocalSaveRepository reads/writes as one atomic unit
    /// (ADR-011 1.): the PlayerProfile, the SkillStatistics dictionary, and
    /// the DifficultyAdjustment history. Bundled together because a single
    /// level completion can change all three at once and they are meant to
    /// be saved in one atomic write, not as three independently-committed
    /// files.
    /// </summary>
    public sealed class LocalProfileSnapshot
    {
        public PlayerProfile Profile { get; }
        public IReadOnlyDictionary<SkillTag, SkillStatistics> Skills { get; }
        public IReadOnlyList<DifficultyAdjustment> DifficultyHistory { get; }

        public LocalProfileSnapshot(
            PlayerProfile profile,
            IReadOnlyDictionary<SkillTag, SkillStatistics> skills,
            IReadOnlyList<DifficultyAdjustment> difficultyHistory)
        {
            Profile = profile;
            Skills = skills ?? new Dictionary<SkillTag, SkillStatistics>();
            DifficultyHistory = difficultyHistory ?? System.Array.Empty<DifficultyAdjustment>();
        }
    }
}
