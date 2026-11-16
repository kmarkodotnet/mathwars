using System.Collections.Generic;
using MathHorde.Core.Progress;

namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// Persists the append-only log of played GameSessionRecords (each
    /// carrying its QuestionAttempt list) — contract core-domain.md 6.,
    /// spec 15., ADR-011 1. Separate file from ILocalSaveRepository so a
    /// per-level append never rewrites the whole profile document.
    /// </summary>
    public interface ILocalStatisticsRepository
    {
        /// <summary>Never throws. Returns an empty list if no file exists yet or it is corrupt/unreadable.</summary>
        IReadOnlyList<GameSessionRecord> LoadAll();

        /// <summary>Appends one session to the log and atomically rewrites the whole file (the log is expected to stay small enough for this — see ADR-011 for the size rationale).</summary>
        void Append(GameSessionRecord session);

        /// <summary>Irreversibly deletes the statistics file. Missing file is a no-op, not an error.</summary>
        void Delete();
    }
}
