// ProjectBootstrap.cs
// F0-01 — reprodukálható Editor-eszköz a Math Horde Unity-projekt alapbeállításaihoz.
// Cél (math-horde.md 20. szakasz): a projekt alapbeállításait NE kézi Inspector-
// kattintással, hanem egy futtatható, verziózott Editor-szkripttel hozzuk létre,
// hogy bármikor reprodukálható legyen (új gépen, CI-ban, regresszió után).
//
// Használat:
//   Editorból: Math Horde/Bootstrap/Configure All (URP + Android Player Settings)
//   Batch-módból:
//     Unity -batchmode -nographics -quit -projectPath game
//           -executeMethod ProjectBootstrap.ConfigureAll -logFile <path>
//
// Ez a szkript csak projekt-/player-beállításokat ír (URP asset, Graphics/Quality
// Settings, PlayerSettings/Android, color space, aktív build target). Nem hoz létre
// scene-t, prefabot vagy gameplay-kódot — azok az F1 fázis feladatai.

using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ProjectBootstrap
{
    // Az URP-eszközöket az Assets/Settings alá tesszük, NEM az Assets/_Project alá —
    // az _Project/Scripts fába az F0-02 (asmdef-váz) hozza létre a rétegstruktúrát,
    // ebben a feladatban (F0-01) nem definiálunk abban semmit.
    private const string SettingsFolder = "Assets/Settings";
    private const string UrpFolder = SettingsFolder + "/URP";
    private const string UrpAssetPath = UrpFolder + "/MobileURPAsset.asset";
    private const string UrpRendererPath = UrpFolder + "/MobileUniversalRenderer.asset";

    private const string AndroidPackageName = "com.kmarkodotnet.mathhorde";
    private const string ProductName = "Számharcosok";

    // Minimum Android API szint indoklása:
    // Android 7.0 (Nougat, API 24) — bőven az ARM64/IL2CPP minimális küszöbe (API 21)
    // fölött van, de még lefedi a célközönség (7-12 éves gyerekek) olcsóbb/régebbi
    // iskolai és családi tableteit is. API 24 alatt a Play Console 2024+ már amúgy sem
    // enged új appot feltölteni, így ez a gyakorlati minimum is.
    //
    // MEGFIGYELT VISELKEDÉS (dokumentálva, nem feltételezve): ezen a gépen, a telepített
    // NDK r27c mellett, a `-executeMethod ProjectBootstrap.ConfigureAll` futás után a
    // `game/ProjectSettings/ProjectSettings.asset`-ben `AndroidMinSdkVersion: 25` jelenik
    // meg, NEM 24 — a Unity Editor a lenti beállítást (AndroidApiLevel24) a telepített NDK
    // által megkövetelt alsó korlátra (25) emeli fel. Ellenőrizve közvetlenül a
    // ProjectSettings.asset tartalmában (grep "AndroidMinSdkVersion") a futás után.
    // A kódban szándékosan 24 marad rögzítve (a tényleges szándékolt minimum), mert ez
    // a NDK-függő felkerekítés a telepített eszközkészlet frissítésével visszaállhat 24-re.
    private const AndroidSdkVersions MinApiLevel = AndroidSdkVersions.AndroidApiLevel24;

    [MenuItem("Math Horde/Bootstrap/Configure All (URP + Android Player Settings)")]
    public static void ConfigureAll()
    {
        ConfigureActiveBuildTarget();
        UniversalRenderPipelineAsset urpAsset = ConfigureRenderPipeline();
        ConfigurePlayerSettings();
        ConfigureColorSpace();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[ProjectBootstrap] ConfigureAll finished. " +
            $"activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}, " +
            $"urpAsset={(urpAsset != null ? AssetDatabase.GetAssetPath(urpAsset) : "NULL")}, " +
            $"msaa={(urpAsset != null ? urpAsset.msaaSampleCount.ToString() : "-")}, " +
            $"shadowDistance={(urpAsset != null ? urpAsset.shadowDistance.ToString("0.0") : "-")}, " +
            $"supportsHDR={(urpAsset != null ? urpAsset.supportsHDR.ToString() : "-")}, " +
            $"graphicsSettingsDefaultRP={(GraphicsSettings.defaultRenderPipeline != null ? AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline) : "NULL")}, " +
            $"colorSpace={PlayerSettings.colorSpace}, " +
            $"scriptingBackend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}, " +
            $"architectures={PlayerSettings.Android.targetArchitectures}, " +
            $"minSdk={PlayerSettings.Android.minSdkVersion}, " +
            $"targetSdk={PlayerSettings.Android.targetSdkVersion}, " +
            $"orientation={PlayerSettings.defaultInterfaceOrientation}, " +
            $"autorotate(P/PUD/LL/LR)={PlayerSettings.allowedAutorotateToPortrait}/" +
            $"{PlayerSettings.allowedAutorotateToPortraitUpsideDown}/" +
            $"{PlayerSettings.allowedAutorotateToLandscapeLeft}/" +
            $"{PlayerSettings.allowedAutorotateToLandscapeRight}, " +
            $"applicationId={PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)}, " +
            $"productName={PlayerSettings.productName}");
    }

    [MenuItem("Math Horde/Bootstrap/Configure Render Pipeline (URP)")]
    public static UniversalRenderPipelineAsset ConfigureRenderPipeline()
    {
        EnsureFolder(SettingsFolder);
        EnsureFolder(UrpFolder);

        var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
        if (urpAsset == null)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
            }

            // UniversalRenderPipelineAsset.Create(ScriptableRendererData) a package publikus
            // gyári metódusa — ugyanazt hívja a "Create > Rendering > URP Asset (with
            // Universal Renderer)" menüpont is (verifikálva a telepített
            // com.unity.render-pipelines.universal@17.3.0 forrásában).
            urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(urpAsset, UrpAssetPath);
        }

        // Mobil-profil (middle-tier Android eszköz, stabil 30-60 FPS cél — 18. szakasz):
        urpAsset.supportsHDR = false;           // HDR ki — mobilon a bandwidth/hőterhelés csökkentése a fő cél
        urpAsset.msaaSampleCount = 2;            // MSAA 2x — vizuális simítás elfogadható GPU-terhelésért
        urpAsset.shadowDistance = 25f;           // alacsony árnyék-távolság — a harc 3 sávos, közeli nézetű
        urpAsset.renderScale = 1.0f;             // natív felbontás, nincs upscale/downscale alapból

        EditorUtility.SetDirty(urpAsset);

        // Hozzárendelés: Graphics Settings (globális alapértelmezett render pipeline)
        GraphicsSettings.defaultRenderPipeline = urpAsset;

        // Hozzárendelés: Quality Settings minden minőségi szinthez (projekt csak URP-t használ)
        int originalQualityLevel = QualitySettings.GetQualityLevel();
        string[] qualityLevelNames = QualitySettings.names;
        for (int i = 0; i < qualityLevelNames.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
            QualitySettings.renderPipeline = urpAsset;
        }
        QualitySettings.SetQualityLevel(originalQualityLevel, applyExpensiveChanges: false);

        return urpAsset;
    }

    [MenuItem("Math Horde/Bootstrap/Configure Player Settings (Android)")]
    public static void ConfigurePlayerSettings()
    {
        PlayerSettings.productName = ProductName;

        // Scripting backend + architektúra: kizárólag ARM64, IL2CPP (android-build skill)
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // Package name
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageName);

        // API szintek: minimum rögzített (indoklás a MinApiLevel konstansnál),
        // target = "Auto", hogy mindig a telepített legújabb Android SDK platform
        // oldódjon fel build időben (jelenleg 34-37 van telepítve a gépen) anélkül,
        // hogy egy konkrét számot be kellene égetni és kézzel frissíteni kelljen.
        PlayerSettings.Android.minSdkVersion = MinApiLevel;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        // Orientáció: álló (portrait), automatikus elforgatás kikapcsolva
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
    }

    [MenuItem("Math Horde/Bootstrap/Configure Color Space (Linear)")]
    public static void ConfigureColorSpace()
    {
        // Linear color space — az URP mobil ajánlása, és a telepített Android
        // SDK/eszközök (API 24+, ARM64, OpenGLES3/Vulkan-kompatibilis GPU-k) támogatják.
        // Ha a tényleges célhardveren (régi, OpenGLES2-only eszköz) ez problémát okozna,
        // ADR-ben kell rögzíteni a Gamma-ra váltást és az indokot.
        PlayerSettings.colorSpace = ColorSpace.Linear;
    }

    [MenuItem("Math Horde/Bootstrap/Configure Active Build Target (Android)")]
    public static void ConfigureActiveBuildTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(string.IsNullOrEmpty(parent) ? "Assets" : parent, folderName);
    }
}
