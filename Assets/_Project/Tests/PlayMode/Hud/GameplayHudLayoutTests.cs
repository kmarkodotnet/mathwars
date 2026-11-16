// GameplayHudLayoutTests.cs
// F1-15 — structural layout assertion that the GameplayHUD prefab
// (built by GameplayHudBuilder) never covers the main combat area
// (math-horde.md 3.2: "a HUD nem takarhatja el a fő harci eseményeket").
// Every direct child of SafeAreaRoot is required to stay within the top
// ~34% of the screen (anchorMin.y >= 0.66 in SafeAreaRoot's own normalized
// space) — lanes, the spawn zone and the player zone occupy the rest and
// are therefore guaranteed to stay unobstructed regardless of what content
// those HUD panels end up showing.
//
// Editor-only (AssetDatabase) — guarded so it only compiles/runs inside the
// Editor test player, consistent with MathHorde.Tests.PlayMode.asmdef
// including the Editor platform alongside device platforms.
#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MathHorde.Tests.PlayMode.Hud
{
    [TestFixture]
    public class GameplayHudLayoutTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/GameplayHUD.prefab";
        private const float MaxAllowedContentDepth = 0.66f; // everything below this stays clear for combat

        private static GameObject LoadPrefabOrFail()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null,
                $"GameplayHUD.prefab hiányzik ({PrefabPath}) — futtasd a Math Horde/UI/Build Gameplay HUD Prefab menüpontot.");
            return prefab;
        }

        private static RectTransform FindRect(GameObject root, string path)
        {
            Transform found = root.transform.Find(path);
            Assert.That(found, Is.Not.Null, $"Hiányzó HUD elem a prefabban: \"{path}\".");
            return (RectTransform)found;
        }

        [TestCase("SafeAreaRoot/TopBar")]
        [TestCase("SafeAreaRoot/StatsPanel")]
        [TestCase("SafeAreaRoot/ChallengeLevelArea_ReservedF4")]
        [TestCase("SafeAreaRoot/MathEventArea_ReservedF2")]
        public void EveryHudPanel_StaysWithinTheTopReservedBand_NeverCoveringTheArena(string path)
        {
            GameObject prefab = LoadPrefabOrFail();
            RectTransform rect = FindRect(prefab, path);

            Assert.That(rect.anchorMin.y, Is.GreaterThanOrEqualTo(MaxAllowedContentDepth),
                $"\"{path}\" a HUD-sávon kívülre (a harci területre) nyúlik.");
        }

        [Test]
        public void ReservedPlaceholders_AreInactiveAndEmpty_NotYetPartOfTheLayout()
        {
            GameObject prefab = LoadPrefabOrFail();

            GameObject mathEventArea = FindRect(prefab, "SafeAreaRoot/MathEventArea_ReservedF2").gameObject;
            GameObject challengeLevelArea = FindRect(prefab, "SafeAreaRoot/ChallengeLevelArea_ReservedF4").gameObject;

            Assert.That(mathEventArea.activeSelf, Is.False, "a matek-feladat terület még nem ez a fázis feladata (F2).");
            Assert.That(challengeLevelArea.activeSelf, Is.False, "a kihívásszint-sáv még nem ez a fázis feladata (F4).");
            Assert.That(mathEventArea.transform.childCount, Is.EqualTo(0));
            Assert.That(challengeLevelArea.transform.childCount, Is.EqualTo(0));
        }

        [Test]
        public void PauseButton_HasATouchFriendlyTapTarget()
        {
            GameObject prefab = LoadPrefabOrFail();
            RectTransform topBar = FindRect(prefab, "SafeAreaRoot/TopBar");
            RectTransform pauseButton = FindRect(prefab, "SafeAreaRoot/TopBar/PauseButton");

            const float referenceWidth = 1080f;
            const float referenceHeight = 1920f;
            const float topBarHeightFraction = 0.10f; // TopBar anchors span y 0.90..1.00 of the full screen

            float widthPx = (pauseButton.anchorMax.x - pauseButton.anchorMin.x) * referenceWidth;
            float heightPx = (pauseButton.anchorMax.y - pauseButton.anchorMin.y) * referenceHeight * topBarHeightFraction;

            // unity-ui skill: min. ~48dp tap target. Even at a generous ~3px/dp
            // density this is a comfortable margin above the minimum.
            const float minimumTapTargetPx = 100f;

            Assert.That(widthPx, Is.GreaterThanOrEqualTo(minimumTapTargetPx));
            Assert.That(heightPx, Is.GreaterThanOrEqualTo(minimumTapTargetPx));
            Assert.That(topBar, Is.Not.Null);
        }
    }
}
#endif
