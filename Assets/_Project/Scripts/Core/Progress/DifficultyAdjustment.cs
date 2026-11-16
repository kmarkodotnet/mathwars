using System;
using System.Collections.Generic;

namespace MathHorde.Core.Progress
{
    /// <summary>
    /// One logged challenge-level change (contract core-domain.md 6., spec
    /// 15.). The Phase-4 adaptive-difficulty engine (IAdaptiveDifficultyService,
    /// F4-03) is the eventual writer of these; this task (save-data-engineer)
    /// only needs the type to exist so the persistence layer has something
    /// contract-shaped to serialize — see ADR-011.
    /// </summary>
    public sealed class DifficultyAdjustment
    {
        public int PreviousLevel { get; }
        public int NewLevel { get; }

        /// <summary>E.g. "LEVEL_PASSED_FIRST_TRY", "TWO_CONSECUTIVE_FAILURES" (spec 7.4).</summary>
        public string Reason { get; }

        public DateTimeOffset Timestamp { get; }
        public IReadOnlyDictionary<string, double> TriggerMetrics { get; }

        public DifficultyAdjustment(
            int previousLevel,
            int newLevel,
            string reason,
            DateTimeOffset timestamp,
            IReadOnlyDictionary<string, double> triggerMetrics)
        {
            if (previousLevel < 1 || previousLevel > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(previousLevel), previousLevel, "PreviousLevel 1..10 kötelező.");
            }

            if (newLevel < 1 || newLevel > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(newLevel), newLevel, "NewLevel 1..10 kötelező.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason kötelező.", nameof(reason));
            }

            PreviousLevel = previousLevel;
            NewLevel = newLevel;
            Reason = reason;
            Timestamp = timestamp;
            TriggerMetrics = triggerMetrics ?? new Dictionary<string, double>();
        }
    }
}
