// EnemyVisualCatalogSO.cs
// F1-14 — configurable (ScriptableObject, unity-csharp skill: "konfigurálható
// játékelem = ScriptableObject") content asset mapping a GameLogic
// EnemyDefinition.EnemyId to the pooled EnemyView prefab that represents it
// on screen, plus how many instances to prewarm per type. Visual archetype
// assignment ONLY — no health/damage/reward logic, matching
// EnemyDefinition.PrefabKey's contract note ("Presentation oldja fel").
//
// Built by the reproducible EnemyPrefabBuilder Editor tool (math-horde.md
// 20. szakasz), not hand-assembled in the Inspector.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MathHorde.Presentation.Enemies
{
    [CreateAssetMenu(fileName = "EnemyVisualCatalog", menuName = "Math Horde/Enemies/Enemy Visual Catalog")]
    public sealed class EnemyVisualCatalogSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("A GameLogic EnemyDefinition.EnemyId katalóguskulcsa (pl. \"basic_bot\").")]
            public string EnemyId;

            [Tooltip("Vizuális archetípus-kulcs (pl. \"basic\", \"fast\", \"armored\", \"boss\", \"swarmlet\").")]
            public string VisualKey;

            public EnemyView Prefab;

            [Tooltip("Hány példányt hozzon létre az ObjectPool előre, a harci ciklus megkezdése ELŐTT.")]
            public int PrewarmCount = 6;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        [Tooltip("Ismeretlen EnemyId esetén ezt az archetípust/prefabot használja (soha nem marad vizuál nélkül egy ellenség).")]
        [SerializeField] private Entry _fallback;

        public IReadOnlyList<Entry> Entries => _entries;

        public Entry Fallback => _fallback;

        /// <summary>Builds the pure, Unity-free id-&gt;key resolver from this asset's entries.</summary>
        public EnemyVisualKeyMap BuildKeyMap()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Entry entry in _entries)
            {
                if (!string.IsNullOrEmpty(entry.EnemyId) && !string.IsNullOrEmpty(entry.VisualKey))
                {
                    map[entry.EnemyId] = entry.VisualKey;
                }
            }

            string fallbackKey = _fallback != null && !string.IsNullOrEmpty(_fallback.VisualKey)
                ? _fallback.VisualKey
                : "basic";

            return new EnemyVisualKeyMap(map, fallbackKey);
        }

        public Entry FindByVisualKey(string visualKey)
        {
            foreach (Entry entry in _entries)
            {
                if (string.Equals(entry.VisualKey, visualKey, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return _fallback;
        }
    }
}
