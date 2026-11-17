// LevelResultScreenLayoutTests.cs
// F1-16 — structural layout assertions for the LevelResultScreen prefab
// (built by LevelResultScreenBuilder): the panel starts hidden (only
// LevelResultScreen.Initialize()/ApplyState() may show it, based on the
// real IAppStateMachine state), both buttons exist with a touch-friendly
// tap target (unity-ui skill: min. ~48dp), and the SerializeField wiring
// in the builder actually points at the objects LevelResultScreen expects
// (so a missing reference does not silently ship as a decorative, dead
// button — spec 22.).
//
// Editor-only (AssetDatabase), consistent with GameplayHudLayoutTests.cs.
#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MathHorde.Presentation.Result;

namespace MathHorde.Tests.PlayMode.Result
{
    [TestFixture]
    public class LevelResultScreenLayoutTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/LevelResultScreen.prefab";

        private static GameObject LoadPrefabOrFail()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null,
                $"LevelResultScreen.prefab hiányzik ({PrefabPath}) — futtasd a Math Horde/UI/Build Level Result Screen Prefab menüpontot.");
            return prefab;
        }

        private static Transform FindOrFail(GameObject root, string path)
        {
            Transform found = root.transform.Find(path);
            Assert.That(found, Is.Not.Null, $"Hiányzó elem a prefabban: \"{path}\".");
            return found;
        }

        [Test]
        public void ResultPanel_StartsInactive_OnlyAppStateDrivenVisibilityShowsIt()
        {
            GameObject prefab = LoadPrefabOrFail();
            GameObject resultPanel = FindOrFail(prefab, "SafeAreaRoot/ResultPanel").gameObject;

            Assert.That(resultPanel.activeSelf, Is.False,
                "a panel ne jelenjen meg automatikusan — csak LevelResultScreen.Initialize() dönthet a láthatóságról.");
        }

        [TestCase("SafeAreaRoot/ResultPanel/Card/ButtonRow/PrimaryButton")]
        [TestCase("SafeAreaRoot/ResultPanel/Card/ButtonRow/SecondaryButton")]
        public void Buttons_HaveATouchFriendlyTapTarget(string path)
        {
            GameObject prefab = LoadPrefabOrFail();
            var rect = (RectTransform)FindOrFail(prefab, path);

            const float referenceWidth = 1080f;
            const float referenceHeight = 1920f;
            // Card spans y 0.30..0.70 (40% of screen height); ButtonRow spans
            // the card's own y 0.08..0.32 (24% of the card); PrimaryButton/
            // SecondaryButton each take the full ButtonRow height.
            const float cardHeightFraction = 0.40f;
            const float buttonRowHeightFractionOfCard = 0.24f;

            float widthPx = (rect.anchorMax.x - rect.anchorMin.x) * referenceWidth * 0.84f; // Card x-span 0.08..0.92 = 84%
            float heightPx = (rect.anchorMax.y - rect.anchorMin.y)
                * referenceHeight * cardHeightFraction * buttonRowHeightFractionOfCard;

            const float minimumTapTargetPx = 100f;

            Assert.That(widthPx, Is.GreaterThanOrEqualTo(minimumTapTargetPx));
            Assert.That(heightPx, Is.GreaterThanOrEqualTo(minimumTapTargetPx));
        }

        [Test]
        public void Buttons_HaveAButtonComponent_NotJustAnImage()
        {
            GameObject prefab = LoadPrefabOrFail();

            Button primary = FindOrFail(prefab, "SafeAreaRoot/ResultPanel/Card/ButtonRow/PrimaryButton").GetComponent<Button>();
            Button secondary = FindOrFail(prefab, "SafeAreaRoot/ResultPanel/Card/ButtonRow/SecondaryButton").GetComponent<Button>();

            Assert.That(primary, Is.Not.Null, "PrimaryButton gomb nélkül dekoratív, működésképtelen elem lenne.");
            Assert.That(secondary, Is.Not.Null, "SecondaryButton gomb nélkül dekoratív, működésképtelen elem lenne.");
        }

        [Test]
        public void LevelResultScreen_SerializedReferences_PointAtTheActualBuiltObjects()
        {
            GameObject prefab = LoadPrefabOrFail();
            var screen = prefab.GetComponent<LevelResultScreen>();
            Assert.That(screen, Is.Not.Null);

            var so = new SerializedObject(screen);
            Assert.That(so.FindProperty("_panelRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("_headlineText").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("_encouragementText").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("_primaryButton").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("_primaryButtonText").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("_secondaryButton").objectReferenceValue, Is.Not.Null);
            Assert.That(so.FindProperty("_secondaryButtonText").objectReferenceValue, Is.Not.Null);
        }
    }
}
#endif
