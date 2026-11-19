using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MathHorde.Presentation.Hud;
using MathHorde.Presentation.UI;

/// <summary>Standalone question HUD; F3-12 reserves this band alongside the combat HUD.</summary>
public static class MathQuestionHudBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/MathQuestionHUD.prefab";

    [MenuItem("Math Horde/UI/Build Math Question HUD")]
    public static void Build()
    {
        TmpHungarianFontSetup.EnsureAll();
        TmpHungarianFontSetup.EnsureAll();
        var root = new GameObject("MathQuestionHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = .5f;
        var safe = Child(root.transform, "SafeAreaRoot", 0, 0, 1, 1);
        safe.gameObject.AddComponent<SafeAreaFitter>();
        var band = Child(safe, "QuestionBand", 0, .66f, 1, .90f);
        var presenter = root.AddComponent<MathQuestionPresenter>();
        var so = new SerializedObject(presenter);
        var rows = so.FindProperty("_rows"); rows.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var row = Child(band, "Question" + i, .02f, 1 - (i + 1) / 3f, .98f, 1 - i / 3f);
            var background = row.gameObject.AddComponent<Image>();
            background.color = new Color(.025f, .04f, .07f, .96f); background.raycastTarget = false;
            var question = Text(row, "Question", .025f, .09f, .74f, .96f, 42, 32);
            var timer = Text(row, "Time", .76f, .22f, .98f, .94f, 32, 26);
            timer.alignment = TextAlignmentOptions.Center;
            var fill = Child(row, "TimeFill", .76f, .10f, .98f, .15f).gameObject.AddComponent<Image>();
            // Filled Image needs a sprite; a sprite-less Image renders a full quad.
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.color = new Color(.1f, .75f, .85f); fill.raycastTarget = false;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0; fill.fillAmount = 0;
            var view = row.gameObject.AddComponent<MathQuestionView>();
            var vs = new SerializedObject(view);
            vs.FindProperty("_questionText").objectReferenceValue = question;
            vs.FindProperty("_timeText").objectReferenceValue = timer;
            vs.FindProperty("_timeFill").objectReferenceValue = fill;
            vs.ApplyModifiedPropertiesWithoutUndo();
            rows.GetArrayElementAtIndex(i).objectReferenceValue = view;
            row.gameObject.SetActive(false);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[MathQuestionHudBuilder] Saved " + PrefabPath);
    }

    private static RectTransform Child(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text Text(Transform parent, string name, float x0, float y0, float x1, float y1, float size, float min)
    {
        var text = Child(parent, name, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = string.Empty;
        text.fontSize = text.fontSizeMax = size; text.fontSizeMin = min;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white; text.raycastTarget = false;
        return text;
    }
}
