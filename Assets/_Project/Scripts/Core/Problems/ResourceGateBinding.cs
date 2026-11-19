using System;
using System.Collections.Generic;

namespace MathHorde.Core.Problems
{
    public readonly struct ResourceGateBinding
    {
        private readonly EvaluationResult _value0, _value1, _value2, _value3;
        public PreparedResourceGate Prepared { get; }
        public int CandidateIndex { get; }
        public ResourceGateTemplate Template => Prepared.Candidates[CandidateIndex];
        public MathProblemId ProblemId => Prepared.Id;
        public int SnapshotValue { get; }
        public int ResourceCapacity { get; }
        public int OptionCount => Template.Options.Count;
        public int CorrectOptionIndex { get; }
        public IReadOnlyList<SkillTag> SkillTags => Prepared.GetSkillTags(CandidateIndex, SnapshotValue);

        public ResourceGateBinding(PreparedResourceGate prepared, int candidateIndex, int snapshotValue,
            int resourceCapacity, int correctOptionIndex, EvaluationResult value0, EvaluationResult value1,
            EvaluationResult value2 = default, EvaluationResult value3 = default)
        {
            Prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
            CandidateIndex = candidateIndex; SnapshotValue = snapshotValue; ResourceCapacity = resourceCapacity;
            CorrectOptionIndex = correctOptionIndex;
            _value0 = value0; _value1 = value1; _value2 = value2; _value3 = value3;
        }

        public EvaluationResult GetValue(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex >= OptionCount) throw new ArgumentOutOfRangeException(nameof(optionIndex));
            switch (optionIndex)
            {
                case 0: return _value0;
                case 1: return _value1;
                case 2: return _value2;
                default: return _value3;
            }
        }
    }
}
