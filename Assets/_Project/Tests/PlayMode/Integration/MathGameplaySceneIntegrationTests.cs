using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MathHorde.Core.Common;
using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;
using MathHorde.GameLogic.Events;
using MathHorde.Infrastructure.Input;
using MathHorde.Presentation.Bootstrap;
using MathHorde.Presentation.Gates;
using MathHorde.Presentation.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MathHorde.Tests.PlayMode.Integration
{
    public class MathGameplaySceneIntegrationTests
    {
        private float _timeScale;
        private TestSupport.TempSaveDirectory _saveDirectory;
        [SetUp] public void SetUp() { _timeScale = Time.timeScale; _saveDirectory = new TestSupport.TempSaveDirectory(); }
        [TearDown] public void TearDown() { Time.timeScale = _timeScale; _saveDirectory.Dispose(); }
        [UnityTest] public IEnumerator Starter_RealSceneResolvesTenEventsDuringCombat() => Run("starter_blaster");
        [UnityTest] public IEnumerator Heavy_RealSceneResolvesTenEventsDuringCombat() => Run("heavy_blaster");
        [UnityTest] public IEnumerator TransientSaveFailureRetainsFinalizationAndRetriesBothOfflineWrites() => Run("starter_blaster", failSave: true);

        [UnityTest]
        public IEnumerator InvalidStartupLimitsAreVisibleAndNeverAdvanceGameplay()
        {
            MathHorde.Infrastructure.Configuration.MathEventConfigSO invalid = null;
            void Configure(Scene scene, LoadSceneMode mode)
            {
                var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplaySessionBootstrap>(true)).Single();
                var field = typeof(GameplaySessionBootstrap).GetField("_mathConfig", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                invalid = Object.Instantiate((MathHorde.Infrastructure.Configuration.MathEventConfigSO)field.GetValue(host));
                TestSupport.ConfigTestReflection.SetField(invalid, "_resourceGateOperandMin", 11);
                TestSupport.ConfigTestReflection.SetField(invalid, "_resourceGateOperandMax", 20);
                field.SetValue(host, invalid);
            }
            SceneManager.sceneLoaded += Configure;
            try
            {
                LogAssert.Expect(LogType.Error, "GATE_LIMITS_INVALID");
                SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
                yield return null; yield return null;
                SceneManager.sceneLoaded -= Configure;
                var bootstrap = Object.FindAnyObjectByType<GameplaySessionBootstrap>();
                Assert.That(bootstrap.LastMathPreparationError, Is.EqualTo("GATE_LIMITS_INVALID"));
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(bootstrap.Clock.GameTime, Is.EqualTo(0));
                Assert.That(bootstrap.Session, Is.Not.Null);
            }
            finally
            {
                SceneManager.sceneLoaded -= Configure;
                if (invalid != null) Object.DestroyImmediate(invalid);
            }
        }

        private IEnumerator Run(string weapon, bool failSave = false)
        {
            Time.timeScale = 1;
            void BindStorage(Scene scene, LoadSceneMode mode)
            {
                var bootstrap = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameplaySessionBootstrap>(true)).Single();
                TestSupport.ConfigTestReflection.SetField(bootstrap, "_localDataDirectory", _saveDirectory.Path);
            }
            SceneManager.sceneLoaded += BindStorage;
            try { SceneManager.LoadScene("Gameplay", LoadSceneMode.Single); yield return null; yield return null; }
            finally { SceneManager.sceneLoaded -= BindStorage; }
            var host = Object.FindAnyObjectByType<GameplaySessionBootstrap>();
            Assert.That(host, Is.Not.Null);
            Assert.That(host.LastMathPreparationError, Is.Null);
            Assert.That(host.Session.SelectWeapon(weapon).IsSuccess, Is.True);
            Assert.That(host.Combat.CurrentWeaponId, Is.EqualTo(weapon));
            Assert.That(Object.FindAnyObjectByType<GatePresenter>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<MathQuestionPresenter>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<MathFeedbackPresenter>(), Is.Not.Null);
            var input = Object.FindAnyObjectByType<InputAdapter>();
            int[] completed = new int[2], shotsDuring = new int[2], damageDuring = new int[2];
            int playerHits = 0;
            bool movementDuringMath = false;
            double lastCompletion = 0;
            var immutable = new Dictionary<MathEventId, string>();
            int ActiveType()
            {
                foreach (var item in host.MathDirector.ActiveEvents)
                    if (item.State == MathEventState.Preview || item.State == MathEventState.Approaching || item.State == MathEventState.AwaitingDecision)
                        return (int)item.Type;
                return -1;
            }
            void Shot(WeaponFired e) { int t = ActiveType(); if (t >= 0 && t < 2) shotsDuring[t]++; }
            void Damage(EnemyDamaged e) { int t = ActiveType(); if (t >= 0 && t < 2) damageDuring[t]++; }
            void Hit(PlayerDamaged e) => playerHits++;
            void Complete(MathEventCompleted e)
            {
                var item = host.MathDirector.ActiveEvents.First(x => x.EventId == e.EventId);
                completed[(int)item.Type]++; lastCompletion = e.GameTime;
                Assert.That(e.Resource, Is.EqualTo(ResourceType.Health));
                TestContext.WriteLine(weapon + " " + item.Type + " completed at " + e.GameTime.ToString("F3") + "s; applied HP=" + e.AppliedDelta);
            }
            host.Events.Subscribe<WeaponFired>(Shot); host.Events.Subscribe<EnemyDamaged>(Damage);
            host.Events.Subscribe<PlayerDamaged>(Hit); host.Events.Subscribe<MathEventCompleted>(Complete);
            Time.timeScale = 12;
            float realDeadline = Time.realtimeSinceStartup + 40;
            int previousId = -1; float previousDepth = 0;
            bool saveBlocked = false;
            string profilePath = System.IO.Path.Combine(_saveDirectory.Path, "profile.json");
            string statisticsPath = System.IO.Path.Combine(_saveDirectory.Path, "statistics.json");
            while (host.Session.IsRunning && host.Clock.GameTime < 151 && Time.realtimeSinceStartup < realDeadline)
            {
                if (failSave && !saveBlocked && host.Clock.GameTime > 145)
                {
                    // Both paths belong to this test's uniquely named temp root.
                    // Empty directories provoke real atomic-write IO failures.
                    if (System.IO.File.Exists(profilePath)) System.IO.File.Delete(profilePath);
                    System.IO.Directory.CreateDirectory(profilePath);
                    System.IO.Directory.CreateDirectory(statisticsPath);
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Local profile save failed; retained for autosave retry:"));
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Local statistics save failed; retained for autosave retry:"));
                    saveBlocked = true;
                }
                bool deciding = host.MathDirector.ActiveEvents.Any(e => e.State == MathEventState.AwaitingDecision);
                if (!deciding && host.Combat.PlayerLane.Value != 1)
                    input.ApplyGesture(new Vector2(Screen.width * .5f, 200), new Vector2(Screen.width * .5f, 200), Screen.width);
                foreach (var item in host.MathDirector.ActiveEvents)
                {
                    if (item.Problem != null)
                    {
                        string signature = item.Problem.DisplayExpression + ":" + item.Problem.CorrectAnswer.ToDisplayString();
                        if (immutable.TryGetValue(item.EventId, out string original)) Assert.That(signature, Is.EqualTo(original));
                        else immutable[item.EventId] = signature;
                        Assert.That(item.Problem.Reward.Resource, Is.EqualTo(ResourceType.Health));
                        Assert.That(item.Problem.Reward.FlatAmount, Is.EqualTo(item.Type == ProblemType.ResourceOperationComparison ? 30 : 15));
                    }
                    if (item.State == MathEventState.AwaitingDecision && host.Clock.GameTime >= item.DecisionOpenedAt + .8)
                    {
                        var correct = item.Problem.Options.Single(o => o.IsCorrect);
                        float x = (correct.Lane.Value + .5f) / 3f * Screen.width;
                        input.ApplyGesture(new Vector2(x, 200), new Vector2(x, 200), Screen.width);
                    }
                }
                var enemies = host.Combat.ActiveEnemies;
                foreach (var enemy in enemies)
                    if (enemy.InstanceId == previousId && enemy.Position.Y < previousDepth && ActiveType() >= 0) movementDuringMath = true;
                if (enemies.Count > 0) { previousId = enemies[0].InstanceId; previousDepth = enemies[0].Position.Y; }
                yield return null;
            }
            host.Events.Unsubscribe<WeaponFired>(Shot); host.Events.Unsubscribe<EnemyDamaged>(Damage);
            host.Events.Unsubscribe<PlayerDamaged>(Hit); host.Events.Unsubscribe<MathEventCompleted>(Complete);
            TestContext.WriteLine("End: time=" + host.Clock.GameTime + "; health=" + host.Resources.GetCurrent(ResourceType.Health) + "; result=" + host.Session.Result);
            Assert.That(completed, Is.EqualTo(new[] { 5, 5 }));
            Assert.That(lastCompletion, Is.LessThan(135), "Leaves the configured final 15 seconds free of questions; .8s reaction driver, not a full-window guarantee.");
            Assert.That(host.Attempts.Count, Is.EqualTo(10));
            Assert.That(host.Attempts.All(a => a.WasCorrect && !a.Timeout), Is.True);
            Assert.That(shotsDuring.All(n => n > 0), Is.True);
            Assert.That(damageDuring.All(n => n > 0), Is.True);
            Assert.That(movementDuringMath, Is.True);
            Assert.That(host.Resources.State.Pools.ContainsKey(ResourceType.Ammo), Is.False);
            Assert.That(host.Clock.IsPaused, Is.False);
            Assert.That(host.LastMathPreparationError, Is.Null);
            Assert.That(host.Session.Result, Is.EqualTo(MathHorde.Core.Progress.SessionResult.Victory));
            if (failSave)
            {
                Assert.That(saveBlocked, Is.True);
                Assert.That(host.Session.Finalize(host.Session.Result, host.Session.FailureReason).TotalQuestions, Is.EqualTo(10));
                Assert.That(host.AppStateMachine.Current, Is.EqualTo(MathHorde.GameLogic.AppFlow.AppState.LevelComplete));
                System.IO.Directory.Delete(profilePath);
                System.IO.Directory.Delete(statisticsPath);
                float retryDeadline = Time.realtimeSinceStartup + 8;
                while (!System.IO.File.Exists(statisticsPath) && Time.realtimeSinceStartup < retryDeadline) yield return null;
                Assert.That(System.IO.File.Exists(profilePath), Is.True, "The normal periodic autosave retries the profile independently.");
            }
            var saved = new MathHorde.Infrastructure.Persistence.LocalStatisticsRepository(_saveDirectory.Path).LoadAll();
            Assert.That(saved.Count, Is.EqualTo(1));
            Assert.That(saved[0].TotalQuestions, Is.EqualTo(10));
            Assert.That(saved[0].Attempts.Count, Is.EqualTo(10));
            TestContext.WriteLine(weapon + ": shots=" + string.Join(",", shotsDuring) + "; enemy damage=" + string.Join(",", damageDuring) + "; player contacts=" + playerHits);
        }
    }
}
