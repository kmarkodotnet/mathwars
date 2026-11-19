// MathDifficultyTableSO.cs
// F2-10 — contract scriptable-object-schema.md 5. (MathDifficultyTableSO +
// MathDifficultyLevelEntry), spec 7.2 ("A táblázat konfigurálható legyen, ne
// kerüljön beégetésre a kódba"), balance/difficulty-model.md 2. and 3.
//
// The ten rows are populated as FIELD DEFAULTS (the contract's "Default
// (szint 1 -> 10)" column) so that a freshly created asset — and a
// ScriptableObject.CreateInstance in a test — already carries the designed,
// valid table instead of an empty list. Every value stays editable in the
// Inspector: the asset is the source of truth at runtime, the defaults are
// only the designed starting point.
//
// Scope note: contract 5.1 requires that BaseThinkTimeSeconds mirrors
// TimePressureTableSO.ThinkTimeSeconds and that a mismatch is an error. That
// cross-asset check cannot live here yet — TimePressureTableSO does not exist
// in code (a separate, later task owns it) — and inventing it here would mean
// creating that asset type outside this task's brief. The mirrored numbers are
// identical to difficulty-model.md 3. today, and the cross-check belongs to
// whichever task introduces TimePressureTableSO.
using System;
using System.Collections.Generic;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.MathEngine;
using UnityEngine;

namespace MathHorde.Infrastructure.Configuration
{
    [CreateAssetMenu(fileName = "MathDifficultyTable", menuName = "MathHorde/Math Difficulty Table")]
    public sealed class MathDifficultyTableSO : ScriptableObject, IValidatableConfig
    {
        /// <summary>The table has exactly this many rows (contract 5.: "Kötelezően 10 sor").</summary>
        public const int RequiredLevelCount = 10;

        [System.Serializable]
        public sealed class MathDifficultyLevelEntry
        {
            [Range(1, 10)]
            [SerializeField] private int _level = 1;

            [Tooltip("String-tábla kulcs (ADR-007) — magyar szöveg SOHA nem kerül assetbe.")]
            [SerializeField] private string _descriptionKey = "diff.l1";

            [SerializeField] private List<BinaryOperator> _allowedOperators = new List<BinaryOperator>();

            [Range(0, 999)]
            [SerializeField] private int _minOperandValue = 1;

            [Range(1, 999)]
            [SerializeField] private int _maxOperandValue = 10;

            [Range(1, 9999)]
            [SerializeField] private int _maxResultValue = 10;

            [Range(2, 5)]
            [SerializeField] private int _minOperandCount = 2;

            [Range(2, 5)]
            [SerializeField] private int _maxOperandCount = 2;

            [SerializeField] private bool _allowParentheses;

            [Range(0, 2)]
            [SerializeField] private int _maxParenthesisDepth;

            [SerializeField] private bool _allowUnknownOperand;

            [Range(0, 1)]
            [SerializeField] private int _unknownOperandCount;

            [SerializeField] private UnknownPlacement _unknownPlacement = UnknownPlacement.None;

            [SerializeField] private bool _allowNegativeResults;

            [Range(2, 4)]
            [SerializeField] private int _answerOptionCount = 2;

            [Range(2, 4)]
            [SerializeField] private int _laneCount = 2;

            [Tooltip("A két kapueredmény minimális eltérése (I10): CL1–3: 8, CL4+: 4.")]
            [Range(1, 50)]
            [SerializeField] private int _minGateGap = 8;

            [Range(2.0f, 30.0f)]
            [SerializeField] private float _baseThinkTimeSeconds = 14.0f;

            [Tooltip("A szint által bevezetett készségek (difficulty-model.md 2.1).")]
            [SerializeField] private List<SkillTag> _requiredSkills = new List<SkillTag>();

            [Range(1, 10)]
            [SerializeField] private int _mathComplexity = 1;

            [Range(1, 10)]
            [SerializeField] private int _timePressure = 1;

            [Range(1, 10)]
            [SerializeField] private int _combatDifficulty = 1;

            public int Level => _level;
            public string DescriptionKey => _descriptionKey;
            public IReadOnlyList<BinaryOperator> AllowedOperators => _allowedOperators;
            public int MinOperandValue => _minOperandValue;
            public int MaxOperandValue => _maxOperandValue;
            public int MaxResultValue => _maxResultValue;
            public int MinOperandCount => _minOperandCount;
            public int MaxOperandCount => _maxOperandCount;
            public bool AllowParentheses => _allowParentheses;
            public int MaxParenthesisDepth => _maxParenthesisDepth;
            public bool AllowUnknownOperand => _allowUnknownOperand;
            public int UnknownOperandCount => _unknownOperandCount;

            /// <summary>
            /// Where the UnknownNode may land. NOT carried by
            /// Core.Skills.DifficultyProfile — the enum lives in the MathEngine
            /// layer, which Core must not reference (see DifficultyProfile's
            /// CONTRACT NOTE 2); read it from this entry.
            /// </summary>
            public UnknownPlacement UnknownPlacement => _unknownPlacement;

            public bool AllowNegativeResults => _allowNegativeResults;
            public int AnswerOptionCount => _answerOptionCount;
            public int LaneCount => _laneCount;
            public int MinGateGap => _minGateGap;
            public float BaseThinkTimeSeconds => _baseThinkTimeSeconds;
            public IReadOnlyList<SkillTag> RequiredSkills => _requiredSkills;
            public int MathComplexity => _mathComplexity;
            public int TimePressure => _timePressure;
            public int CombatDifficulty => _combatDifficulty;

            /// <summary>
            /// Builds one designed row. Only the table's own default factory
            /// uses this — at runtime the serialized asset values win.
            /// </summary>
            internal static MathDifficultyLevelEntry Create(
                int level,
                IEnumerable<BinaryOperator> allowedOperators,
                int minOperandValue,
                int maxOperandValue,
                int maxResultValue,
                int minOperandCount,
                int maxOperandCount,
                int maxParenthesisDepth,
                int unknownOperandCount,
                UnknownPlacement unknownPlacement,
                int answerOptionCount,
                int minGateGap,
                float baseThinkTimeSeconds,
                IEnumerable<SkillTag> requiredSkills)
            {
                return new MathDifficultyLevelEntry
                {
                    _level = level,
                    _descriptionKey = $"diff.l{level}",
                    _allowedOperators = new List<BinaryOperator>(allowedOperators),
                    _minOperandValue = minOperandValue,
                    _maxOperandValue = maxOperandValue,
                    _maxResultValue = maxResultValue,
                    _minOperandCount = minOperandCount,
                    _maxOperandCount = maxOperandCount,
                    _allowParentheses = maxParenthesisDepth > 0,
                    _maxParenthesisDepth = maxParenthesisDepth,
                    _allowUnknownOperand = unknownOperandCount > 0,
                    _unknownOperandCount = unknownOperandCount,
                    _unknownPlacement = unknownPlacement,
                    _allowNegativeResults = false,
                    _answerOptionCount = answerOptionCount,
                    _laneCount = answerOptionCount,
                    _minGateGap = minGateGap,
                    _baseThinkTimeSeconds = baseThinkTimeSeconds,
                    _requiredSkills = new List<SkillTag>(requiredSkills),
                    _mathComplexity = level,
                    _timePressure = level,
                    _combatDifficulty = level
                };
            }

            public void CollectErrors(List<string> errors, string pathPrefix)
            {
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.Level", _level, 1, 10);
                ConfigValidation.RequireNonEmpty(errors, $"{pathPrefix}.DescriptionKey", _descriptionKey);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.MinOperandValue", _minOperandValue, 0, 999);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.MaxOperandValue", _maxOperandValue, 1, 999);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.MaxResultValue", _maxResultValue, 1, 9999);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.MinOperandCount", _minOperandCount, 2, 5);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.MaxOperandCount", _maxOperandCount, 2, 5);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.MaxParenthesisDepth", _maxParenthesisDepth, 0, 2);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.UnknownOperandCount", _unknownOperandCount, 0, 1);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.AnswerOptionCount", _answerOptionCount, 2, 4);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.LaneCount", _laneCount, 2, 4);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.MinGateGap", _minGateGap, 1, 50);
                ConfigValidation.RequireRange(
                    errors, $"{pathPrefix}.BaseThinkTimeSeconds", _baseThinkTimeSeconds, 2.0f, 30.0f);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.MathComplexity", _mathComplexity, 1, 10);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.TimePressure", _timePressure, 1, 10);
                ConfigValidation.RequireRange(errors, $"{pathPrefix}.CombatDifficulty", _combatDifficulty, 1, 10);

                if (_allowedOperators == null || _allowedOperators.Count == 0)
                {
                    errors.Add($"{pathPrefix}.AllowedOperators nem lehet üres.");
                }
                else
                {
                    foreach (BinaryOperator op in _allowedOperators)
                    {
                        if (!Enum.IsDefined(typeof(BinaryOperator), op))
                        {
                            errors.Add($"{pathPrefix}.AllowedOperators ismeretlen operátort tartalmaz: {op}.");
                        }
                    }
                }

                if (_requiredSkills == null)
                {
                    errors.Add($"{pathPrefix}.RequiredSkills nem lehet null.");
                }
                else
                {
                    foreach (SkillTag skill in _requiredSkills)
                    {
                        if (!Enum.IsDefined(typeof(SkillTag), skill))
                        {
                            errors.Add($"{pathPrefix}.RequiredSkills ismeretlen készséget tartalmaz: {skill}.");
                        }
                    }
                }

                if (!Enum.IsDefined(typeof(UnknownPlacement), _unknownPlacement))
                {
                    errors.Add($"{pathPrefix}.UnknownPlacement ismeretlen érték: {_unknownPlacement}.");
                }

                if (float.IsNaN(_baseThinkTimeSeconds))
                {
                    errors.Add($"{pathPrefix}.BaseThinkTimeSeconds véges szám kell legyen.");
                }

                if (_maxOperandValue <= _minOperandValue)
                {
                    errors.Add($"{pathPrefix}: MaxOperandValue > MinOperandValue kötelező.");
                }

                if (_maxOperandCount < _minOperandCount)
                {
                    errors.Add($"{pathPrefix}: MaxOperandCount >= MinOperandCount kötelező.");
                }

                if (_allowParentheses != _maxParenthesisDepth > 0)
                {
                    errors.Add(
                        $"{pathPrefix}: AllowParentheses == (MaxParenthesisDepth > 0) invariáns sérül (core-domain.md 4.).");
                }

                if (_allowUnknownOperand != _unknownOperandCount > 0)
                {
                    errors.Add(
                        $"{pathPrefix}: AllowUnknownOperand == (UnknownOperandCount > 0) invariáns sérül (core-domain.md 4.).");
                }

                if (_allowUnknownOperand
                    && _allowedOperators != null
                    && !_allowedOperators.Contains(BinaryOperator.Addition)
                    && !_allowedOperators.Contains(BinaryOperator.Multiplication))
                {
                    errors.Add(
                        $"{pathPrefix}: AllowUnknownOperand == true esetén az AllowedOperators tartalmazzon Addition-t vagy Multiplication-t.");
                }

                // Generator precondition (ExpressionGenerator: "IncludeUnknown == true
                // esetén UnknownPlacement nem lehet None") — an asset that violates it
                // would throw at generation time instead of failing validation here.
                if (_allowUnknownOperand && _unknownPlacement == UnknownPlacement.None)
                {
                    errors.Add($"{pathPrefix}: AllowUnknownOperand == true esetén UnknownPlacement nem lehet None.");
                }

                if (!_allowUnknownOperand && _unknownPlacement != UnknownPlacement.None)
                {
                    errors.Add($"{pathPrefix}: AllowUnknownOperand == false esetén UnknownPlacement None kell legyen.");
                }

                if (_laneCount < _answerOptionCount)
                {
                    errors.Add($"{pathPrefix}: LaneCount >= AnswerOptionCount kötelező.");
                }
            }
        }

        [Tooltip("Pontosan 10 sor, szigorúan monoton Level szerint (spec 7.2).")]
        [SerializeField] private List<MathDifficultyLevelEntry> _levels = CreateDefaultLevels();

        public IReadOnlyList<MathDifficultyLevelEntry> Levels => _levels;

        /// <summary>
        /// The row for a 1..10 challenge level, without clamping. The repository
        /// validates the entire table before translating any rows.
        /// </summary>
        public MathDifficultyLevelEntry GetLevel(int challengeLevel)
        {
            if (_levels == null)
            {
                throw new ConfigurationValidationException(
                    new[] { $"{name}: a Levels lista null." });
            }

            foreach (MathDifficultyLevelEntry entry in _levels)
            {
                if (entry != null && entry.Level == challengeLevel)
                {
                    return entry;
                }
            }

            throw new KeyNotFoundException(
                $"MathDifficultyTableSO '{name}' has no row for challenge level {challengeLevel}.");
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            if (_levels == null)
            {
                errors.Add("Levels nem lehet null.");
                return errors;
            }

            ConfigValidation.RequireCount(errors, nameof(Levels), _levels.Count, RequiredLevelCount, RequiredLevelCount);

            for (int i = 0; i < _levels.Count; i++)
            {
                MathDifficultyLevelEntry entry = _levels[i];
                if (entry == null)
                {
                    errors.Add($"Levels[{i}] nem lehet null.");
                    continue;
                }

                entry.CollectErrors(errors, $"Levels[{i}]");

                if (i > 0 && _levels[i - 1] != null)
                {
                    if (entry.Level <= _levels[i - 1].Level)
                    {
                        errors.Add($"Levels[{i}]: a Level szigorúan monoton növő kell legyen.");
                    }

                    if (entry.BaseThinkTimeSeconds >= _levels[i - 1].BaseThinkTimeSeconds)
                    {
                        errors.Add(
                            $"Levels[{i}]: a BaseThinkTimeSeconds szigorúan monoton csökkenő kell legyen a Level szerint (difficulty-model.md 3.).");
                    }
                }
            }

            return errors;
        }

        private void OnValidate()
        {
            foreach (string error in Validate())
            {
                Debug.LogError($"{name} ({nameof(MathDifficultyTableSO)}): {error}", this);
            }
        }

        /// <summary>
        /// The designed default table (contract 5. "Default" column, values and
        /// rationale: balance/difficulty-model.md 2., 2.1 and 3.).
        /// </summary>
        private static List<MathDifficultyLevelEntry> CreateDefaultLevels()
        {
            BinaryOperator[] addSub = { BinaryOperator.Addition, BinaryOperator.Subtraction };
            BinaryOperator[] fourOps =
            {
                BinaryOperator.Addition, BinaryOperator.Subtraction,
                BinaryOperator.Multiplication, BinaryOperator.Division
            };
            BinaryOperator[] allOps =
            {
                BinaryOperator.Addition, BinaryOperator.Subtraction,
                BinaryOperator.Multiplication, BinaryOperator.Division,
                BinaryOperator.Modulo, BinaryOperator.IntegerDivisionWithRemainder
            };

            return new List<MathDifficultyLevelEntry>
            {
                MathDifficultyLevelEntry.Create(
                    1, addSub, 1, 10, 10, 2, 2, 0, 0, UnknownPlacement.None, 2, 8, 14.0f,
                    new[]
                    {
                        SkillTag.Addition, SkillTag.Subtraction, SkillTag.SingleDigitAddition,
                        SkillTag.ResourceComparison
                    }),
                MathDifficultyLevelEntry.Create(
                    2, addSub, 1, 20, 20, 2, 2, 0, 0, UnknownPlacement.None, 2, 8, 13.0f,
                    new[]
                    {
                        SkillTag.TwoDigitAddition, SkillTag.CrossingTen, SkillTag.SubtractionWithBorrowing
                    }),
                MathDifficultyLevelEntry.Create(
                    3, fourOps, 1, 20, 100, 2, 2, 0, 0, UnknownPlacement.None, 3, 8, 11.5f,
                    new[]
                    {
                        SkillTag.Multiplication, SkillTag.Division, SkillTag.TimesTable2,
                        SkillTag.TimesTable3, SkillTag.TimesTable5, SkillTag.TimesTable10
                    }),
                MathDifficultyLevelEntry.Create(
                    4, fourOps, 1, 50, 100, 2, 2, 0, 0, UnknownPlacement.None, 3, 4, 10.0f,
                    new[]
                    {
                        SkillTag.TimesTable4, SkillTag.TimesTable6, SkillTag.TimesTable7,
                        SkillTag.TimesTable8, SkillTag.TimesTable9
                    }),
                MathDifficultyLevelEntry.Create(
                    5, allOps, 1, 100, 100, 2, 2, 0, 0, UnknownPlacement.None, 3, 4, 8.0f,
                    new[] { SkillTag.Remainder, SkillTag.DivisionWithRemainder }),
                MathDifficultyLevelEntry.Create(
                    6, allOps, 1, 100, 200, 2, 3, 0, 0, UnknownPlacement.None, 4, 4, 7.5f,
                    new[] { SkillTag.TwoDigitMultiplication }),
                MathDifficultyLevelEntry.Create(
                    7, allOps, 1, 50, 200, 3, 3, 1, 0, UnknownPlacement.None, 4, 4, 7.0f,
                    new[]
                    {
                        SkillTag.OperatorPrecedence, SkillTag.Parentheses, SkillTag.MultiStepExpressions
                    }),
                MathDifficultyLevelEntry.Create(
                    8, allOps, 1, 60, 200, 3, 3, 1, 1, UnknownPlacement.RightOperand, 4, 4, 6.5f,
                    new[] { SkillTag.MissingOperand }),
                MathDifficultyLevelEntry.Create(
                    9, allOps, 1, 99, 500, 3, 4, 1, 1, UnknownPlacement.RightOperand, 4, 4, 5.5f,
                    new[] { SkillTag.TwoDigitMultiplication }),
                MathDifficultyLevelEntry.Create(
                    10, allOps, 1, 99, 999, 4, 5, 2, 1, UnknownPlacement.EitherSide, 4, 4, 4.5f,
                    new[] { SkillTag.MultiStepExpressions })
            };
        }
    }
}
