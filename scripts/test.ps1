<#
.SYNOPSIS
  Math Horde teszt-belepesi pont (kanonikus, PowerShell).

.DESCRIPTION
  A `make` nincs telepitve ezen a gepen, ezert ez a PowerShell szkript a
  kanonikus teszt-belepesi pont (lasd game/docs/TESTING.md 2. szakasz es
  .claude/skills/android-build/SKILL.md). Alapertelmezetten csak a gyors
  `dotnet test` kaput futtatja (Unity Editor nelkul); a -Full kapcsolo
  hozzaadja a teljes Unity Test Framework EditMode + PlayMode batch-mode
  koret is.

  KRITIKUS SZABALY (lasd .claude/skills/android-build/SKILL.md "Build/teszt
  bizonyitek-szabaly"): a Unity batch-mode folyamat KILEPESI KODJA NEM
  BIZONYITEK. Megfigyelt, dokumentalt hiba: ha a projekt mar zarolva van
  (masik Unity peldany/build fut rajta), a Unity batch-mode futas ugy
  "Aborting batchmode due to fatal error: It looks like another Unity
  instance is running..." uzenettel all le, hogy a folyamat kilepesi kodja
  MEGIS 0 lehet -- es a teszt TENYLEGESEN NEM FUT LE, eredmeny-XML sem
  jon letre. Ezert ez a szkript a sikert KIZAROLAG az alabbiak egyuttes
  teljesulesebol allapitja meg, a kilepesi kodtol fuggetlenul:
    1. a projekt NEM volt zarolva a futtatas elott (pre-flight ellenorzes),
    2. a log NEM tartalmazza a "another Unity instance" / "Aborting
       batchmode due to fatal error" mintat,
    3. az eredmeny-XML TENYLEGESEN letrejott (a futtatas elott torolve
       lett egy esetleges regi peldany, hogy az ne tunjon friss sikernek),
    4. az XML gyoker <test-run> elemenek result="Passed" ES failed="0",
    5. total > 0 -- egy 0 tesztet futtato kor SOHA nem "ZOLD", hanem
       kulon, lathato FIGYELMEZTETES stilusban jelenik meg.

.PARAMETER Full
  A dotnet test mellett lefuttatja a Unity EditMode es PlayMode batch-mode
  tesztkoret is.

.PARAMETER UnityPath
  A Unity Editor executable eleresi utja. Alapertelmezett: a projekt
  rogzitett verzioja (6000.3.28f1). Ne hasznalj mas telepitett verziot
  (6000.0.77f1 / 6000.1.14f1 tilos, lasd android-build skill - felmigralnak
  a projektet).

.PARAMETER LogDir
  Hova irodjanak a log- es eredmenyfajlok. Alapertelmezett:
  <projektgyoker>/artifacts (gitignore-olt).

.EXAMPLE
  ./scripts/test.ps1
  Csak a gyors dotnet test kaput futtatja.

.EXAMPLE
  ./scripts/test.ps1 -Full
  dotnet test + Unity EditMode + Unity PlayMode, eredmeny-XML alapu
  ellenorzessel.

.NOTES
  Kilepesi kod: 0 = nincs FAILED/ERROR statuszu lepes (lehet WARNING, pl.
  0 tesztet futtato mod); nem-0 = legalabb egy lepes ténylegesen bukott
  vagy hibazott (CI-barat - a hivo pipeline ebbol allithatja meg a kaput).
  A futas vegen mindig megjelenik egy OSSZEGZES blokk lepesenkenti
  statusszal es teszt-szamokkal.
#>
[CmdletBinding()]
param(
    [switch]$Full,
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe",
    [string]$LogDir
)

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $LogDir) {
    $LogDir = Join-Path $ProjectRoot "artifacts"
}
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

# Lepesenkenti osszegzo lista -- a vegen egy tablazatban jelenik meg.
$script:steps = @()

function Add-Step {
    param(
        [string]$Name,
        [ValidateSet("PASSED", "WARNING", "FAILED", "ERROR", "SKIPPED")]
        [string]$Status,
        [Nullable[int]]$Total = $null,
        [Nullable[int]]$Passed = $null,
        [Nullable[int]]$Failed = $null,
        [string]$Detail = ""
    )
    $script:steps += [PSCustomObject]@{
        Name   = $Name
        Status = $Status
        Total  = $Total
        Passed = $Passed
        Failed = $Failed
        Detail = $Detail
    }
}

function Write-FailureExcerpt {
    param([string]$LogFile, [string]$Label)

    Write-Host ""
    Write-Host "==== $Label -- HIBAS RESZ ($LogFile) ====" -ForegroundColor Red
    if (Test-Path $LogFile) {
        Select-String -Path $LogFile -Pattern 'error|Exception|FAILED|Build FAILED|Aborting batchmode|another Unity instance' |
            Select-Object -Last 40 |
            ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    } else {
        Write-Host "(a log fajl nem jott letre: $LogFile)" -ForegroundColor Red
    }
    Write-Host "=============================================================" -ForegroundColor Red
}

# A Temp/UnityLockfile-t probaljuk kizarolagosan megnyitni: ha ez sikertelen,
# tenylegesen fogja masik folyamat a projektet (pontosan ugyanaz a mechanizmus,
# amit a Unity maga is hasznal a "mar fut egy peldany" ellenorzesere) -- ez
# megbizhatobb, mint a futo folyamatok listajat/parancssorat string-alapon
# egyeztetni.
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

function Invoke-UnityTestPlatform {
    param(
        [string]$PlatformName,
        [string]$ResultsPath,
        [string]$LogFilePath
    )

    # --- 1. Pre-flight: ne is inditsunk Unity-t, ha a projekt mar zarolt. ---
    if (Test-UnityProjectLocked -ProjRoot $ProjectRoot) {
        $msg = "A projekt zarolva van (Temp/UnityLockfile mast folyamat altal tartva) -- " +
               "valoszinuleg fut mar egy masik Unity peldany ezen a projekten (pl. meg nem " +
               "fejezodott be egy korabbi build/teszt-futas). Zard be az Editort vagy vard meg " +
               "a masik futas veget, majd probald ujra. A Unity-t EL SEM INDITOTTUK."
        Write-Host "[test.ps1] HIBA ($PlatformName): $msg" -ForegroundColor Red
        Add-Step -Name "Unity $PlatformName" -Status "ERROR" -Detail $msg
        return
    }

    # --- 2. Regi eredmeny-XML torlese, hogy egy korabbi futas sose tunjon ---
    #        friss sikernek, ha a mostani futas nem allit elo ujat.
    if (Test-Path $ResultsPath) {
        Remove-Item -Force $ResultsPath
    }

    # FONTOS, ketto ok miatt NEM a sima "&" hivasi operatort hasznaljuk:
    #
    # 1. Szandekosan NINCS -quit kapcsolo. A `-runTests` sajat maga zarja be
    #    az Editort a futas vegen; a `-quit` hozzaadasa dokumentaltan azt
    #    okozza, hogy Unity a tesztfuttatas elindulasa ELOTT kilep. Ezt
    #    ezen a gepen tenylegesen elofordulva igazoltuk: -quit-tel az
    #    EditMode run vegigfutott egy teljes, latszolag "sikeres"
    #    boot-and-quit ciklust UGY, hogy a teszt-futtato sosem indult el es
    #    eredmeny-XML sem jott letre.
    # 2. A "& $UnityPath ..." hivasi operator a Unity.exe-hez hasonlo Win32
    #    GUI-subsystem (nem konzol-subsystem) futtathato allomanyoknal NEM
    #    minden host-kontextusban var meg megbizhatoan a process befejezesere
    #    (ezt szinten tenylegesen megfigyeltuk: a hivas AZONNAL visszatert,
    #    $LASTEXITCODE ures maradt, log fajl SOSEM jott letre, holott a
    #    pontosan ugyanazokkal a parameterekkel inditott Start-Process -Wait
    #    hivas korrektul 15+ masodpercig blokkolt es valodi eredmenyt adott).
    #    A Start-Process -Wait -PassThru megbizhatoan blokkol minden
    #    host-kontextusban (interaktiv konzol, nem-interaktiv -File hivas,
    #    beagyazott shell), mert kozvetlenul a process handle-re var, nem a
    #    konzol-orokles mechanizmusara tamaszkodik.
    Write-Host "[test.ps1] Unity $PlatformName batch futtatasa..." -ForegroundColor Cyan
    $unityArgs = @(
        "-batchmode", "-nographics", "-projectPath", $ProjectRoot,
        "-runTests", "-testPlatform", $PlatformName,
        "-testResults", $ResultsPath,
        "-logFile", $LogFilePath
    )
    $proc = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -Wait -PassThru
    $unityExitCode = $proc.ExitCode

    # --- 3. A kilepesi kod CSAK diagnosztikai informacio, nem dontesi alap. ---
    #        Megfigyelt, dokumentalt eset: zarolt projekt eseten a Unity
    #        "Aborting batchmode due to fatal error" uzenettel all le, de a
    #        folyamat kilepesi kodja megis 0 lehet. Ezert eloszor a log
    #        tartalmat, majd az eredmeny-XML letet es tartalmat ellenorizzuk.
    if (Test-LogHasFatalUnityAbort -LogFile $LogFilePath) {
        $msg = "A Unity batch-mode futas VEGZETES HIBAVAL allt le (pl. masik Unity peldany / " +
               "zarolt projekt -- lasd a log reszleteit). A teszt TENYLEGESEN NEM FUTOTT LE, " +
               "holott a Unity folyamat kilepesi kodja $unityExitCode volt (ez NEM megbizhato jelzes)."
        Write-Host "[test.ps1] HIBA ($PlatformName): $msg" -ForegroundColor Red
        Write-FailureExcerpt -LogFile $LogFilePath -Label "Unity $PlatformName"
        Add-Step -Name "Unity $PlatformName" -Status "ERROR" -Detail $msg
        return
    }

    if (-not (Test-Path $ResultsPath)) {
        $msg = "Az eredmeny-XML nem jott letre ($ResultsPath), holott a Unity folyamat kilepesi " +
               "kodja $unityExitCode volt. Ez NEM tekintheto sikeres futtatasnak."
        Write-Host "[test.ps1] HIBA ($PlatformName): $msg" -ForegroundColor Red
        Write-FailureExcerpt -LogFile $LogFilePath -Label "Unity $PlatformName"
        Add-Step -Name "Unity $PlatformName" -Status "ERROR" -Detail $msg
        return
    }

    try {
        [xml]$resultXml = Get-Content -Raw -Path $ResultsPath
        $testRun = $resultXml.DocumentElement
        $result = [string]$testRun.result
        $total = [int]$testRun.total
        $passed = [int]$testRun.passed
        $failed = [int]$testRun.failed
    } catch {
        $msg = "Az eredmeny-XML ($ResultsPath) nem ertelmezheto: $($_.Exception.Message)"
        Write-Host "[test.ps1] HIBA ($PlatformName): $msg" -ForegroundColor Red
        Add-Step -Name "Unity $PlatformName" -Status "ERROR" -Detail $msg
        return
    }

    if ($total -eq 0) {
        $msg = "0 teszt futott le ebben a modban (meg nincs $PlatformName teszt a projektben). " +
               "Ez FIGYELMEZTETES, NEM zold eredmeny -- ne ertelmezd ugy, hogy a kapu lefedte ezt a modot."
        Write-Host "[test.ps1] FIGYELEM ($PlatformName): $msg" -ForegroundColor Yellow
        Add-Step -Name "Unity $PlatformName" -Status "WARNING" -Total $total -Passed $passed -Failed $failed -Detail $msg
        return
    }

    if ($result -eq "Passed" -and $failed -eq 0) {
        Write-Host "[test.ps1] Unity $PlatformName ZOLD. total=$total passed=$passed failed=$failed. Eredmeny: $ResultsPath" -ForegroundColor Green
        Add-Step -Name "Unity $PlatformName" -Status "PASSED" -Total $total -Passed $passed -Failed $failed
    } else {
        $msg = "result=$result, total=$total, passed=$passed, failed=$failed"
        Write-Host "[test.ps1] Unity $PlatformName BUKOTT. $msg" -ForegroundColor Red
        Write-FailureExcerpt -LogFile $LogFilePath -Label "Unity $PlatformName"
        Add-Step -Name "Unity $PlatformName" -Status "FAILED" -Total $total -Passed $passed -Failed $failed -Detail $msg
    }
}

# ---------------------------------------------------------------------------
# 1. dotnet test (gyors kapu, Unity Editor nelkul)
# ---------------------------------------------------------------------------
$dotnetSln = Join-Path $ProjectRoot "tests\MathHorde.Tests.sln"
$dotnetLog = Join-Path $LogDir "test-dotnet.log"

Write-Host "[test.ps1] dotnet test $dotnetSln ..." -ForegroundColor Cyan
& dotnet test $dotnetSln *>&1 | Tee-Object -FilePath $dotnetLog
$dotnetExit = $LASTEXITCODE

$dotnetTotal = $null; $dotnetPassed = $null; $dotnetFailed = $null
$dotnetSummaryLine = Select-String -Path $dotnetLog -Pattern 'Failed:\s*\d+,\s*Passed:\s*\d+,\s*Skipped:\s*\d+,\s*Total:\s*\d+' |
    Select-Object -Last 1
if ($dotnetSummaryLine -and $dotnetSummaryLine.Line -match 'Failed:\s*(\d+),\s*Passed:\s*(\d+),\s*Skipped:\s*(\d+),\s*Total:\s*(\d+)') {
    $dotnetFailed = [int]$Matches[1]; $dotnetPassed = [int]$Matches[2]; $dotnetTotal = [int]$Matches[4]
}

if ($dotnetExit -ne 0) {
    Write-FailureExcerpt -LogFile $dotnetLog -Label "dotnet test"
    Add-Step -Name "dotnet test" -Status "FAILED" -Total $dotnetTotal -Passed $dotnetPassed -Failed $dotnetFailed -Detail "exit=$dotnetExit"
} else {
    Write-Host "[test.ps1] dotnet test ZOLD. total=$dotnetTotal passed=$dotnetPassed failed=$dotnetFailed. Log: $dotnetLog" -ForegroundColor Green
    Add-Step -Name "dotnet test" -Status "PASSED" -Total $dotnetTotal -Passed $dotnetPassed -Failed $dotnetFailed
}

# ---------------------------------------------------------------------------
# 2+3. Unity Test Framework EditMode + PlayMode (csak -Full eseten)
# ---------------------------------------------------------------------------
if ($Full) {
    if (-not (Test-Path $UnityPath)) {
        Write-Host "[test.ps1] HIBA: Unity Editor nem talalhato itt: $UnityPath" -ForegroundColor Red
        Add-Step -Name "Unity EditMode" -Status "SKIPPED" -Detail "Unity nem talalhato: $UnityPath"
        Add-Step -Name "Unity PlayMode" -Status "SKIPPED" -Detail "Unity nem talalhato: $UnityPath"
    } else {
        Invoke-UnityTestPlatform -PlatformName "EditMode" `
            -ResultsPath (Join-Path $LogDir "results-editmode.xml") `
            -LogFilePath (Join-Path $LogDir "test-editmode.log")

        Invoke-UnityTestPlatform -PlatformName "PlayMode" `
            -ResultsPath (Join-Path $LogDir "results-playmode.xml") `
            -LogFilePath (Join-Path $LogDir "test-playmode.log")
    }
}

# ---------------------------------------------------------------------------
# Osszegzes
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "==================== OSSZEGZES ====================" -ForegroundColor Cyan
foreach ($s in $script:steps) {
    $color = switch ($s.Status) {
        "PASSED"  { "Green" }
        "WARNING" { "Yellow" }
        "SKIPPED" { "Yellow" }
        default   { "Red" }
    }
    $countsText = if ($null -ne $s.Total) { "total=$($s.Total) passed=$($s.Passed) failed=$($s.Failed)" } else { "" }
    Write-Host ("  [{0,-8}] {1,-16} {2} {3}" -f $s.Status, $s.Name, $countsText, $s.Detail) -ForegroundColor $color
}
Write-Host "====================================================" -ForegroundColor Cyan

$failedSteps = $script:steps | Where-Object { $_.Status -in @("FAILED", "ERROR") }
if ($failedSteps.Count -gt 0) {
    Write-Host "[test.ps1] VEGEREDMENY: BUKOTT ($($failedSteps.Count) lepes hibas/hibazott)." -ForegroundColor Red
    exit 1
}

$warningSteps = $script:steps | Where-Object { $_.Status -eq "WARNING" }
if ($warningSteps.Count -gt 0) {
    Write-Host "[test.ps1] VEGEREDMENY: ZOLD, DE FIGYELMEZTETESSEL ($($warningSteps.Count) lepes 0 tesztet futtatott -- lasd fent)." -ForegroundColor Yellow
} else {
    Write-Host "[test.ps1] VEGEREDMENY: ZOLD." -ForegroundColor Green
}
exit 0
