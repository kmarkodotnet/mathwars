// PlayerPrefabBuilder.cs
// F1-17 — reprodukálható Editor-eszköz a játékos egyszerű, gyermekbarát
// avatarjához (math-horde.md 20. szakasz: ne kézi Inspector-összeállítással;
// 23. szakasz: semmi vér/brutalitás/realisztikus katonai látvány). F1-13
// jelezte, hogy a játékos Transform-mozgatása sávváltáskor nem volt
// megoldható, mert "nincs még Player-prefab/view a projektben" — ez a fájl
// pótolja azt, az ArenaBuilder/EnemyPrefabBuilder mintáját követve:
// primitív geometria egy "Visual" gyermekben, logikai gyökéren
// (PlayerController + PlayerView) kívül, hogy később grafikai assetre
// cserélhető legyen a scriptek módosítása nélkül.
//
// Forma: kerekded, barátságos "őrbot" — kapszulatörzs, gömbfej, két apró
// "szem", és egy felfelé álló, nem fegyver jellegű "jelzőantenna" (kizárólag
// dekoratív, hogy a karakter egyértelműen megkülönböztethető legyen az
// ellenségektől, math-horde.md 3.1 "vizuálisan jól elkülöníthető").
// Élénk, meleg kék/sárga páros — tudatosan NEM ugyanaz a paletta, mint az
// EnemyPrefabBuilder ellenség-színei.
//
// Használat:
//   Editorból: Math Horde/Player/Build Player Avatar Prefab
//   Batch-módból:
//     Unity -batchmode -nographics -quit -projectPath game
//           -executeMethod PlayerPrefabBuilder.BuildPlayerAvatar -logFile <path>
using System.IO;
using UnityEditor;
using UnityEngine;
using MathHorde.Presentation.Player;

public static class PlayerPrefabBuilder
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/Player";
    private const string PrefabPath = PrefabFolder + "/PlayerAvatar.prefab";

    private const string MaterialFolder = "Assets/_Project/Materials/Player";

    private static readonly Color BodyColor = new Color(0.20f, 0.55f, 0.95f);
    private static readonly Color AccentColor = new Color(0.99f, 0.82f, 0.20f);
    private static readonly Color EyeColor = new Color(0.95f, 0.97f, 0.99f);

    [MenuItem("Math Horde/Player/Build Player Avatar Prefab")]
    public static GameObject BuildPlayerAvatar()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        var root = new GameObject("PlayerAvatar");
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(visual.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.8f, 0.95f, 0.8f);
        ApplyMaterial(body, "Mat_Player_Body", BodyColor);
        RemoveCollider(body);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(visual.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
        head.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
        ApplyMaterial(head, "Mat_Player_Head", BodyColor);
        RemoveCollider(head);

        BuildEye("EyeL", visual.transform, new Vector3(-0.16f, 1.9f, 0.28f));
        BuildEye("EyeR", visual.transform, new Vector3(0.16f, 1.9f, 0.28f));

        GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        antenna.name = "Antenna";
        antenna.transform.SetParent(visual.transform, false);
        antenna.transform.localPosition = new Vector3(0f, 2.35f, 0f);
        antenna.transform.localScale = new Vector3(0.05f, 0.18f, 0.05f);
        ApplyMaterial(antenna, "Mat_Player_Antenna", AccentColor, unlit: true);
        RemoveCollider(antenna);

        GameObject antennaTip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        antennaTip.name = "AntennaTip";
        antennaTip.transform.SetParent(visual.transform, false);
        antennaTip.transform.localPosition = new Vector3(0f, 2.55f, 0f);
        antennaTip.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
        ApplyMaterial(antennaTip, "Mat_Player_AntennaTip", AccentColor, unlit: true);
        RemoveCollider(antennaTip);

        // Logikai gyökéren a két Presentation-komponens: PlayerController
        // (parancs-továbbítás, F1-13) és PlayerView (lane-váltás Transform-
        // mozgatása, F1-17) — mindkettő Initialize(...)-ra vár a scene
        // composition root-tól (GameplaySessionBootstrap), itt csak felkerül
        // a prefabra.
        root.AddComponent<PlayerController>();
        root.AddComponent<PlayerView>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PlayerPrefabBuilder] BuildPlayerAvatar finished. prefab={PrefabPath}");
        return prefab;
    }

    private static void BuildEye(string name, Transform parent, Vector3 localPos)
    {
        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = name;
        eye.transform.SetParent(parent, false);
        eye.transform.localPosition = localPos;
        eye.transform.localScale = Vector3.one * 0.13f;
        ApplyMaterial(eye, "Mat_Player_Eye", EyeColor, unlit: true);
        RemoveCollider(eye);
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }

    private static void ApplyMaterial(GameObject go, string materialName, Color color, bool unlit = false)
    {
        Shader shader = unlit ? ResolveUnlitShader() : ResolveLitShader();
        Material mat = CreateOrUpdateMaterial($"{MaterialFolder}/{materialName}.mat", shader, color);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static Shader ResolveLitShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Simple Lit")
            ?? Shader.Find("Standard");
    }

    private static Shader ResolveUnlitShader()
    {
        return Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color");
    }

    private static Material CreateOrUpdateMaterial(string path, Shader shader, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(string.IsNullOrEmpty(parent) ? "Assets" : parent, folderName);
    }
}
