# TESTING.md — Math Horde teszt-stratégia

Státusz: **F0 fázis (projektváz) kész, a teszt-infrastruktúra működik.**

Ami ma TÉNYLEGESEN fut és zöld (2026-11-16):
- a `tests/` tükör-solution: `dotnet test` → **6 teszt zöld** (1 smoke-teszt
  a linkelés igazolására + 5 lokalizációs teszt a `DictionaryLocalizationService`-re)
- a `game/scripts/test.ps1` és `build.ps1` belépési pontok
- a teljes Android toolchain: a `BuildDevApk` elkészített egy futtatható,
  ARM64-only dev APK-t (lásd `docs/IMPLEMENTATION_STATUS.md`)

Ami még NEM létezik: az alábbi **teszteset-katalógus (3. szakasz) döntő
többsége**, mert a tesztelendő kód (MathEngine, ResourceService,
CombatService, DDA) még nincs megírva — az F1–F4 fázisok dolga. A
katalógus tehát továbbra is kötelező megvalósítási terv, nem állapotjelentés.
Egyetlen teszt sem jelölhető "zöldnek" itt, amíg valóban le nem futott.

A kódszerkezetet a `docs/ARCHITECTURE.md` és a `docs/contracts/` rögzíti.

---

## 1. Teszt-piramis és a hibrid stack

### 1.1 Miért hibrid a stack (ADR-003)

A `Core` és `MathEngine` réteg (6. szakasz: `ExpressionGenerator`,
`ExpressionEvaluator`, `EquationSolver`, `DistractorGenerator`,
`ProblemValidator`) és a `GameLogic` réteg tiszta üzleti logikája
(`ResourceService`, `AdaptiveDifficultyService`, `GateResolutionService`
stb., 13. szakasz) a tervek szerint **nem függ `UnityEngine` típusoktól**.
Ez lehetővé teszi, hogy ugyanazok a forrásfájlok és EditMode tesztfájlok
egy sima `dotnet test` futással, Unity Editor nélkül, másodpercek alatt
visszajelzést adjanak — ez különösen a 10 000 seedelt fuzz-tesztnél
kritikus, mert az Unity Test Framework batch-mode indítása (Editor
boot) önmagában lassabb, mint maga a 10 000 iterációs futás.

A teljes **minőségi kapu** mindig a Unity Test Framework (EditMode +
PlayMode), mert csak az tudja igazolni, hogy a scene-szintű integráció
(fegyver, ellenség, HUD, `MathEventDirector` + `WaveDirector` szinkron)
is működik. A `dotnet test` csak a gyors, fejlesztői visszacsatolás.

### 1.2 Három réteg

| Réteg | Mit tartalmaz | Hogyan fut | Miért itt |
|---|---|---|---|
| **dotnet test** (`tests/` tükör-solution) | `Core`, `MathEngine`, Unity-független `GameLogic` logika + ezek EditMode tesztjei, `Compile Include` linkeléssel a `Assets/_Project/Scripts/`-ból | `dotnet test tests/MathHorde.Tests.sln` | Nincs Unity-függőség, nincs Editor-boot késleltetés — ez a leggyorsabb visszacsatolás, ide tartozik a 10 000 seedelt fuzz is |
| **Unity Test Framework — EditMode** | Ugyanazok a tesztfájlok, mint fent, PLUSZ a Unity-only EditMode tesztek (pl. `ScriptableObject`-alapú konfiguráció betöltése), amik `UnityEngine` típust igényelnek, de nem igényelnek futó scene-t | `Unity -batchmode -runTests -testPlatform EditMode` | A teljes kapu része; igazolja, hogy a dotnet-ben zöld kód Unity Editor alatt is fordul és fut |
| **Unity Test Framework — PlayMode** | Scene-szintű integráció: harc + matek-esemény egyszerre, `MonoBehaviour`, korutin, idő-előrehaladás | `Unity -batchmode -runTests -testPlatform PlayMode` | Csak itt ellenőrizhető valós `Update`/`FixedUpdate` ciklus, GameObject-interakció, a 14. szakasz szinkronizációs követelménye |

### 1.3 Döntési fa — hova kerüljön egy új teszt

```
Új teszt → érint-e UnityEngine típust (MonoBehaviour, GameObject,
           Transform, Time, Input, Physics, Coroutine, élő
           ScriptableObject-példány) a TESZTELT kódban vagy magában
           a teszt kódjában?
│
├─ NEM → EditMode teszt a `Assets/_Project/Tests/EditMode/`-ban,
│         a `tests/` solutionbe is linkelve.
│         Fut: dotnet test ÉS Unity EditMode.
│         (Idetartozik minden MathEngine, ResourceService,
│          AdaptiveDifficultyService, SkillProfile teszt.)
│
└─ IGEN → szükséges-e FUTÓ SCENE / több GameObject időbeli
           interakciója (harci tick, hullámindítás, matek-esemény
           állapotgép élő lejátszása)?
    │
    ├─ NEM (pl. egy ScriptableObject deserializálása,
    │        egy Inspector-mező validálása)
    │   → Unity-only EditMode teszt, külön `.asmdef`-ben, ami NEM
    │     szerepel a `tests/` tükör-solutionben.
    │     Fut: KIZÁRÓLAG Unity EditMode.
    │
    └─ IGEN → PlayMode teszt, `[UnityTest]` + `IEnumerator`.
        Fut: KIZÁRÓLAG Unity PlayMode.
```

Szabály: **egy tesztfájl soha nem szerepel két futtatási módban
egyszerre** (sem duplikálva, sem ugyanaz a fájl mindkét célban
linkelve olyan módon, hogy UnityEngine-függő kódra hivatkozzon a
dotnet oldalról).

---

## 2. Futtatási parancsok

> **Kontrakt-változás (F0-04):** a `make` NINCS telepítve a gyártógépen
> (`make: command not found`), ezért a kanonikus belépési pont NEM Makefile,
> hanem a `game/scripts/` alatti PowerShell szkript (`test.ps1`, `build.ps1`).
> Ez a termék repójában van (`game/scripts/`), így a GitHubról klónozó
> fejlesztő is tudja futtatni, Make telepítése nélkül. Az alábbi parancsok
> már futtathatók (a `tests/` tükör-solution és a Unity projekt F0-03/F0-01
> óta léteznek).
>
> A szkriptek a **`game/` repó gyökeréből** (vagy bárhonnan, mivel a
> `$PSScriptRoot`-ból oldják fel a projekt gyökerét) futnak.

```powershell
# Gyors visszacsatolás (dotnet, Unity Editor nélkül) — fejlesztés közben
# és a 10 000 seedelt fuzz-teszthez elsődleges.
./scripts/test.ps1

# Teljes kapu: dotnet test + Unity EditMode batch + Unity PlayMode batch
./scripts/test.ps1 -Full

# Unity Editor útvonal felülírható (alapértelmezett: 6000.3.28f1):
./scripts/test.ps1 -Full -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe"
```

A `test.ps1` belsőleg ugyanazt a két Unity batch-mode parancsot hívja, amit
korábban ez a szakasz dokumentált — **FONTOS: szándékosan NINCS `-quit`
kapcsoló** (lásd `.claude/skills/android-build/SKILL.md` "Build/teszt
bizonyíték-szabály": a `-runTests` saját maga zárja be az Editort a futás
végén; a `-quit` hozzáadása dokumentáltan azt okozza, hogy Unity a
tesztfuttatás elindulása ELŐTT kilép, és a teszt ténylegesen nem fut le,
miközben a kilépési kód mégis sikeresnek tűnhet):

```bash
Unity -batchmode -nographics -projectPath game \
  -runTests -testPlatform EditMode \
  -testResults artifacts/results-editmode.xml

Unity -batchmode -nographics -projectPath game \
  -runTests -testPlatform PlayMode \
  -testResults artifacts/results-playmode.xml
```

Belépési pontok:

| Szkript | Mit futtat | Mikor |
|---|---|---|
| `./scripts/test.ps1` | `dotnet test tests/MathHorde.Tests.sln` | minden commit előtt, fejlesztői gép |
| `./scripts/test.ps1 -Full` | a fenti + Unity EditMode batch + Unity PlayMode batch | merge előtt, CI, release-kapu |
| `./scripts/build.ps1 -Apk` | `BuildScript.BuildDevApk` (dev APK, debug keystore) | toolchain-ellenőrzés, fejlesztői smoke-build |
| `./scripts/build.ps1 -Aab` | `BuildScript.BuildReleaseAab` (Play-kompatibilis AAB, env-aláírással) | release-kapu, kizárólag emberi jóváhagyással |

Egy dedikált `@smoke`-csak futtatás (a korábban tervezett `make smoke`
megfelelője) ebben a fázisban még nincs külön szkriptbe szervezve — a
`@smoke` kategória szűrése egyelőre `dotnet test tests/MathHorde.Tests.sln
--filter Category=smoke` paranccsal (vagy a Unity Test Framework
`-testFilter`-ével) végezhető el kézzel; ha ez gyakori igénnyé válik, külön
`./scripts/smoke.ps1` kap majd saját belépési pontot egy jövőbeli
feladatban.

---

## 3. Kötelező tesztesetek katalógusa

Azonosító-konvenció: `T-<TERÜLET>-<sorszám>`. A "Mód" oszlop: **dotnet**
(fut `dotnet test`-tel ÉS Unity EditMode-ban), **EditMode-Unity**
(kizárólag Unity EditMode, mert `UnityEngine` típust igényel futó scene
nélkül), **PlayMode** (kizárólag Unity PlayMode).

### 3.1 Matematikai tesztek (17.1. szakasz)

| ID | Leírás | Elvárt eredmény | Mód |
|---|---|---|---|
| T-MATH-001 | `7 × 8` kiértékelése | `56` | dotnet |
| T-MATH-002 | `37 + 28` kiértékelése | `65` | dotnet |
| T-MATH-003 | `83 − 47` kiértékelése | `36` | dotnet |
| T-MATH-004 | `72 ÷ 9` kiértékelése | `8` | dotnet |
| T-MATH-005 | `29 ÷ 4` maradékos osztás | hányados `7`, maradék `1` | dotnet |
| T-MATH-006 | `29 mod 4` | `1` | dotnet |
| T-MATH-007 | `(6 + 4) × 3`, zárójel-precedencia | `30` | dotnet |
| T-MATH-008 | `8 + 4 × 6 − 7`, műveleti sorrend | `25` | dotnet |
| T-MATH-009 | `7 + ? = 3 × 8`, ismeretlen operandus megoldása | `? = 17` | dotnet |
| T-MATH-010 | `(? + 17) × 3 = 6 × (14 − 3)`, összetett egyenlet | `? = 5` | dotnet |
| T-MATH-011 | Nullával osztást tartalmazó kifejezés generálása/kiértékelése elutasítva | `ProblemValidator` elutasítja, nem kerül ki generált feladatként | dotnet |
| T-MATH-012 | Nem egész hányadost eredményező osztás az egész-only MVP módban | elutasítva vagy explicit maradékos/mod alakra kényszerítve a 6.2. szakasz szerint | dotnet |
| T-MATH-013 | Negatív eredményt adó kivonás/egyenlet a feladattípus korlátjába ütközik | elutasítva vagy a feladattípus explicit engedi (konfigurálható korlát tesztelve mindkét irányban) | dotnet |
| T-MATH-014 | Szándékosan túlcsorduláshoz közeli nagy operandusok (`int`/`long` határ) | kontrollált kivétel vagy elutasítás, nem csendes `OverflowException`/wrap-around | dotnet |
| T-MATH-015 | `EquationSolving` feladat pontosan egy `UnknownNode`-dal | megoldás egyértelmű, több-ismeretlenes kifejezés elutasítva | dotnet |
| T-MATH-016 | Generált `MixedOperations` feladatok nagy mintán (nem csak a fenti fix példák) | minden esetben a matematikailag helyes műveleti sorrend szerinti eredmény | dotnet |
| T-MATH-017 | Generált, zárójelet tartalmazó feladatok nagy mintán | a zárójelezett részkifejezés mindig előbb értékelődik ki | dotnet |
| T-MATH-FUZZ-001 | **10 000 seedelt generált feladat** (ld. 3.1.1) | minden invariáns teljesül minden iterációban | dotnet |

**A tíz rögzített példa végrehajtható leképezése (F2-11):** a tesztek a
`Spec17_1` kategóriával külön is kiválaszthatók:
`dotnet test tests/MathHorde.Tests.sln --filter TestCategory=Spec17_1`.
Ugyanezek a források Unity EditMode-ban is futnak (ADR-003); a kategória
pontos darabszáma 10, új tesztmásolat nélkül.

Az alábbi metódusok helye: **Evaluator** =
[`ExpressionEvaluatorTests.cs`](../Assets/_Project/Tests/EditMode/MathEngine/ExpressionEvaluatorTests.cs),
**Solver** =
[`EquationSolverTests.cs`](../Assets/_Project/Tests/EditMode/MathEngine/EquationSolverTests.cs).

| ID | Forrás | Végrehajtott tesztmetódus |
|---|---|---|
| T-MATH-001 | Evaluator | `Evaluate_Multiplication_7x8_Is56` |
| T-MATH-002 | Evaluator | `Evaluate_Addition_37plus28_Is65` |
| T-MATH-003 | Evaluator | `Evaluate_Subtraction_83minus47_Is36` |
| T-MATH-004 | Evaluator | `Evaluate_Division_72div9_Is8` |
| T-MATH-005 | Evaluator | `Evaluate_IntegerDivisionWithRemainder_29divr4_Is7Remainder1` |
| T-MATH-006 | Evaluator | `Evaluate_Modulo_29mod4_Is1` |
| T-MATH-007 | Evaluator | `Evaluate_ForceParentheses_6plus4TimesThree_Is30` |
| T-MATH-008 | Evaluator | `Evaluate_OperatorPrecedence_8plus4times6minus7_Is25` |
| T-MATH-009 | Solver | `Solve_7plusUnknown_EqualsThreeTimesEight_Is17` |
| T-MATH-010 | Solver | `Solve_NestedEquationExample_Is5` |

Mindegyik példa a numerikus eredmény mellett a megjelenítést is ellenőrzi;
a maradékos osztás az AST-kontrakt szerinti `29 ÷r 4` jelölést és a
`7 maradék 1` eredményszöveget használja. A skalár eredménynek nincs maradéka.
A két egyenletnél pontosan egy ismeretlen, egyértelmű egész megoldás és a
megoldó/ellenőrző egyezése is kötelező: a helyes értéket elfogadja, a két
szomszédos egész választ elutasítja.

A 17.1 további hibaágait az Evaluator
`Evaluate_DivisionByZero_FailsWithDivisionByZero`,
`Evaluate_DivisionWithNonIntegerResult_FailsWithNonIntegerResult`,
`Evaluate_SubtractionNegativeResult_DefaultOptions_FailsWithNegativeResultNotAllowed`,
`Evaluate_SubtractionNegativeResult_AllowNegativeResult_Succeeds` és
`Evaluate_TrueIntOverflow_FailsWithOverflow`, valamint a Solver
`SolveForTarget_MultiplicationByTwo_OddTarget_FailsWithNoIntegerSolution` és
`Solve_MultipleUnknowns_FailsWithMultipleUnknowns` tesztjei fedik.
A teljes kapu a `MathProblemGeneratorFuzzTests` tizenöt, egyenként
10 000 seedelt feladatot ellenőrző körét is futtatja (seed: `209000 + i`,
`i = 0..9999`, mind az öt feladattípusra, külön 2/3/4 opcióval).
Minden feladaton ellenőrzi többek között a validitást, az egyetlen helyes
választ, a különböző opciókat, a 2–4-es opciószámot, a seed/típus/nehézség
metaadatokat és a mintán belüli egyedi, nem üres azonosítót. Minden teljes,
10 000-es csoporton külön vizsgálja a helyes sáv khí-négyzet statisztikáját.
A további 10 000 modulo-/maradékos feladat (`2120000 + i`) és a 10 000
vegyes műveletű kapupár (`2091000 + i`), valamint a 10 000 változó
snapshotú erőforrás-kapu (`305000 + i`) mellett ez összesen 180 000
generált feladat a fixture 18 tesztesetében.
A D és E kritérium matematikai rétegét ez igazolja; a játékbeli megjelenés
és választás teljes integrációja a Fázis 3 feladata.

#### 3.1.1 A 10 000 seedelt fuzz-teszt

- **Mesterszeed:** `209000`, a modulo-/maradékos kiegészítő mintánál
  `2120000`, a vegyes kapupár-mintánál `2091000`, a változó snapshotú
  kapuknál `305000`. A `k`-adik feladat
  seedje `masterSeed + k` (`k = 0..9999`), így a teszt teljes egészében
  reprodukálható. Egy bukó esethez a seed mellett a típus/opciószám és
  kapunál a cél, illetve a kiegészítő mintánál az operátor is szerepel
  az assertion üzenetében. A generálási hibát nem hagyjuk ki a mintából.
- **Ellenőrzött invariánsok** minden egyes generált feladatra:
  1. A kifejezésfa gyökere matematikailag érvényes (nincs nullával
     osztás, nincs érvénytelen tartomány, nincs túlcsordulás — 6.2.).
  2. Pontosan egy helyes válasz van a válaszopciók között (ha a
     feladattípus válaszopciós, 6.4.).
  3. Minden felkínált válasz különböző (nincs duplikátum).
  4. A válaszopciók száma 2 és 4 között van.
  5. A feladat ténylegesen a deklarált `SkillTag`-eket gyakoroltatja
     (a kifejezésfa tartalmazza a hozzájuk tartozó művelet-csomópontot).
  6. A helyes válasz és egy tetszőleges disztraktor sosem egyezik meg
     numerikusan (nincs "véletlen helyes" disztraktor).
  7. A feladat `Id`-je, seedje és nehézségi szintje konzisztensen
     visszaolvasható a feladat objektumból.
- **Bukás reprodukálása:** bukásnál a teszt kiírja a hibás feladat
  pontos seedjét, kérésparamétereit és a sérült invariáns megnevezését.
  Ebből egyetlen `[TestCase(seed)]`-es minimál-reprodukcióval a bug
  izolálható kód nélkül is a `factory-docs/bugs/`-ba írható.
- **Futásidő-korlát:** a 10 000 iteráció `dotnet test` alatt célzottan
  **5 másodperc alatt** fusson (tiszta CPU-kötött logika, nincs I/O,
  nincs Unity-boot). Ha egy jövőbeli módosítás ezt számottevően (pl.
  30s fölé) növeli, az külön ADR-t igényel, mert veszélyezteti a
  "gyors visszacsatolás" célt (1.1. szakasz).
- A fixture `[Category("Fuzz")]` alatt fut, célzott futtatása:
  `dotnet test tests/MathHorde.Tests.sln --filter FullyQualifiedName~MathProblemGeneratorFuzzTests`.
  A teljes `scripts/test.ps1 -Full` MINDIG lefuttatja mind a 10 000
  esetet — mintavételezés vagy csonkítás tilos a release-kapuban.

### 3.2 Válaszopció-tesztek (17.2. szakasz)

| ID | Leírás | Elvárt eredmény | Mód |
|---|---|---|---|
| T-OPT-001 | Generált válaszopciós feladatok mintáján pontosan egy helyes válasz | igaz minden mintára | dotnet |
| T-OPT-002 | Nincs két azonos numerikus értékű válaszopció egyazon feladatban | igaz minden mintára | dotnet |
| T-OPT-003 | A válaszopciók száma minden feladatnál 2–4 | igaz minden mintára | dotnet |
| T-OPT-004 | A helyes válasz pozíciójának eloszlása sok feladat generálása után egyenletes | ld. statisztikai küszöb lent | dotnet |
| T-OPT-005 | Egyetlen felkínált disztraktor sem értékelhető helyesnek alternatív matematikai értelmezés mellett (pl. kerekítés, más operátor-sorrend) | igaz minden mintára | dotnet |

**Statisztikai küszöb T-OPT-004-hez:** generálj `N = 10 000` darab,
rögzített `k` válaszopció-számú (külön `k=2`, `k=3`, `k=4` csoport)
feladatot egy rögzített seedtartományból, és végezz khí-négyzet
illeszkedésvizsgálatot a helyes válasz pozíciójának megfigyelt
gyakoriságára a `1/k` egyenletes eloszláshoz képest. Elfogadási
kritérium: a khí-négyzet statisztika `p-érték > 0.01` (azaz nem
utasítjuk el az egyenletes eloszlás nullhipotézisét 1%-os
szignifikanciaszinten). A khí-négyzetet (nem egyszerű toleranciasávot)
azért javasolt választani, mert `k` csoportonként eltérő a várható
cellaszám, és a khí-négyzet ezt korrekt módon súlyozza; egy fix
százalékos tolerancia `k=2`-nél túl szigorú, `k=4`-nél túl engedékeny
lenne ugyanakkora `N` mellett.

**Megvalósított T-OPT-001–005:**
[`MathProblemGeneratorFuzzTests`](../Assets/_Project/Tests/EditMode/MathEngine/MathProblemGeneratorFuzzTests.cs)
`Generate_TenThousandSeedsPerTypeAndOptionCount_ValidAndNoAnswerPositionStrategy`
esetei fedik az öt típust és a három opciószámot. A statisztikai határok
`df=1/2/3` mellett rendre `6.634896601`, `9.210340372`, `11.344866730`;
a megfigyelt sávgyakoriságok, a khí-négyzet és a minimum/maximum-helyes
válasz arányok a tesztkimenetben és a Unity-eredmény-XML-ben is szerepelnek.
Az eredményválasztóknál a legnagyobb/legkisebb válasz választása legfeljebb
`1/k + 0.10` találati arányt adhat. Kapuknál a cél szerinti optimum lehet
szándékosan szélsőérték; ott a külön kapupárteszt a nagyobb operandusra
épülő heurisztika `0.70` felső korlátját ellenőrzi.

A teszt saját, egész aritmetikát szélesített köztes értékekkel számoló
AST-orákuluma a production evaluator/solver/validator nélkül újraszámolja
a kifejezést és minden felkínált válasz igazságát. Ismeretlenes feladatnál
minden opciót behelyettesít az egyenlet mindkét oldalára; kapunál a
snapshotból újraszámolja az összes opció nyers eredményét. A külön
modulo-/maradékos kör a teljes `(hányados, maradék)` választ hasonlítja
össze, így az azonos hányadosú, eltérő maradékú opciók nem olvadnak össze.
A helyes értelmezést az AST műveleti sorrendje és a kontrakt egész
aritmetikája adja; hibás sorrend vagy kerekítés eredménye disztraktor
lehet, nem második helyes válasz. A D/E kritérium itt a matematikai
rétegre igazolt; a játékbeli sávválasztás és jutalmazás Fázis 3 hatóköre.

**F3-06 válaszsáv-integráció és teljesítmény:** az
[`AnswerLaneSelectionIntegrationTests`](../Assets/_Project/Tests/EditMode/GameLogic/AnswerLaneSelectionIntegrationTests.cs)
15 esete a valódi generátort, directort, feloldót és jutalomszolgáltatást használja.
A hat `(opciószám, pályasávszám)` kombináció: `(2,2)`, `(2,3)`, `(2,4)`,
`(3,3)`, `(3,4)`, `(4,4)`; mindegyiknél ugyanazon feladat minden választását
végigpróbáljuk. A Preview már a teljes kifejezést közli, a hordozók később jelennek
meg ugyanazzal az opcióindexszel, sávval, értékcímkével és helyességgel.
Az extra, opció nélküli sáv nem választ válaszopciót. A feladat ugyanaz az immutable
objektum marad a Preview-tól a lezárásig. A lőszer `20→13→7` változása mellett a
helyes választás `+30`, a hibás `+0` jutalmat ad, generálás/binding nélkül a Tickben.
Külön hat eset méri a helyes/hibás 2–4 opciós életciklus 0 managed allokációját,
a hordozók spawnjától a feloldáson és jutalomkönyvelésen át a lezárásig.
Három további eset opciószámonként 10 000 CL4 feladaton, a generálást külön időzítve
ellenőrzi a MathEngine 9. szakasz `<2 ms` átlagidejét; a mért érték az XML-ben van.
A jelenetszintű megjelenítő- és sessionbekötés további F3-12 feladat.

### 3.3 Erőforrás-kapu teszt (17.3. szakasz)

| ID | Leírás | Elvárt eredmény | Mód |
|---|---|---|---|
| T-GATE-001 | Alapeset: kiinduló lőszer 20, bal kapu `×2`, jobb kapu `+25` | bal eredmény 40, jobb eredmény 45, optimális a jobb kapu, helyes választás jutalma +25 | dotnet |
| T-GATE-002 | A T-GATE-001 feladat létrehozásakor a snapshot 20, de a kapu aktiválásakor a tényleges lőszer időközbeni lövésektől 8-ra csökkent | `RewardDelta = OperationResult(45) − SnapshotValue(20) = 25`; végső lőszer `8 + 25 = 33` | dotnet |
| T-GATE-003 | A játékos a hibás (bal, `×2`) kaput választja | nincs matematikai jutalom jóváírva, a tényleges lőszer csak a lövésekből eredően változik | dotnet |

A T-GATE-002 explicit igazolja a specifikáció `RewardDelta =
OperationResult − SnapshotValue` képletét snapshot-alapú számítással,
nem a nyers kapu-eredmény felülírásával (5.1. szakasz, "Erőforrás-
snapshot").

Az F3-05 a meglévő offline `Generate` és az ADR-015 szerinti
`PrepareResourceGate`/`BindResourceGate` útvonalat együtt igazolja. A változó
snapshotú fuzz-minta `0..100` kiindulóértéket, változó kapacitást, mindhárom
célt és az öt skalár műveletet ellenőrzi független egész aritmetikával.
Az engedélyezett negatív páncél-delta és a kapacitást meghaladó nyers nyertes
is része a mintának; a profil változatlan marad.

A `ResourceGatePreparationTests` 10 000 előkészítést és azok 110 000
bindingját az offline validátorral ellenőrzi. Külön esetek igazolják a nyers
és kapacitás okozta döntetlent, az effektív `MinGateGap` pontos határát és
a `ReachTarget` nyertesének kapacitás miatti megváltozását. A sikeres binding
nyers értéket és nyers snapshot-deltát őriz. A Bind siker-, kontextushiba-,
túlcsordulás-, nullával osztás-, nem egész osztás- és hibás modulo-ágának
allokációmérése 0 bájtot követel. Az inkompatibilis CL1/MaxAmmo páros
`GENERATION_EXHAUSTED` marad; a tényleges SO/pályakonfiguráció integrációs
ellenőrzése az F3-07/F3-12 feladata.

### 3.4 Harci tesztek (17.4. szakasz)

| ID | Leírás | Elvárt eredmény | Mód |
|---|---|---|---|
| T-COMBAT-001 | Lövés végrehajtása, van lőszer | a lőszerkészlet eggyel (vagy a fegyver-kontraktban rögzített mértékkel) csökken | PlayMode |
| T-COMBAT-002 | Lövési kísérlet nulla lőszerrel | nem történik lövés, nincs lőszer-alulfutás (negatív érték) | PlayMode |
| T-COMBAT-003 | Találat egy ellenségen | az ellenség életereje a fegyver sebzésével csökken | PlayMode |
| T-COMBAT-004 | Ellenség életereje nullára csökken | az ellenség megsemmisül (eltávolításra/inaktívra kerül) | PlayMode |
| T-COMBAT-005 | Ellenség eléri a játékos pozícióját/vonalát | a játékos életereje csökken a kontraktban rögzített sebzéssel | PlayMode |
| T-COMBAT-006 | A játékos életereje nullára csökken | `GameOver` állapotátmenet következik be | PlayMode |
| T-COMBAT-007 | **Matematikai esemény folyamatban (`AwaitingDecision` állapot), miközben ellenségek közelednek és a fegyver tüzel** | a harci szimuláció (ellenségmozgás, találatfeldolgozás, lőszerfogyás) a matek-esemény állapotától függetlenül, változatlan ütemben fut tovább — ez a 14. szakasz kritikus szinkronizációs követelményének közvetlen igazolása | PlayMode |
| T-COMBAT-008 | Egy adott kapu/sáv-esemény (rögzített esemény-azonosítóval) kétszer kerül feldolgozásra (pl. a játékos kétszer "lép be" ugyanabba a felbontott eseménybe) | a jutalom csak egyszer íródik jóvá (idempotens feldolgozás az esemény-azonosító alapján) | PlayMode |
| T-COMBAT-009 | A matek-esemény határideje lejár (`Expired` állapot) mielőtt a játékos választana | nincs jutalom jóváírva, a harc zavartalanul folytatódik | PlayMode |

A T-GATE sorozat (dotnet) a `GateResolutionService` tiszta
döntéslogikáját igazolja; a T-COMBAT-007/008/009 (PlayMode) ugyanezt a
szolgáltatást élő scene-ben, valós időben, a harci ciklussal
párhuzamosan futtatva igazolja vissza — a kettő együtt fedi le a
17.3+17.4 szakaszt, és (ld. 4. szakasz) a `GateResolutionService`
architektúrabeli megosztottsága miatt (13. szakasz) ugyanez a
mechanizmus szolgálja ki az 5.2. szakasz eredményválasztó sávjait is.

### 3.5 Adaptációs tesztek (17.5. szakasz)

| ID | Leírás | Elvárt eredmény | Mód |
|---|---|---|---|
| T-ADAPT-001 | Pálya első próbálkozásra sikeres | globális kihívásszint `+1` | dotnet |
| T-ADAPT-002 | Ugyanazon pálya két egymást követő sikertelen próbálkozása | globális kihívásszint `−1` | dotnet |
| T-ADAPT-003 | Ismételt sikerek/kudarcok sorozata a szint alsó/felső határán | a szint soha nem megy 1 alá vagy 10 fölé | dotnet |
| T-ADAPT-004 | Három egymást követő hibás matematikai válasz pályán belül | pályán belüli segítség aktiválódik (pl. lassabb kapuközeledés), a már megjelent feladatok megoldása változatlan marad | dotnet |
| T-ADAPT-005 | `MathComplexity`/`TimePressure` módosítása `CombatDifficulty` érintetlenül hagyása mellett, és fordítva | a két (három) dimenzió egymástól függetlenül állítható és olvasható vissza | dotnet |
| T-ADAPT-006 | Globális kihívásszint emelése egy olyan készségprofil mellett, amelyben egy adott művelet (pl. maradékos osztás) még nincs elsajátítva | a feladatgenerátor nem ad ki olyan feladatot, amely az el nem sajátított előfeltételt igényelné, a globális szintemelés ellenére sem | dotnet |
| T-ADAPT-007 | Adaptációs állapot mentése, majd visszatöltése | a visszatöltött állapot (szint, próbálkozás-számlálók, hiszterézis-ablak) mezőről mezőre megegyezik a mentés előttivel | dotnet |

### 3.6 Készségprofil tesztek (8. szakasz, kiegészítő EditMode lefedettség)

| ID | Leírás | Elvárt eredmény | Mód |
|---|---|---|---|
| T-SKILL-001 | Helyes/helytelen válasz adott `SkillTag`-re | az adott készség próbálkozás- és helyes-válasz-számlálója nő, a többi készségé változatlan | dotnet |
| T-SKILL-002 | Egyetlen helyes válasz egy adott készségre | a készség nem minősül elsajátítottnak (minimális mintaszám-küszöb alatt) | dotnet |
| T-SKILL-003 | Feladatgenerálás gyenge és erős készségek vegyes profilja mellett, sok iteráción át | a generátor statisztikailag gyakrabban, de nem kizárólagosan választ gyakorlásra szoruló készséget (marad ismétlő/fenntartó feladat is) | dotnet |
| T-SKILL-004 | `SkillProfile` mentése, majd visszatöltése | minden készség minden mezője (próbálkozás, pontosság, átlag-/medián válaszidő, utolsó gyakorlás ideje) konzisztensen visszaolvasható | dotnet |

---

## 4. Elfogadási kritériumok (21. szakasz, A–K) → teszt-leképezés

| Krit. | Megnevezés | Igazoló tesztek | Megjegyzés |
|---|---|---|---|
| A | Folyamatos harc | T-COMBAT-001, 003, 004, 005, 007 | PlayMode igazolja a lövés→sebzés→közeledés láncot folyamatos működésben |
| B | Erőforrás-kapu | T-GATE-001, 002, 003 | dotnet réteg; a PlayMode T-COMBAT-008/009 a kapu-feldolgozás élő változatát is lefedi |
| C | Eredményválasztó sávok | T-MATH-001, T-OPT-001–005, T-COMBAT-007/008/009 | A `GateResolutionService` megosztottsága miatt (13. szakasz) a kapu-mechanizmus PlayMode tesztjei a sáv-választást is kiszolgálják; a 7×8=56/4-sávos konkrét példa az T-MATH-001 + T-OPT sorozat kombinációjával igazolt |
| D | Többféle operátor | T-MATH-001–010, T-MATH-FUZZ-001 | lefedi +, −, ×, ÷, mod, maradékos osztás, többoperandusos kifejezés |
| E | Ismeretlen operandus | T-MATH-009, T-MATH-010, T-MATH-015 | egyértelmű, egy-ismeretlenes egyenletek |
| F | Valós idejű működés | T-COMBAT-007 | **ez a legszorosabban vett, egyetlen architektúrális invariánst igazoló teszt** — ha ez piros, a 14. szakasz alapelve sérül |
| G | Erőforráshiány | T-COMBAT-002, T-COMBAT-006, T-GATE-003 | hibás matematikai döntések → lőszerhiány → áttörés → GameOver lánc |
| H | Adaptív nehézség | T-ADAPT-001, 002, 003, 007 | szintnövekedés/csökkenés és korlátok |
| I | Készségprofil | T-SKILL-001–004, T-ADAPT-006 | külön készség-nyilvántartás és annak hatása a feladatkiválasztásra |
| J | Szülői kimutatás | **nincs automatizált teszt ebben a katalógusban** | a specifikáció 11. szakasza saját maga "következő mérföldkő"-ként jelöli a Parent Dashboard-ot; az `Analytics` réteg (`ProgressReportService`, `LocalStatisticsRepository`) kontraktjának elkészültekor a TESTING.md-t bővíteni kell `T-PARENT-*` tesztekkel. Amíg ez nincs meg, J kritérium kizárólag manuális ellenőrzéssel igazolható |
| K | Android használhatóság | **csak manuális/eszközös ellenőrzés** | érintőképernyős interakció, olvashatóság és sáv-kiválaszthatóság valós/emulált Android eszközön; nem automatizálható sem EditMode, sem PlayMode Editor-futtatással — a `mobile-build-engineer` és az `android-build` skill checklistje adja a release-kapu tételét |

**Release-kapu checklist:** A–I kritérium minden hozzárendelt tesztje
zöld a `make test` futtatásban; J-hez a jövőbeli `T-PARENT-*` katalógus
elkészülte és zöldje (vagy addig dokumentált, tudatos kockázatvállalás);
K-hoz egy aláírt manuális Android-eszköz-ellenőrzési jegyzőkönyv.

---

## 5. Determinizmus és flaky-teszt szabályok

- **Minden teszt explicit, dokumentált seeddel fut.** Véletlenszám-
  generátor sosem kaphat rendszeridő-alapú implicit seedet tesztben.
- **Játékidő-mockolás kötelező PlayMode-ban.** Az időzítés-érzékeny
  tesztek (pl. T-COMBAT-007, T-COMBAT-009, a matek-feladat
  időkeretének lejárata) a `Time.timeScale` gyorsítását vagy egy
  injektálható, teszt alatt manuálisan léptethető játékidő-absztrakciót
  használjanak, SOHA ne ad-hoc `yield return new WaitForSeconds(x)`-et
  valós másodpercekkel — ez lassítja a futást és determinisztikus
  időzítés nélkül flaky-vé teszi a tesztet.
- **Fuzz-teszt bukás = kötelező minimál-reprodukció.** A bukó seedet a
  hibariportban `[TestCase(konkrétSeed)]` formában kell rögzíteni, hogy
  a fejlesztő egyetlen futtatással, a 10 000-es ciklus nélkül
  reprodukálhassa.
- **Instabil (flaky) PlayMode teszt kezelése:** ha egy PlayMode teszt
  3 egymást követő, változatlan kóddal végzett futtatásból akár
  egyszer is eltérő eredményt ad:
  1. A tesztet azonnal `[Category("Flaky")]`-vel kell megjelölni és
     KIVENNI a `@smoke`/release-kapu kötelező listából — flaky teszt
     nem maradhat néma módon a kapuban.
  2. Bug-riportot kell nyitni (`factory-docs/bugs/BUG-<n>.md`) a gyökérokkal
     (tipikusan hiányzó időmock, frame-függő feltétel, vagy végrehajtási
     sorrend-függőség korutinok között).
  3. A teszt csak a gyökérok javítása és 10 egymást követő stabil
     futtatás után kerülhet vissza a kötelező katalógusba.
  4. Flaky teszt elrejtése `[Ignore]`-ral bug-riport nélkül tilos.

---

## 6. Bug-riport folyamat és smoke-csomag

### 6.1 Bug-riport sablon (`factory-docs/bugs/BUG-<n>.md`)

```markdown
# BUG-<n>: <rövid cím>

- **Súlyosság:** blokkoló / magas / közepes / alacsony
- **Érintett elfogadási kritérium:** <A–K betű és név, vagy spec-szakasz
  hivatkozás, ha nem feleltethető meg közvetlenül A–K-nak>
- **Érintett teszt(ek):** <T-xxx-nnn azonosító(k), ha van>

## Lépések a reprodukáláshoz
1. ...
2. ...

## Elvárt viselkedés
...

## Kapott viselkedés
...

## Minimális reprodukció (ha fuzz/seed-alapú)
`seed = <érték>`, parancs: `dotnet test --filter ...`
```

### 6.2 Smoke-csomag (`@smoke`)

Minden merge-nél és minden `/operate` ciklusban lefutó, 3–5 tesztből
álló minimális halmaz — a legkritikusabb, réteget és architekturális
invariánst átfogó válogatás:

| ID | Miért van a smoke-ban |
|---|---|
| T-MATH-001 | Legegyszerűbb MathEngine-helyesség (a spec D/C kritériumának alapesete) |
| T-GATE-001 | Erőforrás-kapu alapképlet (`RewardDelta`), a B kritérium magja |
| T-COMBAT-007 | A legkritikusabb architekturális szabály: a matek SOHA nem állítja le a harcot (F kritérium) |
| T-COMBAT-008 | Jutalom-duplázódás elleni védelem — gyakori regressziós kockázat idempotencia-hibáknál |
| T-ADAPT-001 | Adaptív nehézség alapiránya (H kritérium) |

Ez az 5 teszt a `make smoke` cél tartalma; a `T-MATH-FUZZ-001` tudatosan
NEM része a smoke-nak (5 másodperces futásideje még mindig túl drága
minden egyes merge-hez, ha percenként több merge történik), de a
teljes `make test`-ben minden alkalommal lefut.

---

## Összefoglaló

A dokumentum egy kötelező, A–K-ra és a 17. szakaszra visszavezethető
teszt-katalógust rögzít: 10 fix matematikai példa (T-MATH-001–010) + 7
általános matematikai validációs teszt + 1 db 10 000-elemű seedelt
fuzz-teszt, 5 válaszopció-teszt, 3 erőforrás-kapu teszt, 9 harci
(PlayMode) teszt, 7 adaptációs teszt és 4 készségprofil-teszt —
összesen 46 azonosított automatizált teszteset plusz a fuzz-ciklus.
Mind a 11 elfogadási kritériumhoz (A–K) van hozzárendelt automatizált
teszt, kivéve J-t (szülői kimutatás), amelyet maga a spec is jövőbeli
mérföldkőnek jelöl, és K-t (Android használhatóság), amely jellegénél
fogva csak eszközös/manuális ellenőrzéssel igazolható. A legnehezebben
igazolható kritérium — a technikai kockázat szempontjából — az **F
(valós idejű működés)**: ez az egyetlen kritérium, amely egyetlen,
időzítésérzékeny PlayMode tesztre (T-COMBAT-007) támaszkodik egy
architekturálisan kritikus, könnyen regresszálható invariáns
(matek-állapotgép és harci ciklus tényleges függetlensége) igazolására.
