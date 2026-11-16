// BuildScript.cs
// F0-04 — batch-módból hívható Android build belépési pontok (android-build skill).
//
// Cél: a Unity Editor GUI nélkül, `-batchmode -executeMethod`-dal reprodukálhatóan
// előállítható fejlesztői APK és Play-kompatibilis AAB, hogy a toolchain
// (Android SDK/NDK/JDK/IL2CPP/Gradle) helyessége korán, scriptből igazolható legyen,
// ne csak a SHIP fázisban.
//
// Használat (a Unity projekt a repó `game/` mappájában van, ADR-002):
//   Fejlesztői APK:
//     Unity -batchmode -nographics -quit -projectPath game \
//           -executeMethod BuildScript.BuildDevApk -logFile <path>
//   Release AAB (aláírás kizárólag env-változóból, lásd lent):
//     ANDROID_KEYSTORE_PATH=... ANDROID_KEYSTORE_PASS=... \
//     ANDROID_KEY_ALIAS=... ANDROID_KEY_PASS=... \
//     Unity -batchmode -nographics -quit -projectPath game \
//           -executeMethod BuildScript.BuildReleaseAab -logFile <path>
//
// Aláírás:
//   - Dev APK: Unity automatikus debug keystore-ja (useCustomKeystore = false).
//     Nincs külön titok, nincs env-változó igény.
//   - Release AAB: a keystore elérési út, alias és jelszavak KIZÁRÓLAG a
//     ANDROID_KEYSTORE_PATH / ANDROID_KEYSTORE_PASS / ANDROID_KEY_ALIAS /
//     ANDROID_KEY_PASS környezeti változókból jönnek. Ha bármelyik hiányzik,
//     a metódus BuildFailedException-nel áll le, és NEM készít aláírás
//     nélküli (vagy repóba írt kulcsú) release csomagot.
//
// Verziószám/buildszám: a `PlayerSettings.Android.bundleVersionCode` minden
// build hívásnál automatikusan +1-gyel nő (ProjectSettings.asset-be mentve
// AssetDatabase.SaveAssets()-szel) — ez a build-azonosító, kézzel soha nem
// szerkesztendő. A felhasználó-arcú verziónév (`PlayerSettings.bundleVersion`,
// jelenleg "1.0", F0-01) ennek a feladatnak NEM a hatásköre — azt egy
// jövőbeli release-döntés (ADR) emeli, nem a build script minden futása.
//
// Scene-kezelés: ebben a fázisban (F0-04) még nincs végleges gameplay scene
// (az Assets/Scenes/ üres, az EditorBuildSettings.scenes üres — ez F1
// feladat). Hogy a toolchain-ellenőrzés build amúgy lefusson, a szkript egy
// ideiglenes, generált placeholder scene-t hoz létre futásidőben
// (Assets/_Project/_BuildTemp/ToolchainPlaceholderScene.unity), KIZÁRÓLAG a
// `BuildPlayerOptions.scenes` tömbben hivatkozza (az EditorBuildSettings
// perzisztens listáját NEM módosítja), és a build végén (siker és hiba esetén
// is, `finally`-ben) törli az asset-et. Ha az EditorBuildSettings.scenes
// később (F1-ben) feltöltődik, a szkript automatikusan azt használja, és a
// placeholder-ág nem aktiválódik.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildScript
{
    private const string BuildsFolder = "Builds";
    private const string DevApkRelativePath = BuildsFolder + "/MathHorde-Dev.apk";
    private const string ReleaseAabRelativePath = BuildsFolder + "/MathHorde-Release.aab";

    private const string TempSceneParentFolder = "Assets/_Project";
    private const string TempSceneFolderName = "_BuildTemp";
    private const string TempSceneFolder = TempSceneParentFolder + "/" + TempSceneFolderName;
    private const string TempScenePath = TempSceneFolder + "/ToolchainPlaceholderScene.unity";

    private const string EnvKeystorePath = "ANDROID_KEYSTORE_PATH";
    private const string EnvKeystorePass = "ANDROID_KEYSTORE_PASS";
    private const string EnvKeyAlias = "ANDROID_KEY_ALIAS";
    private const string EnvKeyPass = "ANDROID_KEY_PASS";

    /// <summary>
    /// Fejlesztői APK: Development build, Unity debug keystore-jával aláírva.
    /// Nincs külön aláírási konfiguráció / titok.
    /// </summary>
    public static void BuildDevApk()
    {
        BuildInternal(isDevelopmentBuild: true, isAppBundle: false, outputRelativePath: DevApkRelativePath);
    }

    /// <summary>
    /// Play Console-kompatibilis AAB. Az aláírás kizárólag környezeti
    /// változókból jön (lásd a fájl tetején) — hiányuk esetén a metódus
    /// BuildFailedException-nel megszakítja a build-et, NEM készít
    /// aláírás nélküli csomagot.
    /// </summary>
    public static void BuildReleaseAab()
    {
        BuildInternal(isDevelopmentBuild: false, isAppBundle: true, outputRelativePath: ReleaseAabRelativePath);
    }

    private static void BuildInternal(bool isDevelopmentBuild, bool isAppBundle, string outputRelativePath)
    {
        EnsureAndroidActiveBuildTarget();

        try
        {
            string[] scenes = ResolveScenesForBuild(out bool usedPlaceholderScene);

            if (isAppBundle)
            {
                ConfigureReleaseSigningFromEnvironmentOrThrow();
            }
            else
            {
                // Dev build: mindig az alapértelmezett Unity debug keystore-t
                // használjuk, soha nem örököljük egy korábbi lokális futás
                // esetleg beállított custom keystore-ját.
                PlayerSettings.Android.useCustomKeystore = false;
            }

            EditorUserBuildSettings.buildAppBundle = isAppBundle;

            int newBuildCode = BumpAndroidBuildNumber();
            string fullOutputPath = PrepareOutputPath(outputRelativePath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullOutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = isDevelopmentBuild
                    ? (BuildOptions.Development | BuildOptions.AllowDebugging)
                    : BuildOptions.None
            };

            Debug.Log(
                "[BuildScript] Build indul. " +
                $"isDevelopmentBuild={isDevelopmentBuild}, isAppBundle={isAppBundle}, " +
                $"outputPath={fullOutputPath}, androidBundleVersionCode={newBuildCode}, " +
                $"bundleVersion={PlayerSettings.bundleVersion}, " +
                $"scenes=[{string.Join(", ", scenes)}], usedPlaceholderScene={usedPlaceholderScene}");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            LogReportSummaryOrThrow(report, fullOutputPath);
        }
        finally
        {
            CleanupPlaceholderSceneIfPresent();
        }
    }

    private static void EnsureAndroidActiveBuildTarget()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
        {
            return;
        }

        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        if (!switched)
        {
            throw new BuildFailedException(
                "[BuildScript] Nem sikerült az active build targetet Androidra váltani.");
        }
    }

    private static string[] ResolveScenesForBuild(out bool usedPlaceholderScene)
    {
        string[] configuredScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (configuredScenes.Length > 0)
        {
            usedPlaceholderScene = false;
            return configuredScenes;
        }

        usedPlaceholderScene = true;
        return new[] { CreatePlaceholderScene() };
    }

    private static string CreatePlaceholderScene()
    {
        if (!AssetDatabase.IsValidFolder(TempSceneFolder))
        {
            AssetDatabase.CreateFolder(TempSceneParentFolder, TempSceneFolderName);
        }

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        bool saved = EditorSceneManager.SaveScene(scene, TempScenePath);
        if (!saved)
        {
            throw new BuildFailedException(
                $"[BuildScript] Nem sikerült elmenteni az ideiglenes placeholder scene-t: {TempScenePath}");
        }

        Debug.LogWarning(
            "[BuildScript] Nincs scene az EditorBuildSettings-ben (F1 feladat még nem hozta létre a " +
            "végleges gameplay scene-t). A toolchain-ellenőrzés kedvéért egy ideiglenes, generált " +
            $"placeholder scene-t használ a build: {TempScenePath}. Ez NEM végleges scene, a build " +
            "végén automatikusan törlődik, és soha nem kerül az Assets/Scenes/ véglegesei közé.");

        return TempScenePath;
    }

    private static void CleanupPlaceholderSceneIfPresent()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TempScenePath) != null)
        {
            AssetDatabase.DeleteAsset(TempScenePath);
        }

        if (AssetDatabase.IsValidFolder(TempSceneFolder))
        {
            string[] remaining = AssetDatabase.FindAssets(string.Empty, new[] { TempSceneFolder });
            if (remaining.Length == 0)
            {
                AssetDatabase.DeleteAsset(TempSceneFolder);
            }
        }

        AssetDatabase.Refresh();
    }

    private static void ConfigureReleaseSigningFromEnvironmentOrThrow()
    {
        string keystorePath = Environment.GetEnvironmentVariable(EnvKeystorePath);
        string keystorePass = Environment.GetEnvironmentVariable(EnvKeystorePass);
        string keyAlias = Environment.GetEnvironmentVariable(EnvKeyAlias);
        string keyPass = Environment.GetEnvironmentVariable(EnvKeyPass);

        var missing = new List<string>();
        if (string.IsNullOrEmpty(keystorePath)) missing.Add(EnvKeystorePath);
        if (string.IsNullOrEmpty(keystorePass)) missing.Add(EnvKeystorePass);
        if (string.IsNullOrEmpty(keyAlias)) missing.Add(EnvKeyAlias);
        if (string.IsNullOrEmpty(keyPass)) missing.Add(EnvKeyPass);

        if (missing.Count > 0)
        {
            throw new BuildFailedException(
                "[BuildScript] BuildReleaseAab megszakítva: hiányzó környezeti változó(k): " +
                string.Join(", ", missing) +
                ". Release AAB SOHA nem készül aláírás nélkül, és a kulcsadatok SOHA nem kerülnek " +
                "repóba. Állítsd be ANDROID_KEYSTORE_PATH / ANDROID_KEYSTORE_PASS / ANDROID_KEY_ALIAS / " +
                "ANDROID_KEY_PASS környezeti változókat (lokális shell env vagy CI secret), majd futtasd " +
                "újra a build-et.");
        }

        if (!File.Exists(keystorePath))
        {
            throw new BuildFailedException(
                $"[BuildScript] BuildReleaseAab megszakítva: ANDROID_KEYSTORE_PATH ('{keystorePath}') " +
                "nem létező fájlra mutat.");
        }

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystorePath;
        PlayerSettings.Android.keystorePass = keystorePass;
        PlayerSettings.Android.keyaliasName = keyAlias;
        PlayerSettings.Android.keyaliasPass = keyPass;
    }

    private static int BumpAndroidBuildNumber()
    {
        int next = PlayerSettings.Android.bundleVersionCode + 1;
        PlayerSettings.Android.bundleVersionCode = next;
        AssetDatabase.SaveAssets();
        return next;
    }

    private static string PrepareOutputPath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string fullPath = Path.Combine(projectRoot, relativePath).Replace('\\', '/');

        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return fullPath;
    }

    private static void LogReportSummaryOrThrow(BuildReport report, string outputPath)
    {
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                "[BuildScript] Build FAILED. " +
                $"result={summary.result}, totalErrors={summary.totalErrors}, outputPath={outputPath}. " +
                "Lásd a fenti Unity logot (-logFile) a részletes hibaüzenetért.");
        }

        Debug.Log(
            "[BuildScript] Build completed. " +
            $"result={summary.result}, outputPath={outputPath}, " +
            $"Total build size={summary.totalSize} bytes, " +
            $"totalTime={summary.totalTime}, totalWarnings={summary.totalWarnings}, " +
            $"totalErrors={summary.totalErrors}");
    }
}
