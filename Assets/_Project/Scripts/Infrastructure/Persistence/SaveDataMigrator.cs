using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

// local-persistence skill: "Minden mentett dokumentum tartalmaz egy
// SchemaVersion mezőt. Verzióemeléshez explicit MigrateV<n>ToV<n+1>
// függvény, soha nem csendes mezőeldobás." See ADR-011 2. for why this is
// JObject-based rather than a chain of versioned POCOs.
namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// Brings a raw profile.json JObject up to
    /// PlayerProfileSaveData.CurrentSchemaVersion before deserialization.
    /// Each step is a pure, named, independently testable
    /// MigrateV&lt;n&gt;ToV&lt;n+1&gt; function — add the next one here (and bump
    /// CurrentSchemaVersion) the next time a destructive field change is
    /// needed; never silently drop or reinterpret an old field.
    /// </summary>
    public static class SaveDataMigrator
    {
        /// <summary>
        /// Applies every missing migration step in order. Returns a JObject
        /// whose "SchemaVersion" equals PlayerProfileSaveData.CurrentSchemaVersion.
        /// </summary>
        public static JObject MigrateToCurrent(JObject root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            int version = (int?)root["SchemaVersion"] ?? 1;

            if (version < 1)
            {
                // Never observed in practice (would mean a hand-edited or
                // foreign file) — treat as V1 rather than guessing further
                // back, and log so a real occurrence is visible.
                Debug.LogWarning("[SaveDataMigrator] SchemaVersion < 1 — V1-ként kezelve.");
                version = 1;
            }

            if (version == 1)
            {
                root = MigrateV1ToV2(root);
                version = 2;
            }

            // Next step goes here:
            // if (version == 2) { root = MigrateV2ToV3(root); version = 3; }

            if (version > PlayerProfileSaveData.CurrentSchemaVersion)
            {
                // A save written by a NEWER build than this one is running —
                // never guess a downgrade; the caller falls back to a fresh
                // profile rather than silently losing/misreading fields.
                throw new InvalidOperationException(
                    $"A mentés sémaverziója ({version}) újabb, mint amit ez a build ismer " +
                    $"({PlayerProfileSaveData.CurrentSchemaVersion}).");
            }

            return root;
        }

        /// <summary>
        /// V1 → V2 (ADR-011 2.): HighestChallengeLevel and DifficultyHistory
        /// did not exist in V1. HighestChallengeLevel defaults to the V1
        /// CurrentChallengeLevel (the highest level ever reached is at
        /// least the current one); DifficultyHistory defaults to empty
        /// (no history was ever recorded under V1).
        /// </summary>
        public static JObject MigrateV1ToV2(JObject v1)
        {
            if (v1 == null)
            {
                throw new ArgumentNullException(nameof(v1));
            }

            var v2 = (JObject)v1.DeepClone();

            if (v2["HighestChallengeLevel"] == null)
            {
                v2["HighestChallengeLevel"] = v2["CurrentChallengeLevel"] ?? 1;
            }

            if (v2["DifficultyHistory"] == null)
            {
                v2["DifficultyHistory"] = new JArray();
            }

            v2["SchemaVersion"] = 2;
            return v2;
        }
    }
}
