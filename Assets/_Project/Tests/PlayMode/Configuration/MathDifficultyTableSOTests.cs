// MathDifficultyTableSOTests.cs
// F2-10 — contract scriptable-object-schema.md 5. (MathDifficultyTableSO), the
// numbers themselves: balance/difficulty-model.md 2. and 3. PlayMode, not
// EditMode: the type is a ScriptableObject, so the mirror dotnet solution
// cannot link it (unity-csharp skill, "Buktatók (ScriptableObject-konfig,
// F1-09)").
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Expressions;
using MathHorde.Core.Problems;
using MathHorde.Infrastructure.Configuration;
using MathHorde.MathEngine;
using MathHorde.Tests.PlayMode.TestSupport;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MathHorde.Tests.PlayMode.Configuration
{
    [TestFixture]
    public class MathDifficultyTableSOTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        private MathDifficultyTableSO CreateTable()
        {
            var table = ScriptableObject.CreateInstance<MathDifficultyTableSO>();
            _created.Add(table);
            return table;
        }

        private static MathDifficultyTableSO.MathDifficultyLevelEntry Row(
            MathDifficultyTableSO table, int level)
        {
            return table.GetLevel(level);
        }

        [Test]
        public void DefaultTable_IsValid()
        {
            MathDifficultyTableSO table = CreateTable();

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(string.Join(" | ", errors), Is.EqualTo(string.Empty));
            Assert.That(errors.Count, Is.EqualTo(0));
        }

#if UNITY_EDITOR
        [Test]
        public void ShippedAsset_ImportsAndContainsAllTenDesignedRows()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MathDifficultyTableSO>(
                "Assets/_Project/Configuration/MathDifficultyTable.asset");

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.Validate().Count, Is.EqualTo(0), string.Join(" | ", asset.Validate()));
            MathDifficultyTableSO defaults = CreateTable();
            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                Assert.That(JsonUtility.ToJson(asset.GetLevel(level)),
                    Is.EqualTo(JsonUtility.ToJson(defaults.GetLevel(level))), $"level={level}");
            }
        }
#endif

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Validate_WithNonFiniteThinkTime_ReportsIt(float thinkTime)
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 1), "_baseThinkTimeSeconds", thinkTime);

            Assert.That(ConfigTestReflection.AnyContains(table.Validate(), "BaseThinkTimeSeconds"), Is.True);
        }

        [Test]
        public void Validate_WithMissingOrUndefinedSkills_ReportsIt()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 1), "_requiredSkills", null);
            ConfigTestReflection.SetField(Row(table, 2), "_requiredSkills", new List<SkillTag> { (SkillTag)999 });

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Levels[0].RequiredSkills"), Is.True);
            Assert.That(ConfigTestReflection.AnyContains(errors, "Levels[1].RequiredSkills"), Is.True);
        }

        [Test]
        public void Validate_WithUndefinedOperatorOrPlacement_ReportsIt()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 1), "_allowedOperators", new List<BinaryOperator> { (BinaryOperator)999 });
            ConfigTestReflection.SetField(Row(table, 8), "_unknownPlacement", (UnknownPlacement)999);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "AllowedOperators"), Is.True);
            Assert.That(ConfigTestReflection.AnyContains(errors, "UnknownPlacement"), Is.True);
        }

        [Test]
        public void Validate_WithNullOrDuplicateRows_ReportsIt()
        {
            MathDifficultyTableSO table = CreateTable();
            var rows = new List<MathDifficultyTableSO.MathDifficultyLevelEntry>(table.Levels);
            rows[1] = null;
            rows[4] = rows[3];
            ConfigTestReflection.SetField(table, "_levels", rows);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Levels[1]"), Is.True);
            Assert.That(ConfigTestReflection.AnyContains(errors, "Level szigorúan monoton"), Is.True);
        }

        [Test]
        public void DefaultTable_HasExactlyTenStrictlyIncreasingLevels()
        {
            MathDifficultyTableSO table = CreateTable();

            Assert.That(table.Levels.Count, Is.EqualTo(MathDifficultyTableSO.RequiredLevelCount));

            for (int i = 0; i < table.Levels.Count; i++)
            {
                Assert.That(table.Levels[i].Level, Is.EqualTo(i + 1));
                Assert.That(table.Levels[i].DescriptionKey, Is.EqualTo($"diff.l{i + 1}"));
            }
        }

        [Test]
        public void DefaultTable_ThinkTimeMatchesTimePressureTableAndDecreasesStrictly()
        {
            // difficulty-model.md 3. — the canonical ThinkTimeSeconds column. The
            // lowest value is 4.5 s on purpose (never below 4.0 s: a 7-year-old's
            // ~0.8 s lane-change would eat the thinking time).
            float[] expected = { 14.0f, 13.0f, 11.5f, 10.0f, 8.0f, 7.5f, 7.0f, 6.5f, 5.5f, 4.5f };
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                Assert.That(Row(table, level).BaseThinkTimeSeconds, Is.EqualTo(expected[level - 1]));
                Assert.That(Row(table, level).BaseThinkTimeSeconds, Is.GreaterThanOrEqualTo(4.0f));
            }
        }

        [Test]
        public void DefaultTable_MinGateGapIsEightUpToLevelThreeAndFourAbove()
        {
            // GAME_DESIGN 3.5/1. via difficulty-model.md 2.
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                Assert.That(Row(table, level).MinGateGap, Is.EqualTo(level <= 3 ? 8 : 4));
            }
        }

        [Test]
        public void DefaultTable_AnswerOptionCountFollowsTheTwoThreeFourRamp()
        {
            int[] expected = { 2, 2, 3, 3, 3, 4, 4, 4, 4, 4 };
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                MathDifficultyTableSO.MathDifficultyLevelEntry row = Row(table, level);
                Assert.That(row.AnswerOptionCount, Is.EqualTo(expected[level - 1]));
                Assert.That(row.LaneCount, Is.EqualTo(row.AnswerOptionCount));
            }
        }

        [Test]
        public void DefaultTable_OperatorsWidenWithTheLevelAndNeverNarrow()
        {
            MathDifficultyTableSO table = CreateTable();

            Assert.That(Row(table, 1).AllowedOperators.Count, Is.EqualTo(2));
            Assert.That(Row(table, 3).AllowedOperators.Count, Is.EqualTo(4));
            Assert.That(Row(table, 5).AllowedOperators.Count, Is.EqualTo(6));
            Assert.That(Row(table, 1).AllowedOperators.Contains(BinaryOperator.Multiplication), Is.False);
            Assert.That(Row(table, 3).AllowedOperators.Contains(BinaryOperator.Multiplication), Is.True);
            Assert.That(Row(table, 5).AllowedOperators.Contains(BinaryOperator.Modulo), Is.True);

            for (int level = 2; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                foreach (BinaryOperator op in Row(table, level - 1).AllowedOperators)
                {
                    Assert.That(Row(table, level).AllowedOperators.Contains(op), Is.True);
                }
            }
        }

        [Test]
        public void DefaultTable_ParenthesesStartAtLevelSevenAndNestOnlyAtTen()
        {
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= 6; level++)
            {
                Assert.That(Row(table, level).AllowParentheses, Is.False);
                Assert.That(Row(table, level).MaxParenthesisDepth, Is.EqualTo(0));
            }

            for (int level = 7; level <= 9; level++)
            {
                Assert.That(Row(table, level).AllowParentheses, Is.True);
                Assert.That(Row(table, level).MaxParenthesisDepth, Is.EqualTo(1));
            }

            Assert.That(Row(table, 10).MaxParenthesisDepth, Is.EqualTo(2));
        }

        [Test]
        public void DefaultTable_UnknownOperandStartsAtLevelEightAndIsEitherSideOnlyAtTen()
        {
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= 7; level++)
            {
                Assert.That(Row(table, level).AllowUnknownOperand, Is.False);
                Assert.That(Row(table, level).UnknownOperandCount, Is.EqualTo(0));
                Assert.That(Row(table, level).UnknownPlacement, Is.EqualTo(UnknownPlacement.None));
            }

            Assert.That(Row(table, 8).UnknownOperandCount, Is.EqualTo(1));
            Assert.That(Row(table, 8).UnknownPlacement, Is.EqualTo(UnknownPlacement.RightOperand));
            Assert.That(Row(table, 9).UnknownPlacement, Is.EqualTo(UnknownPlacement.RightOperand));
            Assert.That(Row(table, 10).UnknownPlacement, Is.EqualTo(UnknownPlacement.EitherSide));
        }

        [Test]
        public void DefaultTable_NoLevelAllowsNegativeResults()
        {
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                Assert.That(Row(table, level).AllowNegativeResults, Is.False);
            }
        }

        [Test]
        public void DefaultTable_OperandCountsStayWithinTheMvpTwoToFiveBand()
        {
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                MathDifficultyTableSO.MathDifficultyLevelEntry row = Row(table, level);
                Assert.That(row.MinOperandCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(row.MaxOperandCount, Is.LessThanOrEqualTo(5));
                Assert.That(row.MaxOperandCount, Is.GreaterThanOrEqualTo(row.MinOperandCount));
                Assert.That(row.MaxResultValue, Is.GreaterThanOrEqualTo(row.MaxOperandValue));
            }

            Assert.That(Row(table, 10).MaxOperandCount, Is.EqualTo(5));
            Assert.That(Row(table, 10).MaxResultValue, Is.EqualTo(999));
        }

        [Test]
        public void DefaultTable_ThreeDimensionsMirrorTheLevel()
        {
            // The table ships the neutral diagonal (difficulty-model.md 2.: the three
            // dimensions default to the level itself); the DDA moves them apart later.
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                MathDifficultyTableSO.MathDifficultyLevelEntry row = Row(table, level);
                Assert.That(row.MathComplexity, Is.EqualTo(level));
                Assert.That(row.TimePressure, Is.EqualTo(level));
                Assert.That(row.CombatDifficulty, Is.EqualTo(level));
            }
        }

        [Test]
        public void DefaultTable_EveryLevelIntroducesOrPractisesAtLeastOneSkill()
        {
            MathDifficultyTableSO table = CreateTable();

            for (int level = 1; level <= MathDifficultyTableSO.RequiredLevelCount; level++)
            {
                Assert.That(Row(table, level).RequiredSkills.Count, Is.GreaterThan(0));
            }
        }

        [Test]
        public void GetLevel_WithUnknownLevel_ThrowsKeyNotFound()
        {
            MathDifficultyTableSO table = CreateTable();

            Assert.That(() => table.GetLevel(0), Throws.TypeOf<KeyNotFoundException>());
            Assert.That(() => table.GetLevel(11), Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void Validate_WithFewerThanTenRows_ReportsTheCount()
        {
            MathDifficultyTableSO table = CreateTable();
            var trimmed = new List<MathDifficultyTableSO.MathDifficultyLevelEntry>(table.Levels);
            trimmed.RemoveAt(trimmed.Count - 1);
            ConfigTestReflection.SetField(table, "_levels", trimmed);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "elemszáma"), Is.True);
        }

        [Test]
        public void Validate_WithNullLevels_ReportsIt()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(table, "_levels", null);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Levels"), Is.True);
        }

        [Test]
        public void Validate_WithNonDecreasingThinkTime_ReportsTheMonotonicityRule()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 2), "_baseThinkTimeSeconds", 20.0f);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "BaseThinkTimeSeconds"), Is.True);
        }

        [Test]
        public void Validate_WithParenthesisFlagAndDepthDisagreeing_ReportsTheInvariant()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 1), "_allowParentheses", true);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "AllowParentheses"), Is.True);
        }

        [Test]
        public void Validate_WithUnknownFlagAndCountDisagreeing_ReportsTheInvariant()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 1), "_allowUnknownOperand", true);
            ConfigTestReflection.SetField(Row(table, 1), "_unknownPlacement", UnknownPlacement.RightOperand);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "AllowUnknownOperand"), Is.True);
        }

        [Test]
        public void Validate_WithUnknownOperandButNoPlacement_ReportsTheGeneratorPrecondition()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 8), "_unknownPlacement", UnknownPlacement.None);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "UnknownPlacement"), Is.True);
        }

        [Test]
        public void Validate_WithUnknownOperandButNoAdditionOrMultiplication_ReportsIt()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(
                Row(table, 8),
                "_allowedOperators",
                new List<BinaryOperator> { BinaryOperator.Subtraction, BinaryOperator.Division });

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "Multiplication"), Is.True);
        }

        [Test]
        public void Validate_WithEmptyOperatorList_ReportsIt()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 1), "_allowedOperators", new List<BinaryOperator>());

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "AllowedOperators"), Is.True);
        }

        [Test]
        public void Validate_WithLaneCountBelowAnswerOptionCount_ReportsIt()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 10), "_laneCount", 2);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "LaneCount"), Is.True);
        }

        [Test]
        public void Validate_WithMaxResultBelowMaxOperand_AllowsIndependentBounds()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 5), "_maxResultValue", 10);

            IReadOnlyList<string> errors = table.Validate();

            // E.g. 90 - 85 = 5 is valid even when operands may reach 100.
            Assert.That(errors.Count, Is.EqualTo(0), string.Join(" | ", errors));
        }

        [Test]
        public void Validate_WithOutOfRangeValue_ReportsTheRange()
        {
            MathDifficultyTableSO table = CreateTable();
            ConfigTestReflection.SetField(Row(table, 1), "_minGateGap", 99);

            IReadOnlyList<string> errors = table.Validate();

            Assert.That(ConfigTestReflection.AnyContains(errors, "MinGateGap"), Is.True);
        }
    }
}
