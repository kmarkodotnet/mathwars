using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MathHorde.Presentation.Gates;

/// <summary>Reproducible, non-interactive world-space math carriers; no physics/combat component.</summary>
public static class GateCarrierBuilder
{
    public const string Folder = "Assets/_Project/Prefabs/Gates";

    [MenuItem("Math Horde/UI/Build Math Carrier Prefabs")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Gates");
        BuildCarrier("ResourceGate", true, new Color(0.10f, 0.75f, 0.85f));
        BuildCarrier("AnswerCarrier", false, new Color(0.95f, 0.65f, 0.15f));
        AssetDatabase.SaveAssets();
        Debug.Log("[GateCarrierBuilder] Saved ResourceGate and AnswerCarrier prefabs.");
    }

    private static void BuildCarrier(string name, bool resource, Color accent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        root.layer = LayerMask.NameToLayer("Ignore Raycast");
        var rect = (RectTransform)root.transform;
        rect.sizeDelta = new Vector2(360, 260);
        rect.localScale = Vector3.one * 0.005f;
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        // Only the label band is filled. The opening below stays transparent for combat.
        Panel(root.transform, "LabelBackground", 0, .35f, 1, 1, new Color(.025f, .04f, .07f, .95f));
        Panel(root.transform, "TopAccent", 0, .96f, 1, 1, accent);
        Panel(root.transform, "LeftPost", 0, 0, .035f, resource ? 1 : .35f, accent);
        Panel(root.transform, "RightPost", .965f, 0, 1, resource ? 1 : .35f, accent);
        if (!resource) Panel(root.transform, "AnswerUnderline", .20f, .32f, .80f, .35f, accent);
        TMP_Text option = Text(root.transform, "Option", .04f, .35f, .96f, resource ? .70f : .96f, 96);
        option.enableAutoSizing = true;
        option.fontSizeMin = 48;
        option.fontSizeMax = 96;
        TMP_Text context = Text(root.transform, "Context", resource ? .16f : .04f, .70f, .96f, .96f, 34);
        context.enableAutoSizing = true;
        context.fontSizeMin = 28;
        context.fontSizeMax = 34;
        var view = root.AddComponent<GateCarrierView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("_optionText").objectReferenceValue = option;
        serialized.FindProperty("_contextText").objectReferenceValue = context;
        if (resource)
        {
            var icons = serialized.FindProperty("_goalIcons");
            icons.arraySize = 3;
            for (int i = 0; i < 3; i++) icons.GetArrayElementAtIndex(i).objectReferenceValue = GoalIcon(root.transform, i, accent);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
        Object.DestroyImmediate(root);
    }

    private static GameObject GoalIcon(Transform parent, int goal, Color accent)
    {
        var icon = Child(parent, goal == 0 ? "MaximizeIcon" : goal == 1 ? "MinimizeIcon" : "TargetIcon", .025f, .73f, .14f, .94f);
        if (goal < 2)
        {
            Panel(icon, "Shaft", .42f, .08f, .58f, .72f, accent);
            var left = Child(icon, "ArrowLeft", .16f, .52f, .56f, .67f);
            left.localEulerAngles = new Vector3(0, 0, 45);
            var a = left.gameObject.AddComponent<Image>(); a.color = accent; a.raycastTarget = false;
            var right = Child(icon, "ArrowRight", .44f, .52f, .84f, .67f);
            right.localEulerAngles = new Vector3(0, 0, -45);
            var b = right.gameObject.AddComponent<Image>(); b.color = accent; b.raycastTarget = false;
            if (goal == 1) icon.localEulerAngles = new Vector3(0, 0, 180);
        }
        else
        {
            Panel(icon, "TargetOuter", .05f, .12f, .95f, .88f, accent);
            Panel(icon, "TargetInner", .20f, .25f, .80f, .75f, new Color(.025f, .04f, .07f));
            Panel(icon, "TargetCenter", .38f, .40f, .62f, .60f, accent);
        }
        icon.gameObject.SetActive(false);
        return icon.gameObject;
    }

    private static RectTransform Child(Transform parent, string name, float x0, float y0, float x1, float y1)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = LayerMask.NameToLayer("Ignore Raycast");
        child.transform.SetParent(parent, false);
        var rect = (RectTransform)child.transform;
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void Panel(Transform parent, string name, float x0, float y0, float x1, float y1, Color color)
    {
        var image = Child(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static TMP_Text Text(Transform parent, string name, float x0, float y0, float x1, float y1, float size)
    {
        var text = Child(parent, name, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = string.Empty;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
