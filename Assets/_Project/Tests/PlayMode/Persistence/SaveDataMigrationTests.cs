// F6-01/ADR-011 2. — SchemaVersion migration. Exercises the one real,
// demonstrated migration step (V1 -> V2) both directly (SaveDataMigrator)
// and end-to-end through LocalSaveRepository.Load() reading an actual V1
// sample file written to disk (never a live app-close, by design — see the
// local-persistence skill's "nem kizárólag alkalmazás-bezáráskor" rule,
// which this test also implicitly exercises: the migration path does not
// care HOW the old file got there).
using System;
using System.IO;
using MathHorde.Infrastructure.Persistence;
using MathHorde.Tests.PlayMode.TestSupport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.Persistence
{
    [TestFixture]
    public class SaveDataMigrationTests
    {
        private static string V1SampleJson(Guid profileId) => $@"{{
            ""SchemaVersion"": 1,
            ""ProfileId"": ""{profileId}"",
            ""DisplayName"": ""Régi Mentés"",
            ""CreatedAt"": ""2025-06-01T00:00:00+00:00"",
            ""CurrentChallengeLevel"": 5,
            ""UnlockedWorlds"": [""w1""],
            ""CompletedLevels"": [""w1-l01""],
            ""Skills"": []
        }}";

        [Test]
        public void MigrateV1ToV2_MissingHighestChallengeLevel_DefaultsToCurrentChallengeLevel()
        {
            var v1 = JObject.Parse(V1SampleJson(Guid.NewGuid()));

            JObject v2 = SaveDataMigrator.MigrateV1ToV2(v1);

            Assert.That((int)v2["HighestChallengeLevel"], Is.EqualTo(5));
            Assert.That((int)v2["SchemaVersion"], Is.EqualTo(2));
        }

        [Test]
        public void MigrateV1ToV2_MissingDifficultyHistory_DefaultsToEmptyArray()
        {
            var v1 = JObject.Parse(V1SampleJson(Guid.NewGuid()));

            JObject v2 = SaveDataMigrator.MigrateV1ToV2(v1);

            Assert.That(((JArray)v2["DifficultyHistory"]).Count, Is.EqualTo(0));
        }

        [Test]
        public void MigrateV1ToV2_DoesNotMutateInputObject()
        {
            var v1 = JObject.Parse(V1SampleJson(Guid.NewGuid()));

            SaveDataMigrator.MigrateV1ToV2(v1);

            Assert.That(v1["HighestChallengeLevel"], Is.Null);
            Assert.That((int)v1["SchemaVersion"], Is.EqualTo(1));
        }

        [Test]
        public void MigrateToCurrent_AlreadyCurrentVersion_IsUnchanged()
        {
            var current = new JObject { ["SchemaVersion"] = PlayerProfileSaveData.CurrentSchemaVersion, ["CurrentChallengeLevel"] = 2 };

            JObject result = SaveDataMigrator.MigrateToCurrent(current);

            Assert.That((int)result["SchemaVersion"], Is.EqualTo(PlayerProfileSaveData.CurrentSchemaVersion));
        }

        [Test]
        public void MigrateToCurrent_NewerThanKnownVersion_Throws()
        {
            var future = new JObject { ["SchemaVersion"] = PlayerProfileSaveData.CurrentSchemaVersion + 1 };

            Assert.That(() => SaveDataMigrator.MigrateToCurrent(future), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void LocalSaveRepository_LoadingV1SampleFileFromDisk_MigratesAndLoadsSuccessfully()
        {
            using (var tempDir = new TempSaveDirectory())
            {
                string path = Path.Combine(tempDir.Path, LocalSaveRepository.DefaultFileName);
                var profileId = Guid.NewGuid();
                File.WriteAllText(path, V1SampleJson(profileId));

                var repository = new LocalSaveRepository(tempDir.Path);
                LocalProfileSnapshot snapshot = repository.Load();

                Assert.That(snapshot.Profile.Id.Value, Is.EqualTo(profileId));
                Assert.That(snapshot.Profile.DisplayName, Is.EqualTo("Régi Mentés"));
                Assert.That(snapshot.Profile.CurrentChallengeLevel, Is.EqualTo(5));
                // The migrated field: V1 had no HighestChallengeLevel, so it
                // must come back equal to CurrentChallengeLevel, not 0/default.
                Assert.That(snapshot.Profile.HighestChallengeLevel, Is.EqualTo(5));
                Assert.That(snapshot.DifficultyHistory.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void LocalSaveRepository_ResavingMigratedV1File_PersistsAsCurrentVersion()
        {
            using (var tempDir = new TempSaveDirectory())
            {
                string path = Path.Combine(tempDir.Path, LocalSaveRepository.DefaultFileName);
                File.WriteAllText(path, V1SampleJson(Guid.NewGuid()));

                var repository = new LocalSaveRepository(tempDir.Path);
                LocalProfileSnapshot snapshot = repository.Load();
                repository.Save(snapshot);

                string rewritten = File.ReadAllText(path);
                var rewrittenJson = JObject.Parse(rewritten);
                Assert.That((int)rewrittenJson["SchemaVersion"], Is.EqualTo(PlayerProfileSaveData.CurrentSchemaVersion));
            }
        }
    }
}
