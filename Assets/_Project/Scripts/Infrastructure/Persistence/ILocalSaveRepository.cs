namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// Persists the player's profile core: PlayerProfile + SkillStatistics +
    /// DifficultyAdjustment history (contract core-domain.md 6./4., spec
    /// 15., ADR-011). Implementations must never throw out of Load() — a
    /// missing or corrupt file must log and fall back to a fresh profile
    /// (local-persistence skill).
    /// </summary>
    public interface ILocalSaveRepository
    {
        /// <summary>Never throws. Returns a fresh, default snapshot if no save exists yet or the file is corrupt/unreadable.</summary>
        LocalProfileSnapshot Load();

        /// <summary>Atomic write (temp file + replace) — never leaves a half-written file on disk.</summary>
        void Save(LocalProfileSnapshot snapshot);

        /// <summary>Irreversibly deletes the profile save file. Missing file is a no-op, not an error.</summary>
        void Delete();
    }
}
