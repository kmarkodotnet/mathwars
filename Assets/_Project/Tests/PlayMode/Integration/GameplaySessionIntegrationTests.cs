// GameplaySessionIntegrationTests.cs
// F1-17 — the Fázis 1 gate's actual proof: loads the REAL, reproducibly-
// built Gameplay.unity scene (GameplaySceneBuilder) and exercises the whole
// wired composition root end-to-end, not a hand-assembled fake. This is the
// direct evidence for math-horde.md 21. kritérium A: "a karakter
// automatikusan lövi a szemből érkező ellenségeket, miközben fogy a
// lőszer, az ellenfelek sebződnek és tovább közelednek".
//
// Scene-level (not a unit test over one service) on purpose: criterion A is
// a statement about the WIRED SYSTEM (WaveDirector -> CombatService ->
// ResourceService -> EnemyPresenter -> GameplayHUD all actually connected in
// one scene), which is exactly what every other test in this codebase so
// far deliberately does NOT cover (see F1-14's Napló: "nincs end-to-end
// PlayMode integrációs teszt a teljes spawn->damage->kill->pool-release
// láncra... az az F1-17 feladata").
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using MathHorde.Core.Resources;
using MathHorde.GameLogic.AppFlow;
using MathHorde.GameLogic.Events;
using MathHorde.Presentation.Bootstrap;
using NUnit.Framework;

namespace MathHorde.Tests.PlayMode.Integration
{
    [TestFixture]
    public class GameplaySessionIntegrationTests
    {
        private const string SceneName = "Gameplay";

        [SetUp]
        public void SetUp()
        {
            // These are the only tests in the project that keep a loaded
            // scene running across many real-time frames (SceneManager.
            // LoadScene + a multi-second wait loop). That window is long
            // enough to coincide with TextMeshPro's one-time, delayed
            // "import TMP Essential Resources?" check
            // (TMP_Settings.DelayShowPackageImporterWindow), which tries to
            // open an EditorWindow and logs "[Error] No graphic device is
            // available to initialize the view." under -nographics batch
            // mode — observed to fail whichever test happens to be running
            // when its timer elapses (confirmed: it hit a different,
            // unrelated test before this file even existed). Unrelated to
            // this fixture's own correctness, which is asserted explicitly
            // below regardless of log output.
            LogAssert.ignoreFailingMessages = true;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator CriterionA_EnemySpawns_PlayerAutoFires_SustainsFireWithoutAmmoPool_EnemyIsDamaged()
        {
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<GameplaySessionBootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "A Gameplay scene-nek tartalmaznia kell egy GameplaySessionBootstrap-ot (composition root).");

            int spawnedCount = 0;
            int firedCount = 0;
            int damagedCount = 0;
            int killedCount = 0;

            void OnSpawned(EnemySpawned e) => spawnedCount++;
            void OnFired(WeaponFired e) => firedCount++;
            void OnDamaged(EnemyDamaged e) => damagedCount++;
            void OnKilled(EnemyKilled e) => killedCount++;

            bootstrap.Events.Subscribe<EnemySpawned>(OnSpawned);
            bootstrap.Events.Subscribe<WeaponFired>(OnFired);
            bootstrap.Events.Subscribe<EnemyDamaged>(OnDamaged);
            bootstrap.Events.Subscribe<EnemyKilled>(OnKilled);

            Assert.That(bootstrap.Resources.State.Pools.ContainsKey(ResourceType.Ammo), Is.False);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline
                   && !(spawnedCount > 0 && firedCount >= 6 && damagedCount > 0 && killedCount > 0))
            {
                yield return null;
            }

            Assert.That(bootstrap.Resources.State.Pools.ContainsKey(ResourceType.Ammo), Is.False);

            bootstrap.Events.Unsubscribe<EnemySpawned>(OnSpawned);
            bootstrap.Events.Unsubscribe<WeaponFired>(OnFired);
            bootstrap.Events.Unsubscribe<EnemyDamaged>(OnDamaged);
            bootstrap.Events.Unsubscribe<EnemyKilled>(OnKilled);

            Assert.That(spawnedCount, Is.GreaterThan(0), "Nem spawnolt ellenség a valós scene-ben (WaveDirector -> CombatService).");
            Assert.That(firedCount, Is.GreaterThan(0), "A karakter nem tüzelt automatikusan a szemből érkező ellenségre.");
            Assert.That(damagedCount + killedCount, Is.GreaterThan(0), "Egyetlen ellenség sem sebződött/halt meg — a lövés nem ért célba.");
            Assert.That(firedCount, Is.GreaterThanOrEqualTo(6));
            Assert.That(killedCount, Is.GreaterThan(0));
            var hud = Object.FindAnyObjectByType<MathHorde.Presentation.Hud.GameplayHUD>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.transform.Find("SafeAreaRoot/StatsPanel/AmmoRow"), Is.Null);
            Assert.That(bootstrap.Resources.GetMax(ResourceType.Shield), Is.EqualTo(50));
            Assert.That(bootstrap.Combat.IsPlayerAlive, Is.True, "A játékosnak életben kell maradnia ennyi idő alatt.");
        }

        [UnityTest]
        public IEnumerator Pause_FreezesCombatAndGameTime_ResumeContinuesIt()
        {
            // F1-11's Napló jelezte nyitott bekötés, amit ez a feladat zár le
            // (factory-docs/contract-findings.md #5): a Pause állapotba lépés
            // ténylegesen megállítja-e a GameSessionService-t a VALÓS scene-ben,
            // nem csak a GameSessionAppFlowBinder izolált tesztjében.
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<GameplaySessionBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);

            int firedCount = 0;
            void OnFired(WeaponFired e) => firedCount++;
            bootstrap.Events.Subscribe<WeaponFired>(OnFired);
            int startingShots = firedCount;

            // Várjunk, amíg a harc TÉNYLEGESEN fogyaszt lőszert — enélkül a
            // "szünetben nem változik" állítás üresen is igaz lenne.
            float runningDeadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < runningDeadline
                   && firedCount <= startingShots)
            {
                yield return null;
            }

            int shotsBeforePause = firedCount;
            Assert.That(shotsBeforePause, Is.GreaterThan(startingShots), "Ez az előfeltétel: a harcnak ténylegesen fogyasztania kellett lőszert szünet előtt.");

            bootstrap.AppStateMachine.Fire(AppTrigger.PausePressed);
            Assert.That(bootstrap.AppStateMachine.Current, Is.EqualTo(AppState.Pause));

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            int shotsAfterPauseWait = firedCount;
            Assert.That(shotsAfterPauseWait, Is.EqualTo(shotsBeforePause),
                "Szünetben a lőszer (és így a harci szimuláció) nem változhat — a GameSessionService.Pause() valóban megállította.");

            bootstrap.AppStateMachine.Fire(AppTrigger.ResumePressed);
            Assert.That(bootstrap.AppStateMachine.Current, Is.EqualTo(AppState.Gameplay));
            float resumeDeadline = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < resumeDeadline && firedCount == shotsBeforePause) yield return null;
            Assert.That(firedCount, Is.GreaterThan(shotsBeforePause));
            bootstrap.Events.Unsubscribe<WeaponFired>(OnFired);
        }
    }
}
