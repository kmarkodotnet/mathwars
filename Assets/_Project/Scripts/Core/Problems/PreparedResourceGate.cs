using System;
using System.Collections.Generic;
using MathHorde.Core.Skills;

namespace MathHorde.Core.Problems
{
    /// <summary>Immutable preparation data. A null context tag list denotes an unsupported candidate/context pair.</summary>
    public sealed class PreparedResourceGate
    {
        private readonly IReadOnlyList<SkillTag>[][] _contextTags;
        public MathProblemId Id { get; }
        public int Seed { get; }
        public DifficultyProfile Difficulty { get; }
        public float ThinkTimeSeconds { get; }
        public RewardSpec Reward { get; }
        public GateGoal Goal { get; }
        public int? TargetValue { get; }
        public int PreparedCapacity { get; }
        public IReadOnlyList<ResourceGateTemplate> Candidates { get; }

        public PreparedResourceGate(MathProblemId id, int seed, DifficultyProfile difficulty, float thinkTimeSeconds,
            RewardSpec reward, GateGoal goal, int? targetValue, int preparedCapacity,
            IReadOnlyList<ResourceGateTemplate> candidates, IReadOnlyList<IReadOnlyList<SkillTag>[]> contextTags)
        {
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            Reward = reward ?? throw new ArgumentNullException(nameof(reward));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (contextTags == null) throw new ArgumentNullException(nameof(contextTags));
            if (preparedCapacity <= 0 || preparedCapacity == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(preparedCapacity));
            if (!(thinkTimeSeconds > 0f) || float.IsInfinity(thinkTimeSeconds)) throw new ArgumentOutOfRangeException(nameof(thinkTimeSeconds));
            if (contextTags.Count != candidates.Count) throw new ArgumentException("Candidate metadata count differs.", nameof(contextTags));
            Id = id; Seed = seed; ThinkTimeSeconds = thinkTimeSeconds; Goal = goal; TargetValue = targetValue;
            PreparedCapacity = preparedCapacity;
            Candidates = new List<ResourceGateTemplate>(candidates).AsReadOnly();
            _contextTags = new IReadOnlyList<SkillTag>[candidates.Count][];
            for (int c = 0; c < candidates.Count; c++)
            {
                if (candidates[c] == null || contextTags[c] == null || contextTags[c].Length != preparedCapacity + 1)
                    throw new ArgumentException("Incomplete candidate metadata.", nameof(contextTags));
                _contextTags[c] = new IReadOnlyList<SkillTag>[preparedCapacity + 1];
                for (int s = 0; s <= preparedCapacity; s++)
                    if (contextTags[c][s] != null) _contextTags[c][s] = new List<SkillTag>(contextTags[c][s]).AsReadOnly();
            }
        }

        public IReadOnlyList<SkillTag> GetSkillTags(int candidateIndex, int snapshotValue)
        {
            if (candidateIndex < 0 || candidateIndex >= _contextTags.Length) throw new ArgumentOutOfRangeException(nameof(candidateIndex));
            if (snapshotValue < 0 || snapshotValue > PreparedCapacity) throw new ArgumentOutOfRangeException(nameof(snapshotValue));
            return _contextTags[candidateIndex][snapshotValue];
        }
    }
}
