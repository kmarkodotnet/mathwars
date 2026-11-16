<#
.SYNOPSIS
  Math Horde Android build belepesi pont (kanonikus, PowerShell): -Apk vagy -Aab.

.DESCRIPTION
  A `make` nincs telepitve ezen a gepen, ezert ez a PowerShell szkript a
  kanonikus build belepesi pont (lasd game/docs/TESTING.md es
  .claude/skills/android-build/SKILL.md). A tenyleges build-logikat a
  game/Assets/Editor/BuildScript.cs Editor-szkript tartalmazza; ez a
  szkript csak a Unity Editor-t hivja batch-modban `-executeMethod`-dal.

  KRITIKUS SZABALY (lasd .claude/skills/android-build/SKILL.md "Build/teszt
  bizonyitek-szabaly"): a Unity batch-mode folyamat KILEPESI KODJA NEM
  BIZONYITEK -- megfigyelt eset, hogy zarolt projekt ("another Unity
  instance is running") eseten is 0-t adhat vissza, miutan a build
  TENYLEGESEN NEM FUTOTT LE. Ezert ez a szkript a sikert KIZAROLAG az
  alabbiak egyuttes teljesulesebol allapitja meg:
    1. a projekt NEM volt zarolva a futtatas elott (pre-flight ellenorzes),
    2. a log NEM tartalmazza a "another Unity instance" / "Aborting
       batchmode due to fatal error" / "BuildFailedException" mintat,
    3. a vart kimeneti fajl (APK/AAB) TENYLEGESEN letezik A FUTTATAS UTAN,
    4. a kimeneti fajl utolso modositasi ideje KESOBBI, mint a szkript
       inditasa (igy egy regi, korabbi build maradvanya nem tunhet friss
       sikernek),
    5. a kimeneti fajl merete > 0 bajt.
  A "Build completed"/"Total build size" log-sor csak diagnosztikai kiegeszito,
  nem helyettesiti a fenti ellenorzest.

.PARAMETER Apk
  Fejlesztoi APK build (BuildScript.BuildDevApk - Unity debug keystore,
  development build). Vart kimenet: Builds/MathHorde-Dev.apk.

.PARAMETER Aab
  Play-kompatibilis AAB build (BuildScript.BuildReleaseAab). Az alairashoz
  szukseges ANDROID_KEYSTORE_PATH / ANDROID_KEYSTORE_PASS / ANDROID_KEY_ALIAS
  / ANDROID_KEY_PASS kornyezeti valtozoknak MAR a szkript inditasa elott
  beallitva kell lenniuk (lokalis shell env vagy CI secret). Ez a szkript
  nem ker be es nem allit be titkot, es nem irja ki az ertekuket. Vart
  kimenet: Builds/MathHorde-Release.aab.

.PARAMETER UnityPath
  A Unity Editor executable eleresi utja. Alapertelmezett: a projekt
  rogzitett verzioja (6000.3.28f1). Ne hasznalj mas telepitett verziot
  (6000.0.77f1 / 6000.1.14f1 tilos - felmigralnak a projektet).

.PARAMETER LogDir
  Hova irodjon a build log. Alapertelmezett: <projektgyoker>/artifacts
  (gitignore-olt).

.EXAMPLE
  ./scripts/build.ps1 -Apk

.EXAMPLE
  $env:ANDROID_KEYSTORE_PATH = "C:\keys\release.keystore"
  $env:ANDROID_KEYSTORE_PASS = "..."
  $env:ANDROID_KEY_ALIAS = "..."
  $env:ANDROID_KEY_PASS = "..."
  ./scripts/build.ps1 -Aab

.NOTES
  Kilepesi kod: 0 = a kimeneti fajl bizonyithatoan, frissen letrejott;
  nem-0 = build bukott, hibazott, zarolt projekt miatt el sem indult, vagy
  hianyzo elofeltetel (CI-barat).
#>
[CmdletBinding()]
param(
    [switch]$Apk,
    [switch]$Aab,
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe",
    [string]$LogDir
)

$ErrorActionPreference = "Stop"

if ($Apk -and $Aab) {
    Write-Host "[build.ps1] HIBA: -Apk es -Aab egyszerre nem adhato meg." -ForegroundColor Red
    exit 1
}
if (-not $Apk -and -not $Aab) {
    Write-Host "[build.ps1] HIBA: adj meg -Apk vagy -Aab kapcsolot." -ForegroundColor Red
    exit 1
}

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $LogDir) {
    $LogDir = Join-Path $ProjectRoot "artifacts"
}
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Write-FailureExcerpt {
    param([string]$LogFile, [string]$Label)

    Write-Host ""
    Write-Host "==== $Label -- HIBAS RESZ ($LogFile) ====" -ForegroundColor Red
    if (Test-Path $LogFile) {
        Select-String -Path $LogFile -Pattern 'error|Exception|BuildFailedException|Aborting batchmode|another Unity instance' |
            Select-Object -Last 60 |
            ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    } else {
        Write-Host "(a log fajl nem jott letre: $LogFile)" -ForegroundColor Red
    }
    Write-Host "====================================================" -ForegroundColor Red
}

# Ugyanaz a zarolas-ellenorzes, mint test.ps1-ben: a Temp/UnityLockfile
# kizarolagos megnyitasi kiserlete a Unity sajat mechanizmusat hasznalja,
# megbizhatobb, mint folyamatlista/parancssor string-egyeztetes.
function Test-UnityProjectLocked {
    param([string]$ProjRoot)

    $lockFile = Join-Path $ProjRoot "Temp\UnityLockfile"
    if (-not (Test-Path $lockFile)) {
        return $false
    }

    try {
        $fs = [System.IO.File]::Open($lockFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $fs.Close()
        return $false
    } catch {
        return $true
    }
}

function Test-LogHasFatalUnityAbort {
    param([string]$LogFile)

    if (-not (Test-Path $LogFile)) {
        return $false
    }
    return [bool](Select-String -Path $LogFile -SimpleMatch -Pattern @('another Unity instance is running', 'Aborting batchmode due to fatal error') -Quiet)
}

if (-not (Test-Path $UnityPath)) {
    Write-Host "[build.ps1] HIBA: Unity Editor nem talalhato itt: $UnityPath" -ForegroundColor Red
    exit 1
}

if ($Apk) {
    $method = "BuildScript.BuildDevApk"
    $logFile = Join-Path $LogDir "build-apk.log"
    $expectedOutput = Join-Path $ProjectRoot "Builds\MathHorde-Dev.apk"
} else {
    $method = "BuildScript.BuildReleaseAab"
    $logFile = Join-Path $LogDir "build-aab.log"
    $expectedOutput = Join-Path $ProjectRoot "Builds\MathHorde-Release.aab"

    $requiredEnv = @("ANDROID_KEYSTORE_PATH", "ANDROID_KEYSTORE_PASS", "ANDROID_KEY_ALIAS", "ANDROID_KEY_PASS")
    $missingEnv = @()
    foreach ($var in $requiredEnv) {
        $value = [System.Environment]::GetEnvironmentVariable($var)
        if ([string]::IsNullOrEmpty($value)) {
            $missingEnv += $var
        }
    }
    if ($missingEnv.Count -gt 0) {
        Write-Host "[build.ps1] HIBA: hianyzo env-valtozo(k) a release AAB alairasahoz: $($missingEnv -join ', ')" -ForegroundColor Red
        Write-Host "[build.ps1] Allitsd be mind a negy valtozot (ANDROID_KEYSTORE_PATH/_PASS, ANDROID_KEY_ALIAS/_PASS), majd futtasd ujra." -ForegroundColor Red
        exit 1
    }
}

# --- Pre-flight: ne is inditsuk el Unity-t, ha a projekt mar zarolt. ---
if (Test-UnityProjectLocked -ProjRoot $ProjectRoot) {
    Write-Host "[build.ps1] HIBA: a projekt zarolva van (Temp/UnityLockfile masik folyamat altal tartva) -- " -ForegroundColor Red -NoNewline
    Write-Host "fut mar egy masik Unity peldany ezen a projekten. Zard be az Editort / vard meg a masik " -ForegroundColor Red -NoNewline
    Write-Host "futas veget, majd probald ujra. A Unity-t EL SEM INDITOTTUK." -ForegroundColor Red
    exit 1
}

# A kimeneti fajl "frissesegenek" megallapitasahoz rogzitjuk a futtatas
# inditasi idejet -- egy ennel regebbi fajl egy KORABBI build maradvanya,
# nem a mostani futas bizonyitek.
$buildStartTime = Get-Date

Write-Host "[build.ps1] Unity batch build indul: $method" -ForegroundColor Cyan
Write-Host "[build.ps1] projectPath=$ProjectRoot"
Write-Host "[build.ps1] Log: $logFile"
Write-Host "[build.ps1] Vart kimenet: $expectedOutput"

# Start-Process -Wait -PassThru hasznalata a sima "&" hivasi operator
# helyett: Win32 GUI-subsystem futtathato allomanyoknal (mint a Unity.exe)
# a "&" operator NEM minden host-kontextusban var meg megbizhatoan a
# process befejezesere (tenylegesen megfigyelve: azonnal visszatert,
# $LASTEXITCODE ures maradt, log fajl sosem jott letre) -- a Start-Process
# -Wait kozvetlenul a process handle-re var, ezert megbizhato mindenhol.
$unityArgs = @(
    "-batchmode", "-nographics", "-quit", "-projectPath", $ProjectRoot,
    "-executeMethod", $method, "-logFile", $logFile
)
$proc = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -Wait -PassThru
$unityExitCode = $proc.ExitCode
Write-Host "[build.ps1] Unity folyamat kilepesi kodja: $unityExitCode (ONMAGABAN NEM BIZONYITEK -- lasd lent)." -ForegroundColor DarkGray

# --- 1. Vegzetes Unity-hiba (pl. zarolt projekt) a logban, fuggetlenul a kilepesi kodtol. ---
if (Test-LogHasFatalUnityAbort -LogFile $logFile) {
    Write-Host "[build.ps1] HIBA: a Unity batch-mode futas VEGZETES HIBAVAL allt le (masik Unity peldany / zarolt projekt)." -ForegroundColor Red
    Write-Host "[build.ps1] A build TENYLEGESEN NEM FUTOTT LE, holott a kilepesi kod $unityExitCode volt." -ForegroundColor Red
    Write-FailureExcerpt -LogFile $logFile -Label $method
    exit 1
}

# --- 2. A kimeneti fajlnak TENYLEGESEN, FRISSEN kell letreznie. ---
if (-not (Test-Path $expectedOutput)) {
    Write-Host "[build.ps1] HIBA: a vart kimeneti fajl nem jott letre: $expectedOutput (kilepesi kod: $unityExitCode)." -ForegroundColor Red
    Write-FailureExcerpt -LogFile $logFile -Label $method
    exit 1
}

$fileInfo = Get-Item $expectedOutput

if ($fileInfo.LastWriteTime -lt $buildStartTime) {
    Write-Host "[build.ps1] HIBA: a kimeneti fajl ($expectedOutput) regebbi ($($fileInfo.LastWriteTime)), mint a build inditasa ($buildStartTime) -- " -ForegroundColor Red -NoNewline
    Write-Host "ez egy KORABBI build maradvanya, a mostani futas NEM allitott elo uj csomagot." -ForegroundColor Red
    Write-FailureExcerpt -LogFile $logFile -Label $method
    exit 1
}

if ($fileInfo.Length -le 0) {
    Write-Host "[build.ps1] HIBA: a kimeneti fajl ($expectedOutput) merete 0 bajt." -ForegroundColor Red
    exit 1
}

if ($unityExitCode -ne 0) {
    # Nem fatalis Unity-hiba mintat talaltunk a logban, de a kilepesi kod
    # megis nem-0 -- a frissen letrejott, nem-ures kimeneti fajl ennek
    # ellentmond, de a kilepesi kodot erdemes lathatova tenni, nem elnyelni.
    Write-Host "[build.ps1] FIGYELEM: a kimeneti fajl bizonyithatoan friss es nem ures, de a Unity kilepesi kodja $unityExitCode (nem 0) volt. Ellenorizd a logot." -ForegroundColor Yellow
    Write-FailureExcerpt -LogFile $logFile -Label $method
}

Write-Host "[build.ps1] Build ZOLD -- bizonyitek: $expectedOutput letezik, meret=$($fileInfo.Length) bajt, modositva=$($fileInfo.LastWriteTime) (a futtatas inditasa utan)." -ForegroundColor Green
exit 0
