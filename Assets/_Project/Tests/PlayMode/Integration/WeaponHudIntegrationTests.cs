using System;
using System.Collections;
using MathHorde.Core.Common;
using MathHorde.Core.Resources;
using MathHorde.GameLogic.AppFlow;
using MathHorde.GameLogic.Events;
using MathHorde.Infrastructure.Input;
using MathHorde.Presentation.Bootstrap;
using MathHorde.Presentation.Hud;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MathHorde.Tests.PlayMode.Integration
{
    public class WeaponHudIntegrationTests
    {
        private Mouse _mouse;
        private InputSettings.BackgroundBehavior _previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorInputBehavior;
        [SetUp]
        public void SetUp()
        {
            // Headless batch mode has no focused Game View. Route queued device
            // events through the real runtime/UI module without requiring one.
            _previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            _previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown]
        public void TearDown()
        {
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            InputSystem.settings.backgroundBehavior = _previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInputBehavior;
        }

        [UnityTest]
        public IEnumerator RealScenePointerSwitchesLocalizedButtons_ChangesActualDamage_AndNeverEmitsLaneInput()
        {
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null; yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameplaySessionBootstrap>();
            var hud = Object.FindAnyObjectByType<GameplayHUD>();
            var input = Object.FindAnyObjectByType<InputAdapter>();
            Assert.That(bootstrap.Session.AvailableWeapons.Count, Is.EqualTo(2));
            Assert.That(bootstrap.Session.CurrentWeaponId, Is.EqualTo("starter_blaster"));
            var first = hud.transform.Find("SafeAreaRoot/WeaponSelector/WeaponButton0").GetComponent<Button>();
            var second = hud.transform.Find("SafeAreaRoot/WeaponSelector/WeaponButton1").GetComponent<Button>();
            Assert.That(first.transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("Alapfegyver"));
            Assert.That(second.transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("Erős fegyver"));
            Assert.That(first.transform.Find("Active").gameObject.activeSelf, Is.True);
            Assert.That(second.transform.Find("Active").gameObject.activeSelf, Is.False);
            Assert.That(((RectTransform)second.transform).rect.height, Is.GreaterThanOrEqualTo(144));
            Assert.That(((RectTransform)second.transform).rect.width, Is.GreaterThanOrEqualTo(144));
            Assert.That(bootstrap.Session.AvailableWeapons[0].Damage, Is.EqualTo(5));
            Assert.That(bootstrap.Session.AvailableWeapons[1].Damage, Is.EqualTo(10));

            int taps = 0, swipes = 0, changes = 0, starterDamage = 0, heavyDamage = 0;
            string lastWeapon = null; int lastTarget = -1;
            input.TapDetected += _ => taps++;
            input.SwipeDetected += _ => swipes++;
            bootstrap.Events.Subscribe<WeaponChanged>(_ => changes++);
            bootstrap.Events.Subscribe<WeaponFired>(e => { lastWeapon = e.WeaponId; lastTarget = e.TargetEnemyInstanceId; });
            bootstrap.Events.Subscribe<EnemyDamaged>(e =>
            {
                if (e.EnemyInstanceId != lastTarget) return;
                if (lastWeapon == "starter_blaster") starterDamage = e.Damage;
                if (lastWeapon == "heavy_blaster") heavyDamage = e.Damage;
            });
            float until = Time.realtimeSinceStartup + 5;
            while (starterDamage == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(starterDamage, Is.EqualTo(5), "Measured on a real wave enemy, via damage resolution.");
            _mouse = InputSystem.AddDevice<Mouse>();
            yield return Click(second);
            Assert.That(bootstrap.Session.CurrentWeaponId, Is.EqualTo("heavy_blaster"));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(second.transform.Find("Active").gameObject.activeSelf, Is.True);
            Assert.That(first.transform.Find("Active").gameObject.activeSelf, Is.False);
            until = Time.realtimeSinceStartup + 5;
            while (heavyDamage == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(heavyDamage, Is.EqualTo(10));
            Assert.That(heavyDamage, Is.GreaterThan(starterDamage));
            yield return Click(first);
            Assert.That(bootstrap.Session.CurrentWeaponId, Is.EqualTo("starter_blaster"));
            Assert.That(changes, Is.EqualTo(2));

            // A gesture beginning over a weapon button must remain consumed even
            // when its release is outside that button (no accidental math swipe).
            Vector2 position = Center(second);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position + new Vector2(200, 200) });
            yield return null; yield return null;
            Assert.That(taps, Is.Zero); Assert.That(swipes, Is.Zero);
            Assert.That(bootstrap.Combat.PlayerLane.Value, Is.EqualTo(0));

            bootstrap.AppStateMachine.Fire(AppTrigger.PausePressed);
            yield return null;
            Assert.That(first.interactable, Is.False); Assert.That(second.interactable, Is.False);
            yield return Click(second);
            Assert.That(bootstrap.Session.CurrentWeaponId, Is.EqualTo("starter_blaster"));
            bootstrap.AppStateMachine.Fire(AppTrigger.ResumePressed);
            yield return null;
            Assert.That(second.interactable, Is.True);
            bootstrap.Session.Finalize(MathHorde.Core.Progress.SessionResult.Victory, MathHorde.Core.Progress.FailureReason.None);
            yield return null;
            Assert.That(second.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator SynchronousWeaponEventOnlyMarksDirty_AndAllocatesNothingBeforeRenderPending()
        {
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null; yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameplaySessionBootstrap>();
            var hud = Object.FindAnyObjectByType<GameplayHUD>();
            var active = hud.transform.Find("SafeAreaRoot/WeaponSelector/WeaponButton1/Active").gameObject;
            // Warm the complete actual GameEventBus -> HUD-model subscriber path.
            bootstrap.Session.SelectWeapon("heavy_blaster"); bootstrap.Session.SelectWeapon("starter_blaster"); hud.RenderPending();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                bootstrap.Session.SelectWeapon("heavy_blaster");
                bootstrap.Session.SelectWeapon("starter_blaster");
                bootstrap.Session.SelectWeapon(null);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
            bootstrap.Session.SelectWeapon("heavy_blaster");
            Assert.That(active.activeSelf, Is.False, "Handler stores metadata only, no synchronous UI work.");
            hud.RenderPending();
            Assert.That(active.activeSelf, Is.True);
            // Resource and wave subscribers must also defer strings/TMP work
            // until RenderPending, so the real HUD cannot allocate in Tick.
            bootstrap.Combat.SpawnEnemy("armored_bot", new LaneIndex(0), 5f);
            for (int i = 0; i < 100; i++)
            {
                bootstrap.Resources.ApplyDelta(ResourceType.Health, -1, "TEST");
                bootstrap.Resources.ApplyDelta(ResourceType.Health, 1, "TEST");
                bootstrap.Session.Tick(.00001f);
            }
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                bootstrap.Resources.ApplyDelta(ResourceType.Health, -1, "TEST");
                bootstrap.Resources.ApplyDelta(ResourceType.Health, 1, "TEST");
                bootstrap.Session.Tick(.00001f);
            }
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
            hud.RenderPending();
        }

        private static Vector2 Center(Button button) => RectTransformUtility.WorldToScreenPoint(null,
            ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        private IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 position = Center(button);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            yield return null; yield return null;
        }
    }
}
