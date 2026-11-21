# ANDROID_BUILD.md — Android build-toolchain státusz (F0-04)

Státusz: **a teljes Android build-toolchain ténylegesen lefuttatva és
igazolva ezen a gépen**, nem csak kódszemlézve. Ez a dokumentum az
`android-build` skill kötelező szabálya szerint pontosan rögzíti, mit
sikerült ellenőrizni, milyen paraméterekkel, és mi a lépéssor egy másik
gépen való reprodukáláshoz.

---

## 1. Belépési pontok

| Fájl | Szerep |
|---|---|
| `game/Assets/Editor/BuildScript.cs` | `BuildScript.BuildDevApk`, `BuildScript.BuildReleaseAab` — a tényleges `BuildPipeline.BuildPlayer` hívás |
| `game/scripts/build.ps1` | `-Apk` / `-Aab` kapcsoló, Unity batch-mode hívása, nem-nulla kilépési kód hibánál |
| `game/scripts/test.ps1` | `dotnet test` (gyors kapu) és `-Full`-lal Unity EditMode+PlayMode |

A `make` NEM telepített ezen a gépen — a fenti PowerShell szkriptek a
kanonikus belépési pontok (lásd `game/docs/TESTING.md` 2. szakasza és
`.claude/skills/android-build/SKILL.md`).

## 2. Aláírás

- **Dev APK:** `PlayerSettings.Android.useCustomKeystore = false` — Unity
  automatikus debug keystore-ja. Nincs titok.
- **Release AAB:** `ANDROID_KEYSTORE_PATH`, `ANDROID_KEYSTORE_PASS`,
  `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASS` környezeti változókból — ha
  bármelyik hiányzik, `BuildScript.BuildReleaseAab` `BuildFailedException`-nel
  megszakad, **nem** készít aláírás nélküli csomagot. Ezen a gépen ezek az
  env-változók **nincsenek beállítva** (nincs release keystore ebben a
  fázisban) — ezért a `BuildReleaseAab` útvonal ebben a feladatban **nem lett
  lefuttatva**, csak a hibaág (lásd 4. szakasz).

## 3. Scene-kezelés ebben a fázisban

Az `EditorBuildSettings.scenes` jelenleg üres (F1 feladat hozza majd létre a
végleges gameplay scene-t). A `BuildScript` ezt észleli, és a build idejére
egy ideiglenes placeholder scene-t generál
(`Assets/_Project/_BuildTemp/ToolchainPlaceholderScene.unity`), amit
**kizárólag** a `BuildPlayerOptions.scenes` tömbben hivatkozik — az
`EditorBuildSettings` perzisztens listáját nem módosítja —, majd a build
végén (siker és hiba esetén is) törli. A lenti futásban ez megtörtént;
a build után ellenőrizve: `Assets/_Project/_BuildTemp` nem létezik,
`ProjectSettings/EditorBuildSettings.asset` változatlan (`m_Scenes: []`).

## 4. Ténylegesen lefuttatott, igazolt eredmények

### 4.1 `BuildScript.BuildDevApk` — SIKERES, valódi futás

Parancs (a `game/scripts/build.ps1 -Apk` ezt hívja belsőleg):

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.28f1/Editor/Unity.exe"
"$UNITY" -batchmode -nographics -quit -projectPath "F:\Claude\math-horde\game" \
  -executeMethod BuildScript.BuildDevApk \
  -logFile "F:\Claude\math-horde\game\artifacts\build-apk.log"
```

Futás dátuma: **2026-11-16**, háttérfolyamatként indítva, kilépési kód: `0`.

A log releváns sorai (`game/artifacts/build-apk.log`):

```
[Licensing::Module] License group: ... Product: Unity Personal ... Expiration: Unlimited
[BuildScript] Nincs scene az EditorBuildSettings-ben ... placeholder scene-t használ a build: Assets/_Project/_BuildTemp/ToolchainPlaceholderScene.unity ...
[BuildScript] Build indul. isDevelopmentBuild=True, isAppBundle=False, outputPath=F:/Claude/math-horde/game/Builds/MathHorde-Dev.apk, androidBundleVersionCode=2, bundleVersion=1.0, scenes=[Assets/_Project/_BuildTemp/ToolchainPlaceholderScene.unity], usedPlaceholderScene=True
[BuildScript] Build completed. result=Succeeded, outputPath=F:/Claude/math-horde/game/Builds/MathHorde-Dev.apk, Total build size=1469602577 bytes, totalTime=00:09:57.4728690, totalWarnings=0, totalErrors=0
Exiting batchmode successfully now!
Exiting without the bug reporter. Application will terminate with return code 0
```

**A ténylegesen létrejött fájl** (közvetlenül ellenőrizve `ls -la`-val, nem
csak a log alapján feltételezve):

```
F:\Claude\math-horde\game\Builds\MathHorde-Dev.apk
mérete: 90 821 041 bájt (~86,6 MiB)
```

Megjegyzés a méret-eltérésről: a Unity `BuildReport.summary.totalSize` mezője
(`1 469 602 577` bájt, ~1,37 GiB) nem egyezik a lemezen lévő `.apk` fájl
méretével (90 821 041 bájt). Ez a Unity `BuildReport` dokumentált, ismert
sajátossága — a `totalSize` az aszinkron streaming-asset/intermediate
build-adatok összegét adja vissza, nem a végső, tömörített csomagfájl
méretét. **A hiteles, idézendő méret a ténylegesen a lemezen lévő `.apk`
fájl mérete: 90 821 041 bájt.**

Ezzel igazolva: a teljes lánc — Unity 6000.3.28f1 Editor, C# szkript-fordítás,
URP shader-variáns-fordítás, **IL2CPP** (ARM64) natív fordítás, Android SDK
(build-tools 36.0.0, platform-tools 37.0.1), **NDK r27c**, OpenJDK 17, Gradle
9.3.1 — ténylegesen együtt, hibamentesen lefutott és telepíthető APK-t
állított elő. `totalWarnings=0`, `totalErrors=0`.

**Build-automatizált verziószám-emelés igazolva:** a `ProjectSettings.asset`
`AndroidBundleVersionCode` mezője a futás előtt `1` volt, a futás után `2` —
ezt a `BuildScript.BumpAndroidBuildNumber()` emelte kódból, kézi
szerkesztés nélkül (ez a `ProjectSettings.asset` egy követett, nem
gitignore-olt fájl, úgyhogy ez a változás a repóban is látszódni fog —
ez szándékos, ADR-ben nem kell külön rögzíteni, mert ez pontosan a feladat
által elvárt "automatikus build-szám" viselkedés).

### 4.2 `BuildScript.BuildReleaseAab` — NEM lett lefuttatva (tudatos döntés)

Ezen a gépen nincs release keystore, és az `ANDROID_KEYSTORE_PATH` /
`ANDROID_KEYSTORE_PASS` / `ANDROID_KEY_ALIAS` / `ANDROID_KEY_PASS`
env-változók nincsenek beállítva — ez F0-04-ben szándékos (nincs még
release keystore-döntés, titok soha nem kerül ide kitalálva). A
`BuildScript.ConfigureReleaseSigningFromEnvironmentOrThrow()` és a
`game/scripts/build.ps1 -Aab` env-ellenőrzését **kódszemlézéssel és a hiba-ág
logikájának manuális átolvasásával** ellenőriztem (mind a négy env-változó
hiányát kezeli, egyiket sem hagyja figyelmen kívül, nem indít aláírás
nélküli build-et), de **ténylegesen lefuttatott, sikeres AAB-buildet ez a
feladat nem állít — mert nincs valódi release keystore, amivel ezt
felelősségteljesen meg lehetne tenni.** Amint lesz release keystore (saját
ADR-döntés + CI-secret), a `BuildReleaseAab` ugyanazzal a mechanizmussal
futtatható, mint a dev APK.

## 5. Teljesítmény-/mobiloptimalizációs átvizsgálás (18. szakasz) — ebben a fázisban

A `game/Assets/_Project/Scripts/` alatt jelenleg (F0-02/F0-03 állapot) csak
réteg-asmdef-ek, `_Placeholder.cs` fájlok és egy lokalizációs szolgáltatás
(`DictionaryLocalizationService`, `ILocalizationService`,
`ResourcesLocalizationService`) léteznek — **nincs gameplay-/harci kód,
nincs ellenség, lövedék, effekt, `MonoBehaviour.Update()` harci ciklus.**
Grep-pel ellenőrizve (`Update`, `Instantiate`, `new List`/`new Dictionary`,
`GetComponent`, `FindObjectOf`): a találatok kizárólag a lokalizációs
szolgáltatás egyszeri, konstruktor-idejű `Dictionary`-allokációi, nem
per-frame hot path.

**Következtetés:** object pooling, `Update`-beli allokáció és
draw-call/anyag-szám vizsgálata ebben a fázisban **nem alkalmazható** —
nincs mit auditálni, mert a harci/vizuális kód még nem létezik (F1+
feladat). A `.claude/skills/android-build/SKILL.md` "Teljesítmény-
ellenőrzőlista release előtt" szakasza változatlanul rögzíti az
elvárásokat a jövőbeli auditokhoz; ezt a feladatot nem kellett és nem is
lehetett volna most tartalommal megtölteni hamis pozitívumok nélkül.

## 6. Pontos lépéssor a build reprodukálásához egy másik gépen

Előfeltételek:
- Unity Hub-bal telepített **6000.3.28f1** Editor, Android Build Support
  modullal (OpenJDK, NDK, Android SDK komponensek a modul részeként).
- Windows + PowerShell (vagy a megfelelő shell), a repó klónozva.

Lépések:

```powershell
cd game

# 1. Gyors kapu (Unity nélkül)
./scripts/test.ps1

# 2. Fejlesztői APK (toolchain-ellenőrzés)
./scripts/build.ps1 -Apk
#   -> Builds/MathHorde-Dev.apk, log: artifacts/build-apk.log

# 3. Release AAB — CSAK ha van valódi release keystore:
$env:ANDROID_KEYSTORE_PATH = "C:\elérési\út\release.keystore"
$env:ANDROID_KEYSTORE_PASS = "<titok, soha ne írd fájlba>"
$env:ANDROID_KEY_ALIAS     = "<alias>"
$env:ANDROID_KEY_PASS      = "<titok, soha ne írd fájlba>"
./scripts/build.ps1 -Aab
#   -> Builds/MathHorde-Release.aab, log: artifacts/build-aab.log

# 4. Teljes teszt-kapu (Unity EditMode + PlayMode is)
./scripts/test.ps1 -Full
```

Ha más gépen más útvonalon van a Unity Editor telepítve:

```powershell
./scripts/build.ps1 -Apk -UnityPath "D:\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe"
```

Hibakeresés: a szkriptek nem-nulla kilépési kóddal állnak le hiba esetén, és
a konzolra kiemelik a log `error|Exception|BuildFailedException` mintájú
sorainak utolsó részét; a teljes log mindig megmarad az `artifacts/`
mappában.

## F1-20 helyi Android smoke-build — 2026-11-21

Parancs a gyár gyökeréből:
`powershell -NoProfile -ExecutionPolicy Bypass -File game/scripts/build.ps1 -Apk -LogDir F:/Claude/math-horde/game/artifacts/dev/F1-20-ea3260f9b73c/android`.
A rögzített Unity 6000.3.28f1 fejlesztői build sikeres. A friss
`Builds/MathHorde-Dev.apk` **96 126 045 bájt**, módosítási ideje
2026-11-21 12:57:30 (helyi idő), a build kezdete utáni. Az archívumban kizárólag
`arm64-v8a` ABI és `libil2cpp.so` található. A buildszkript automatikusan
4→5-re emelte az `AndroidBundleVersionCode` értékét; nincs kézi verzióemelés.
SHA-256: `2d7affde25e7de0348351a70ee981e305a1aecad5c84589196514cc56402a4d6`.
Napló és archívum-ellenőrzés: `artifacts/dev/F1-20-ea3260f9b73c/android/`.

A telepített SDK `adb devices` kimenete üres, ezért fizikai Android-eszközös
FPS/érintés/szín/olvashatóság ellenőrzés nem történt. A K kapu manuális része
a teljes math scene-kompozícióval együtt marad: csatlakoztatott ARM64 eszközön
`adb install -r Builds/MathHorde-Dev.apk`, majd indítás, tartós tüzelés,
Health/Shield kijelzés, sávinput és pause/resume ellenőrzése portrait módban.
A fegyverválasztó gombok csak F1-21 után ellenőrizhetők; F3-12 teljes
math-bekötése és coverage-e még nyitott. Release AAB/publikálás nem történt.
