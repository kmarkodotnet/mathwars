# IMPLEMENTATION_STATUS.md — Math Horde

Utolsó frissítés: **2026-11-21** · Aktuális fázis: **F3 — matematikai kapuk, folyamatban**

**ADR-017: F1-20 elkészült; F1-21 és F3-12 nyitott.**
A jelenlegi egyfegyveres F1 scene mindig végtelenül tüzel. Nincs ammo pool,
fogyasztás, dry-fire, kill-refund vagy finite/dev kapcsoló. A Core/SO/repository
és a valódi builder/assetek nem tartalmaznak aktív ammo mezőt; a HUD Health/Shield
értéket mutat. `WeaponFired` fegyverazonosítót és sebzést hordoz, kill nem jutalmaz.
Két valóban választható típus és HUD-gombjaik továbbra is **F1-21** feladata.

A közös `ResourceRules` alapján a pool/state/resource API programozói hibával
utasítja el az Ammo tombstone-t. A Generate/GenerateBatch és Prepare/Bind,
Validate/IsValid, schedule/adapter, reward és mindhárom math presenter külön
negatív regresszióval védett. Mindkét request mindkét jutalommezőjét ellenőrizzük;
Ammo esetén `AMMO_NOT_ALLOWED`, eltérő nem-ammo párnál `INVALID_REQUEST` az eredmény.
A reward tiltása a pool- és idempotencia-napló módosítása előtt történik.
Az ordinalok és a régi `AmmoStarvation` mentések olvasása/visszaírása megmaradt;
új session továbbra is `LossDiagnosis.None` értéket rögzít.

A W1L01 öt resource bejegyzése `Maximize × Shield`, öt lane bejegyzése Health;
az eredeti indexek/triggerek/CL1 készségek megmaradtak. MaxShield láthatóan 0→50.
Az adapter a HealthRewardBase=15 értéket továbbra is explicit jutalomtérképből
kapja; a RewardTableSO és a valódi math-host kompozíciója F3-12 része marad.
A reprodukálható scene-builder új assethez ugyanezt a schedule-t állítja elő.
A **CL1/Shield50 teljes coverage** továbbra is valódi integrációs akadály,
valamint a geometria/schedule és a teljes scene-matekbekötés **F3-12** feladata.
Az Ammo→más erőforrás névcsere nem tekinthető ennek megoldásának.
A korábbi gyári F3-12 resource-context DRAFT superseded, nem implementálandó.
A teljes minőségi kapu és mindkét kötelező matekmechanika követelménye változatlan.

F1-20 friss kapuja: **.NET 843/843/0, Unity EditMode 842/842/0, PlayMode 352/352/0**
(total/passed/failed; nincs kihagyás). Mindkét 10 000 seedelt generátor-oracle,
a snapshot/gap/cap/skill/idempotencia és G4 regresszió zöld. A valódi Gameplay
scene lövése/sebzése/killje, a Health/Shield HUD és pause/resume is ellenőrzött.
A teljes parancs és a javított PlayMode újrafuttatása a
[tesztelési dokumentumban](TESTING.md#f1-20-friss-migrációs-bizonyíték--2026-11-21),
részletes lezárás az [F1-20 naplóban](backlog/done/F1-20.md).

> A következő F3-02…F3-07 leírások az ADR-017 előtti ténylegesen ellenőrzött
> munkák történeti bizonyítékai. Az ottani ammo-adatok nem jelenlegi termékszabályok;
> a migrált működés F1-20-as bizonyítéka fent külön szerepel. A done naplók nem változtak.

Az aktuális feladatonkénti állapotot a [generált backlog](backlog.md) tartalmazza.
**F3-07 konfigurációja elkészült**: valódi `MathEventConfigDefault.asset`, a
`LevelDefinitionSO` nyolcmezős matematikai ütemezése/accuracyje és Core-fordítása,
valamint az eredeti indexeket megőrző adapter a meglévő director-settingsre.
A CL-időzítés/jutalom feloldott táblázati értékei explicit bemenetek; nincs
alternatív balance-tábla. A W1L01 10 váltakozó eseményt tartalmaz, de a
CL1/MaxAmmo=200 teljes coverage továbbra is igazolt `GENERATION_EXHAUSTED`:
ez ismert, rollout előtt javítandó technikai hiba. A teljes scene- és
pályaidőzítési integráció **F3-12**. Részletek:
[konfiguráció és integrációs korlát](balance/math-events.md),
[F3-07 napló](backlog/done/F3-07.md).

**F3-06 válaszsáv-generálása és sávhoz rendelése igazolt**: a meglévő generátor
és director teljes, korai immutable feladat-Preview-jét 2–4 opcióval és minden
megengedett pályasávszámmal ellenőrizzük a valódi feloldásig és jutalomkönyvelésig.
A harc közben fogyó lőszer mellett a helyes választás fix +30, a hibás +0;
az opció nélküli sáv nem old fel feladatot. Az előkészített helyes és hibás
életciklus 0 bájtot allokál, a CL4 generálás átlagideje opciószámonként
10 000 mintán 2 ms alatt marad. A meglévő független aritmetikai és sáveloszlási
fuzz is a teljes kapu része. A scene/presenter bekötése továbbra is F3-12.
Részletes bizonyíték: [F3-06 napló](backlog/done/F3-06.md).

**F3-05 generátorellenőrzése elkészült**: a már implementált offline
`Generate` és az ADR-015 Prepare/Bind út invariánsait változó snapshotú,
10 000 seedelt regresszió és explicit kapacitáshatár-esetek is fedik.
A nyers eredmény/delta megmarad; a döntetlen, a túl kicsi effektív rés és
a kapacitás miatti nyertesváltás elutasított. A Bind aritmetikai hibaágai
is 0 bájtot allokálnak. A profil/Max inkompatibilitás továbbra is valódi hiba;
a teljes scene- és assetintegráció F3-07/F3-12.
Részletes bizonyíték: [F3-05 napló](backlog/done/F3-05.md).

**F3-03 elkészült**: `IGateResolutionService`, nyers snapshot-delta és fix
sávjutalom, struct válaszadat, timeout, idempotens feloldás és napló-visszaállítás.
Az előre lefoglalt napló mellett mindkét feladattípus 10 000 első feloldása és
10 000 ismétlése 0 bájtot allokál. A szolgáltatás nem módosít erőforrást;
a jutalomkönyvelés F3-04, a session teljes integrációja F3-12.
A teljes `scripts/test.ps1 -Full` kapu eredménye: **.NET 774/774,
Unity EditMode 773/773, Unity PlayMode 227/227**, mindenütt 0 hiba.
Részletes bizonyíték: [F3-03 napló](backlog/done/F3-03.md).

**F3-02 elkészült**: determinisztikus `MathEventDirector`, külső Prepare,
snapshotfüggő és allokációmentes resource Bind, carrier-mozgás, deadline,
idempotens feloldási parancs és jutalomfeldolgozási visszajelzés.
Tízezer resource gate 110 000 bindingja átment az offline validátoron;
a két valódi directoros PlayMode teszt alatt a harc folyamatos maradt.
Részletes bizonyíték: [F3-02 napló](backlog/done/F3-02.md),
elfogadott életciklus: [ADR-015](decisions/ADR-015.md).
A scene teljes feloldási/jutalom/presenter/analitikai bekötése **F3-12**;
a Bootstrapben az előkészítési és Tick utáni diagnosztikai hívási hely készült el.
Az F1 SO-adatok teljes snapshot-lefedettsége nem automatikus: az inkompatibilis
profil/Max pár őszinte `GENERATION_EXHAUSTED` hibát ad; az assetintegráció F3-07/F3-12.

> Az alábbi 0–7. szakasz a **2026-11-16-i F0-átadás történeti pillanatképe**.
> Az ottani feladatszámok, hiánylisták és következő lépések nem a jelenlegi
> fejlesztési állapotot írják le; az aktuális állapot forrása a fenti backlog.

> Ez a fájl a spec 22. szakasza által kötelezően előírt állapotkövetés.
> Szabály: **a dokumentáció soha nem helyettesíti az implementációt** — itt
> csak az szerepelhet késznek, ami ténylegesen létezik és ellenőrizve lett.

---

## 0. F0 fázis — projektváz (KÉSZ, ellenőrzött)

| ID | Eredmény | Bizonyíték |
|---|---|---|
| F0-01 | Unity projekt a repó gyökerében | `ProjectVersion.txt` = `6000.3.28f1`; ARM64-only, IL2CPP, portrait, Linear színtér, URP 17.3.0 asset hozzárendelve; a beállításokat a `Assets/Editor/ProjectBootstrap.cs` **reprodukálhatóan** állítja (spec 20. szakasz) |
| F0-02 | 8 assembly definition, rétegenként | Mind a 6 réteg DLL-je lefordul; a Core/MathEngine/GameLogic/Analytics `noEngineReferences: true`. **Igazolva szabotázs-próbával:** egy `using UnityEngine;` a Core alatt `error CS0246`-ot ad → az invariáns gépileg kikényszerített, nem csak dokumentált |
| F0-03 | `tests/` tükör .NET solution | 4 mirror-projekt + `MathHorde.Tests.Unit`; `Compile Include` **linkelés** (nincs duplikált forrásfájl); `LangVersion 9.0` a Unity-profilhoz; `global.json` a 10.0.301 SDK-ra pinnel (a gépen 11.0.100 is van) |
| F0-04 | Build/teszt belépési pontok + **működő APK** | `Builds/MathHorde-Dev.apk`, 90 821 041 byte; kizárólag `lib/arm64-v8a/` natív libek; `package=com.kmarkodotnet.mathhorde`, `targetSdk=36`, label „Számharcosok". A teljes lánc igazolva: Unity 6000.3.28f1 → IL2CPP ARM64 → NDK r27c → build-tools 36 → OpenJDK 17 → Gradle |
| F0-05 | Lokalizációs váz | `ILocalizationService` (Core, Unity-független) + `DictionaryLocalizationService` + `ResourcesLocalizationService` (Infrastructure) + `Resources/Strings/hu.json` (9 HUD-kulcs); hiányzó kulcs `[[kulcs]]`-ként látszik, nem crashel |

**A hibrid teszt-stack mindkét oldala igazoltan fut** (ugyanaz az 5 tesztfájl):
- `dotnet test` (NUnit 4.2.2, net9.0): `Failed: 0, Passed: 6` (5 lokalizációs + 1 smoke)
- Unity EditMode (NUnit 3.5-alapú): `result="Passed" total="5" passed="5" failed="0"`
- Unity PlayMode: `total="0"` — még nincs PlayMode teszt, a szkript ezt **figyelmeztetésként** jelzi, nem zöldként

## 1. Dokumentáció (ARCH fázis, kész)

| Dokumentum | Tartalom |
|---|---|
| `docs/ARCHITECTURE.md` | 6 réteg, függőségi irányok, asmdef-terv, mappaszerkezet, két állapotgép, a harc↔matek együttfutás mechanizmusa |
| `docs/contracts/core-domain.md` | Core adatmodellek, invariánsok |
| `docs/contracts/math-engine.md` | MathEngine publikus interfészei (AST, generátor, evaluator, distraktor, validátor) |
| `docs/contracts/game-logic.md` | GameLogic szolgáltatás-interfészek, események, erőforrás-snapshot |
| `docs/contracts/scriptable-object-schema.md` | A spec 16. szakaszának összes konfigurálható paramétere SO-sémaként |
| `docs/GAME_DESIGN.md` | Core loop, a két matek-mechanika designja, ellenségtípusok, erőforrások, jutalmazás |
| `docs/balance/difficulty-model.md` | 1–10 kihívásszint számszerű modellje (MathComplexity / TimePressure / CombatDifficulty), adaptációs szabályok |
| `docs/balance/worlds.md` | Világok tartalmi terve, világ↔kihívásszint szétválasztás |
| `docs/balance/SIM-plan.md` | A balancing-szimuláció terve (Fázis 4-ben fut) |
| `docs/TESTING.md` | Teszt-piramis, hibrid stack, 46 katalogizált teszteset, A–K leképezés |
| `docs/backlog.md` | **91 feladat** 8 fázisban (F0 előkészítés + a spec 19. szakaszának 7 fázisa), függőségi gráffal |
| `docs/decisions/ADR-001…010` | Unity-verzió, repó-elrendezés, hibrid teszt-stack, JSON-könyvtár, matek↔harc szinkronizáció, determinisztikus véletlen, nyelvi konvenció, **ellenségtípus-taxonómia (008)**, **dokumentum-hierarchia és konzisztencia-kapu (009)**, **készség-mastery szintek (010)** |

A gyár konfigurációja (`CLAUDE.md`, `.claude/agents`, `.claude/commands`,
`.claude/skills`) szintén átállt a Unity/mobil stackre.

## 2. Hogyan lett ellenőrizve

- A három dokumentáló agent kimenetét átnéztem konzisztenciára.
- **Talált és javított hiba:** az ADR-006 önellentmondó volt — egyszerre
  állította, hogy a generálás platformfüggetlenül bájtra azonos, és hogy a
  `System.Random` sorozata verziók között változhat. Mivel a hibrid
  teszt-stack (ADR-003) lényege, hogy ugyanaz a seedelt teszt fut
  `dotnet test` és Unity alatt is, az alapimplementációt saját,
  platformfüggetlen PRNG-re (`DeterministicRandomSource`, xoshiro128\*\*)
  cseréltem. Javítva: `ADR-006`, `core-domain.md`, `ARCHITECTURE.md`,
  `backlog.md` (F1-03).
- **Az F0 fázis minden eredményét visszaolvasással ellenőriztem** (nem az
  agentek jelentése alapján): `ProjectVersion.txt`, `ProjectSettings.asset`
  kulcsmezői, `manifest.json`, az asmdef-ek `noEngineReferences` értékei, a
  lefordult DLL-ek, az APK natív libjei (`unzip -l` + `aapt2 dump badging`),
  és a teszt-eredmény XML-ek tartalma.
- **Talált és javított hiba #2 — hamis zöld a minőségi kapuban:** a
  `test.ps1 -Full` „Unity EditMode ZÖLD" és „PlayMode ZÖLD" üzenetet írt,
  miközben az eredmény-XML-ek **nem is jöttek létre** (a Unity a
  projekt-lock miatt meg sem nyitotta a projektet, és közben 0-s kilépési
  kódot adott). Két valódi gyökérok derült ki:
  1. a `-quit` és a `-runTests` együtt — a Unity kiléphet, MIELŐTT a
     teszt-futtató elindul, így nincs eredmény, de a kilépés tisztának tűnik;
  2. a PowerShell `&` hívásoperátor nem blokkol megbízhatóan GUI-subsystem
     exe-nél (`Unity.exe`) — `Start-Process -Wait -PassThru` kell.
  Javítva: a szkriptek most az **eredmény-XML tartalmát** (`result`,
  `total`, `failed`) és a **kimeneti fájl létezését + friss időbélyegét**
  tekintik bizonyítéknak, nem a kilépési kódot; a projekt-lockot a Unity
  indítása ELŐTT felismerik; a `total=0` külön figyelmeztetés. A tanulság
  bekerült az `android-build` skillbe.
- **Eltérések, amiket az agentek jelentettek és nem titkoltak el:** a minimum
  API-szint 24 helyett 25 lett (a Unity + NDK r27c felemelte); a Unity
  6000.3-ban nincs generikus `Standalone` platformnév az asmdef-ben.
- **NEM futott / nem igazolt:** `BuildReleaseAab` — nincs valódi release
  keystore ebben a környezetben, ezért csak a fail-fast ága lett ellenőrizve
  (env-változók nélkül helyesen megtagadja a buildet). Aláírt release build
  nem készült, és ilyet nem is állítunk.

## 3. Mi hiányzik

A **91 backlog-feladatból 5 kész** (az F0 fázis), **86 nyitott**. Hiányzik
a teljes játékmenet: nincs matekmotor (AST, generátor, distraktorok),
nincs harcrendszer, nincs erőforrás-kezelés, nincs matek-kapu, nincs
adaptív nehézség, nincs pálya/világ/boss, nincs HUD és nincs szülői
dashboard. A jelenlegi APK egy **üres, generált placeholder scene-t**
tartalmaz — nem játék, hanem a toolchain bizonyítéka.

## 4. Elfogadási kritériumok (spec 21. szakasz)

| A | B | C | D | E | F | G | H | I | J | K |
|---|---|---|---|---|---|---|---|---|---|---|
| ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |

Egyik sem kezdődött el. A leképezést a `docs/TESTING.md` 4. szakasza tartalmazza.

## 5. Ismert problémák és kockázatok

| # | Kockázat | Hatás | Kezelés |
|---|---|---|---|
| 1 | ~~Unity 6.3 nem található~~ — **MEGOLDVA** 2026-11-16: `6000.3.28f1` telepítve, Android modullal (OpenJDK 17, NDK r27c, SDK 34–37) | — | ADR-001 rögzíti a patch-szintet; az F0-01 indulhat |
| 2 | **F kritérium** (a harc a matek alatt sem áll le) jelenleg egyetlen PlayMode teszten (T-COMBAT-007) múlik | Ez a spec legfontosabb architekturális invariánsa; egy regresszió átcsúszhat | A Fázis 1 végén bővíteni a lefedettséget (több időzítési forgatókönyv, tick-sorrend unit teszt a `GameSessionService`-re) |
| 3 | **J és K kritérium** nem automatizálható (szülői kimutatás, Android használhatóság) | Release-kapu nem lehet teljesen gépi | J: `T-PARENT-*` katalógus a Fázis 6-ban; K: dokumentált manuális eszköz-ellenőrzés |
| 4 | `IAdaptiveDifficultyService` kontraktja szándékosan vázlat | A Fázis 4 nem indulhat kontrakt-mélyítés nélkül | F4-00 backlog-feladat írja elő, az architect agenttel |
| 5 | A motorkód **C# 9** nyelvi szintre korlátozott (Unity 6.3 profil) | Újabb nyelvi elemek nem használhatók a megosztott forrásban | A `tests/` solution is C# 9-re állítva, hogy a hibrid stack ne divergáljon |
| 6 | A **gyárkonfiguráció nincs verziókövetés alatt** (a repó a termék mappája, ADR-002) | A `factory-engineer` önfejlesztő hurka (`factory/*` ág + review-kapu) nem végrehajtható | A factory-engineer csak javaslatot ír, a gyárat a felhasználó jóváhagyásával módosítjuk; ha kell a hurok, a gyár mappához külön repó inicializálandó |
| 7 | A gépen a **6000.0.77f1 és 6000.1.14f1 is telepítve van** | Ha véletlenül azokkal nyitják meg a projektet, a Unity visszafordíthatatlanul felfelé migrál | Minden batch-mode hívás a `6000.3.28f1` Editort nevezi meg explicit útvonallal (`android-build` skill) |

## 6. Következő lépés

1. **Commit + push** a `mathwars` remote-ra (a felhasználó döntése; eddig
   egyetlen commit sincs a repóban).
2. **F1 fázis — játszható harci alap** (18 feladat, F1-01…F1-18): Core
   erőforrás- és pálya-modellek, `DeterministicRandomSource`,
   `ResourceService`, `CombatService`, `WaveDirector`, `GameSessionService`
   a kötelező tick-sorrenddel, object pooling, 3 sávos pálya, egyujjas
   sávváltás, alap HUD, GameOver, végül a gameplay scene reprodukálható
   Editor-eszközzel. A fázis végén az **A** kritérium (folyamatos harc) és
   részben a **G**, **K** igazolható, és az APK már valóban játszható.
3. F2 fázis: matekmotor (AST, generátor, validátor) + a 10 000 seedelt fuzz.
   A Fázis 1 végén az **A** és részben a **G**, **K** kritériumnak már igazolhatónak kell lennie.

## 7. Elrendezés (ADR-002)

Ez a repó a **termék**: Unity projekt + `tests/` + `docs/`. A gyártást végző
agent-gyár (`.claude/`, `scripts/`, `factory-docs/`) a repón kívül, a
munkaterület gyökerében él, és onnan nézve ez a mappa a `game/`. A termék-
dokumentáció útvonalai ezért a repó gyökeréhez relatívak.
