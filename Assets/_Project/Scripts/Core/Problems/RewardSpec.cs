using System;
using MathHorde.Core.Resources;

namespace MathHorde.Core.Problems
{
    /// <summary>
    /// What the correct answer grants (contract core-domain.md 3.).
    /// RewardDelta = OperationResult − SnapshotValue for a
    /// ResourceOperationComparison gate (spec 5.1).
    /// </summary>
    public sealed class RewardSpec
    {
        public ResourceType Resource { get; }

        /// <summary>
        /// Fixed amount for an AnswerLaneSelection option (e.g. +15 health).
        /// For ResourceOperationComparison this is always 0 — there the
        /// delta is computed from the resolved gate operations, not fixed
        /// here (see UseSnapshotDelta).
        /// </summary>
        public int FlatAmount { get; }

        /// <summary>True ⇒ ResourceOperationComparison (the reward is the snapshot delta, not FlatAmount).</summary>
        public bool UseSnapshotDelta { get; }

        /// <summary>Default 0 (spec 9.: no extra penalty beyond the missed reward).</summary>
        public int IncorrectAmount { get; }

        public RewardSpec(ResourceType resource, int flatAmount, bool useSnapshotDelta, int incorrectAmount = 0)
        {
            if (useSnapshotDelta && flatAmount != 0)
            {
                throw new ArgumentException(
                    "UseSnapshotDelta == true esetén FlatAmount kötelezően 0 — a ResourceOperationComparison jutalma a snapshot-deltából számítódik, nem fix összeg.",
                    nameof(flatAmount));
            }

            Resource = resource;
            FlatAmount = flatAmount;
            UseSnapshotDelta = useSnapshotDelta;
            IncorrectAmount = incorrectAmount;
        }
    }
}
