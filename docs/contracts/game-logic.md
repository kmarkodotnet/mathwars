# Kontrakt — GameLogic réteg

> Assembly: `MathHorde.GameLogic` · Namespace: `MathHorde.GameLogic.*`
> References: `MathHorde.Core`, `MathHorde.MathEngine`
> **`noEngineReferences: true` — `using UnityEngine;` TILOS** (ADR-003, ARCHITECTURE 2.1).
> A réteg headless futtatható: a balancing-szimuláció (spec 9.1) Unity nélkül pörgeti.
> Mélység: **MÉLY** (Fázis 1–3). `IAdaptiveDifficultyService`: **vázlat** (Fázis 4).
>
> **Revízió r2 (2026-11-16, architect):** a `ResourceService.Initialize` pajzs-viselkedése
> rögzítve (`MaxShield`); `EnemySnapshot` definíció/snapshot szétválasztása explicit;
> `CalculateStars` küszöbök SO-ból; `LossDiagnosis` bevezetve az adaptációs bemenethez.
>
> **Revízió r3 — ADR-018 + ADR-019 (2026-11-22, architect) — ÉRVÉNYES:**
> a matematikai esemény **nem olvas élő erőforrásállást**. A kapu-feladat a
> Ticken kívül, teljesen legenerált, immutable `MathProblem`; a snapshot-alapú
> `Bind`, a kapacitásfigyelés és `IResourceService.CaptureSnapshot` **kivezetve**.
> A jutalom mindkét kötelező mechanikán **fix, pozitív `Health`-összeg**
> (`RewardSpec.FlatAmount`); nincs delta-képzés.

## 0. Alapszabályok

| # | Szabály |
|---|---|
| G1 | Semmilyen szolgáltatás nem olvas `Time.deltaTime`-ot — az idő **mindig** `IGameClock`-ból vagy `Tick(double gameTime)` paraméterből jön. |
| G2 | A szolgáltatások **nem** hívják egymást keresztbe-kasul: a `GameSessionService` az egyetlen kompozíciós gyökér, amely a `Tick` sorrendet vezényli. |
| G3 | Esemény-kibocsátás `IGameEventSink`-en keresztül; a kibocsátó nem tud az előfizetőkről. |
| G4 | `Tick`-ben nincs allokáció: előre allokált bufferek, `struct` eseménypayload, nincs LINQ. |
| G5 | A `MathEventDirector` és a `WaveDirector` **nem hivatkozik egymásra** — sem fordításilag, sem futásidőben. Ezt a konstruktor-szignatúrák kényszerítik ki (egyiknek sincs a másikra mutató paramétere). |
| G6 | Minden véletlen `IRandomSource`-ból (ADR-006). |
| G7 | A GameLogic csak `*Definition` (statikus) és saját `*Snapshot`/`Active*` (futásidejű) típusokkal dolgozik. Futásidejű mezőt `*Definition`-re visszaírni TILOS — a definíció immutable és példányok között osztott (ADR-009). |
| G8 | **A matematikai feladat tartalma nem függ futásidejű játékállapottól** (ADR-018). A `MathEventDirector` a feladat előkészítése után erőforrást nem olvas, feladatot nem módosít és nem generál újra. A kész `MathProblem` a feloldásig változatlan. |

---

## 1. Események — `IGameEventSink`

```csharp
namespace MathHorde.GameLogic.Events;

public interface IGameEventSink
{
    void Publish<TEvent>(in TEvent gameEvent) where TEvent : struct, IGameEvent;
}

public interface IGameEvent { double GameTime { get; } }
```

**Teljes eseménykatalógus (Fázis 1–3):**

| Esemény (struct) | Kibocsátó | Mezők |
|---|---|---|
| `SessionStarted` | GameSessionService | `GameSessionId SessionId, LevelId LevelId, int Seed, double GameTime` |
| `SessionEnded` | GameSessionService | `GameSessionId SessionId, SessionResult Result, FailureReason Reason, double GameTime` |
| `ResourceChanged` | ResourceService | `ResourceChange Change, double GameTime` |
| `WaveStarted` | WaveDirector | `int WaveIndex, double GameTime` |
| `WaveCleared` | WaveDirector | `int WaveIndex, double GameTime` |
| `EnemySpawned` | WaveDirector | `int EnemyInstanceId, string EnemyId, LaneIndex Lane, Vec2 Position, double GameTime` |
| `EnemyDamaged` | CombatService | `int EnemyInstanceId, int Damage, int RemainingHealth, double GameTime` |
| `EnemyKilled` | CombatService | `int EnemyInstanceId, string EnemyId, double GameTime` |
| `EnemyReachedPlayer` | CombatService | `int EnemyInstanceId, int ContactDamage, double GameTime` |
| `WeaponFired` | CombatService | `int TargetEnemyInstanceId, string WeaponId, int Damage, double GameTime` |
| `WeaponChanged` | CombatService | `string PreviousWeaponId, string WeaponId, double GameTime` (F1-21) |
| `PlayerDamaged` | CombatService | `int Damage, int RemainingHealth, double GameTime` |
| `PlayerLaneChanged` | CombatService | `LaneIndex From, LaneIndex To, double GameTime` |
| `MathEventPreviewStarted` | MathEventDirector | `MathEventId EventId, ProblemType Type, MathProblemId? ProblemId, string DisplayExpression, GateGoal Goal, int? TargetValue, int RewardAmount, double GameTime` |
| `MathEventGatesSpawned` | MathEventDirector | `MathEventId EventId, int GateBaseValue, double GameTime` (ADR-019: bázisérték, **nem** erőforrás-snapshot) |
| `MathEventDecisionWindowOpened` | MathEventDirector | `MathEventId EventId, double DeadlineGameTime, double GameTime` |
| `MathEventResolved` | GateResolutionService | `MathEventId EventId, LaneIndex SelectedLane, bool WasCorrect, double GameTime` |
| `MathEventCompleted` | MathEventDirector | `MathEventId EventId, int AppliedDelta, ResourceType Resource, double GameTime` (`Resource` mindig `Health`) |
| `MathEventExpired` | MathEventDirector | `MathEventId EventId, string ReasonCode, double GameTime` (`"DEADLINE"`, `"SESSION_ENDED"`, `"PREPARATION_FAILED"`) |
| `MathEventPreparationFailed` | MathEventDirector | `MathEventId EventId, string ErrorCode, double GameTime` (tartalom-/kontrakthiba, nem gyermek-timeout) |
| `RewardApplied` | RewardService | `MathEventId EventId, ResourceType Resource, int AppliedDelta, double GameTime` |
| `RewardSkipped` | RewardService | `MathEventId EventId, string ReasonCode, double GameTime` (`"INCORRECT"`, `"ALREADY_APPLIED"`, `"EXPIRED"`) |
| `ChallengeLevelChanged` | AdaptiveDifficultyService | `int Previous, int New, string Reason, double GameTime` (Fázis 4) |
| `AssistTierChanged` | AdaptiveDifficultyService | `int PreviousTier, int NewTier, string TriggerCode, double GameTime` (Fázis 4) |

A `MathEventPreviewStarted` korábbi `ResourceType Resource` mezője **törölve**: a
jutalom erőforrása mindig `Health`, ezért nem információ. Helyette a fix
`RewardAmount` (a `RewardSpec.FlatAmount`) kerül a payloadba, hogy a HUD előre
mutathassa, mennyi életerő a tét. A `MathEventGatesSpawned` korábbi
`SnapshotResource` mezője szintén **törölve**.

A Presentation réteg **kizárólag** ezekre az eseményekre és az olvasó property-kre
támaszkodhat; a GameLogic belső mezőihez nem fér hozzá.

---

## 2. IResourceService (spec 4.3)

```csharp
namespace MathHorde.GameLogic;

public interface IResourceService
{
    ResourceState State { get; }
    int GetCurrent(ResourceType type);
    int GetMax(ResourceType type);

    /// EGYETLEN módosítási út. Pozitív és negatív delta egyaránt.
    /// Aktív erőforrásnál a [Min, Max] tartományra vág; a vágás nem dob kivételt.
    /// reasonCode: "MATH_GATE" | "ENEMY_CONTACT" | "PICKUP" | "LEVEL_INIT"
    ///           | "BOSS_PHASE"
    ResourceChange ApplyDelta(ResourceType type, int delta, string reasonCode);

    /// Lefoglalás: csak akkor von le, ha a teljes mennyiség rendelkezésre áll.
    /// Visszatérés: true, ha sikerült. Csak véges, nem-ammo erőforráshoz használható.
    bool TryConsume(ResourceType type, int amount, string reasonCode, out ResourceChange change);

    /// Maximum módosítása (upgrade). A Current soha nem haladhatja meg az új Max-ot.
    ResourceChange SetMax(ResourceType type, int newMax, string reasonCode);

    /// Pálya kezdeti feltöltése a LevelDefinition alapján. Csak a GameSessionService hívja.
    void Initialize(LevelDefinition level);
}
```

### 2.1. TILTÁSOK (kódszinten kikényszerítve)

| Tilos | Miért |
|---|---|
| **Nincs publikus `SetCurrent(type, absoluteValue)`** | A matematikai jutalom soha nem írhatja felül a harc által módosított tényleges értéket (spec 5.1, 17.3). Minden jóváírás delta. |
| **Nincs `CaptureSnapshot`** (ADR-019 2.10. — kivezetve) | Nincs olyan mechanika, amely az erőforrás pillanatnyi állását feladatbemenetként használná (ADR-018 1. pont). A metódus visszavezetése kontraktsértés. |
| `ApplyDelta` hívása a Presentation rétegből | A HUD csak olvas és `ResourceChanged` eseményre animál. |
| `ResourcePool` közvetlen mutálása | `ResourceState` immutable. |
| A `Max` levezetése a `Starting*` értékből | Minden erőforrásnak **saját** `Max` mezője van a `LevelDefinition`-ben (`MaxHealth`, `MaxShield`) — spec 4.3. |
| Pajzs matematikai jutalomként | ADR-018 3. pont: `ApplyDelta(Shield, …)` kizárólag `"ENEMY_CONTACT"`, `"LEVEL_INIT"` vagy `"BOSS_PHASE"` indokkal hívható; `"MATH_GATE"` indokkal **csak `Health`** módosítható. |

### 2.2. `Initialize` kötelező viselkedése

A `LevelDefinition` `Starting*`/`Max*` párjaiból **pontosan** két aktív pool jön létre,
azonos véges-erőforrás minta szerint. Az Ammo tombstone nem aktív erőforrás:

| Pool | `Current` | `Max` | Mikor jön létre |
|---|---|---|---|
| `Health` | `level.StartingHealth` | `level.MaxHealth` | mindig (`MaxHealth > 0` invariáns) |
| `Shield` | `level.StartingShield` | `level.MaxShield` | **csak ha `level.MaxShield > 0`** |

`MaxShield == 0` ⇒ a pajzs-pool **nem jön létre** (a `ResourcePool` invariánsa `Max > 0`),
és a `GetCurrent(Shield)` / `GetMax(Shield)` 0-t ad — ez a pajzs-mechanika nélküli pálya
helyes ábrázolása. A `Max = max(StartingShield, 1)` jellegű kerülő megoldás TILOS.

`ResourceType.EnemyArmor` és `Energy` poolokat az `Initialize` **nem** hoz létre az
F1–F4 fázisban (F5-től a `LevelDefinition` bővítése után).

### 2.3. Kötelező referencia-viselkedés (fix jutalom, ADR-018)

A matematikai jutalom fix, pozitív `Health`-összeg, amelyet a harc által már
módosított tényleges értékhez **deltaként** adunk, `Max`-ra vágva:

```
Initialize: Health = 100 (Max 100), Shield = 20 (Max 50)
... harc: ENEMY_CONTACT 30 sebzés -> Shield 0, Health 90
Helyes kapuválasz, RewardSpec.FlatAmount = 30 (Health)
ApplyDelta(Health, +30, "MATH_GATE") -> PreviousValue = 90, NewValue = 100,
                                        AppliedDelta = 10, WasClamped = true
```

A `WasClamped` nem hiba és nem „elvesztegetett” feladat: a `Max` kemény korlát
(core-domain 5.). A HUD a tényleges `AppliedDelta`-t animálja.

Ammo pool nem inicializálódik. Az Ammo-t fogadó `GetCurrent`/`GetMax`/
`ApplyDelta`/`TryConsume`/`SetMax` út `ArgumentException` programozói hibával
elutasít; sem 0/Max, sem `int.MaxValue` nem reprezentál végtelent.

---

## 3. ICombatService (spec 4.1, 4.2)

```csharp
public interface ICombatService
{
    void Initialize(LevelDefinition level, IReadOnlyList<WeaponDefinition> weapons,
                    IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog);

    /// Egy szimulációs lépés: ellenségmozgás, célzás, tüzelés, találat, ütközés, halál.
    /// NEM függ semmilyen matematikai esemény állapotától (spec 1., 23./4.).
    void Tick(double gameTime, float deltaTime);

    /// A WaveDirector ezen keresztül helyez be ellenséget — a két szolgáltatás
    /// nem hivatkozik egymásra, a GameSessionService köti őket össze.
    int SpawnEnemy(string enemyId, LaneIndex lane, float spawnDepth);

    void MovePlayerToLane(LaneIndex lane);
    LaneIndex PlayerLane { get; }
    bool IsPlayerAlive { get; }

    /// Olvasó nézet a Presentation számára (előre allokált, Tick között stabil buffer).
    IReadOnlyList<EnemySnapshot> ActiveEnemies { get; }

    /// Harci nehézség-szorzók, a CombatDifficulty dimenzióból (spec 7.3).
    void ApplyCombatModifiers(CombatModifiers modifiers);
}

/// Egy ÉLŐ ellenség-példány futásidejű állapota.
///
/// SZÁNDÉKOS SZÉTVÁLASZTÁS (ADR-009): a `CurrentHealth` / `Position` / `Lane` / `IsAlive`
/// mezők ITT élnek, és NEM az `EnemyDefinition`-ön. Az `EnemyDefinition` statikus
/// konfiguráció, amelynek egyetlen példánya a pálya összes azonos ellenségén osztódik;
/// ha futásidejű mezőt tenne, két egyszerre élő „basic_bot” felülírná egymás életerejét.
/// A spec 4.1 „aktuális és maximális életerő” követelményét ez a struct elégíti ki
/// (`CurrentHealth` + `MaxHealth`, utóbbi a definícióból, a `CombatModifiers`-szel
/// már megskálázva). A két típust összevonni TILOS.
public readonly struct EnemySnapshot
{
    public int InstanceId { get; }
    public string EnemyId { get; }       // vissza a definícióra (katalógus-kulcs)
    public EnemyType Type { get; }
    public LaneIndex Lane { get; }
    public Vec2 Position { get; }
    public int CurrentHealth { get; }
    public int MaxHealth { get; }         // EnemyDefinition.MaxHealth × EnemyHealthMultiplier
    public bool IsAlive { get; }
}

public readonly struct CombatModifiers
{
    public float EnemySpeedMultiplier { get; }     // 0.5 .. 2.0
    public float EnemyHealthMultiplier { get; }    // 0.5 .. 2.0
    public float EnemyDamageMultiplier { get; }    // 0.5 .. 2.0
    public float SpawnRateMultiplier { get; }      // 0.5 .. 2.0
    public static CombatModifiers Neutral { get; }
}
```

**Kötelező harci szabályok (spec 4.2, 17.4):**

1. Tüzelés: a kiválasztott fegyver `1 / FireRate` ütemében, ha van célpont.
   Lőszerkészlet, fogyasztás, dry-fire és újratöltés nincs; minden típus mindig végtelen (ADR-017).
2. Célpontválasztás a `TargetingMode` szerint: alap `OwnLaneThenAdjacent` — először a
   játékos sávjában a legközelebbi élő ellenség, csak ha nincs, akkor a szomszédos sáv.
3. `ProjectileSpeed == 0` ⇒ hitscan (azonnali találat); különben a lövedék a `Tick`-ben halad
   és ütközéskor sebez.
4. **A matematikai kapu-/válaszhordozó egységek NEM célpontok** (spec 3.3): külön entitáslista,
   a `CombatService` nem is látja őket; a `MathEventDirector` kezeli a pozíciójukat.
5. Ellenség a játékos vonalát elérve `ContactDamage`-et okoz, majd `AttackCooldownSeconds`-ig
   nem sebez újra; a `PlayerHealth` `ApplyDelta(Health, -dmg, "ENEMY_CONTACT")`-on keresztül fogy.
   **Pajzs-sorrend (GAME_DESIGN.md 6.1):** ha van `Shield` pool és `GetCurrent(Shield) > 0`,
   a sebzés **először** a pajzsból fogy 1:1 arányban (`ApplyDelta(Shield, -x, "ENEMY_CONTACT")`),
   és csak a maradék megy a `Health`-re. A pajzs tehát puffer, nem külön életerő.
6. `Health <= 0` ⇒ `IsPlayerAlive = false` + `PlayerDamaged` majd a `GameSessionService`
   `SessionEnded(Defeat, HealthDepleted)`.
7. Ellenség `CurrentHealth <= 0` ⇒ `EnemyKilled`, az instance a poolba kerül (az instance-id
   nem használható újra ugyanabban a session-ben). Kill után nincs erőforrás-visszatérítés; az `EnemyKilled` önmagában nem jutalom (ADR-017).

---

### 3.1. Fegyvertípus-választás — F1-21 (ADR-017)

`ICombatService` további felülete: `string CurrentWeaponId { get; }` és
`Result<bool> SelectWeapon(string weaponId, double gameTime)`. Az Initialize a
pálya rendezett `AvailableWeaponIds` halmazához tartozó immutable definíciókat
kapja; hiány/duplikáció/idegen Id startup konfigurációs hiba. Kezdetben WeaponId aktív.
A session listája ebből a másolt katalógusból olvas, nem keres SO-t Tickben.
A session SelectWeapon kizárólag futó/élő/nem szünetelő állapotban továbbít.
Hibák: `SESSION_NOT_RUNNING`, `SESSION_PAUSED`, `WEAPON_NOT_AVAILABLE`; azonos típus
`Ok(false)`, váltás `Ok(true)` és pontosan egy WeaponChanged struct esemény.
Váltáskor nincs lövés; `nextShotAt = max(oldNextShotAt, t + 1/newFireRate)`.
Már repülő lövedék eredeti sebzése/sebessége megmarad. Nincs resource/math hívás,
RNG vagy managed allokáció. Az ADR-017 rögzíti a két szállított típust és a HUD-utat.

## 4. IWaveDirector (spec 4.1)

```csharp
public interface IWaveDirector
{
    void Initialize(LevelDefinition level, IRandomSource random);

    /// Hullám-ütemezés. SOHA nem kérdezi le és nem várja meg a matematikai eseményt
    /// (spec 14.: "A WaveDirector ettől függetlenül működtesse az ellenséges hullámokat").
    void Tick(double gameTime);

    int CurrentWaveIndex { get; }
    int TotalWaves { get; }
    bool AllWavesCompleted { get; }
    bool IsBossActive { get; }

    /// A kibocsátandó spawn-parancsok a Tick során; a GameSessionService üríti és
    /// továbbítja az ICombatService.SpawnEnemy felé.
    IReadOnlyList<SpawnCommand> PendingSpawns { get; }
    void ClearPendingSpawns();

    void ApplyCombatModifiers(CombatModifiers modifiers);
}

public readonly struct SpawnCommand
{
    public string EnemyId { get; }
    public LaneIndex Lane { get; }
    public float SpawnDepth { get; }
    public double ScheduledGameTime { get; }
}
```

**Szabály:** két matematikai feladat között sem állhat le tartósan a spawnolás
(spec 4.1) — a `WaveDefinition`-ök időben átfedhetnek vagy közvetlenül követik egymást; a
hullámok közötti maximális szünet a `CombatBalanceSO.MaxWaveGapSeconds` (alap 2 mp).

---

## 5. IMathEventDirector (spec 5., 13., 14.)

```csharp
public readonly struct MathEventId : IEquatable<MathEventId>
{
    public Guid Value { get; }
    /// Determinisztikus: a session seedjéből és az esemény sorszámából képzett GUID.
    public static MathEventId Create(int sessionSeed, int eventIndex);
}

public enum MathEventState
{
    Inactive = 0, Preview = 1, Approaching = 2, AwaitingDecision = 3,
    Resolving = 4, Completed = 5, Expired = 6
}

public interface IMathEventDirector
{
    void Initialize(LevelDefinition level, DifficultyProfile difficulty,
                    IMathProblemGenerator generator, IRandomSource random);

    /// KIZÁRÓLAG a session Ticken kívül. Minden még előkészítetlen eseményhez
    /// teljes, validált MathProblem-et generál (MathEngine 7.).
    /// Ismételt, változatlan kontextusú hívás cache-találat; true = minden kész.
    Result<bool> PrepareUpcomingEvents(double gameTime);

    /// Léptetés. NEM módosít harci állapotot, NEM olvas erőforrást és NEM hívja
    /// az IGameClock.Pause-t. A feloldásokat parancsként sorba állítja
    /// (PendingResolutions), nem alkalmazza.
    void Tick(double gameTime);

    /// A játékos sávváltása. Csak AwaitingDecision állapotú eseményre hat.
    /// Idempotens: ugyanazon eventId második választása figyelmen kívül marad.
    void OnPlayerLaneSelected(LaneIndex lane, double gameTime);

    /// A session vége: minden nem terminális esemény Expired-be kerül.
    /// A már Completed eseményeket NEM vonja vissza.
    void OnSessionEnded(double gameTime);

    /// A session visszajelzése a jutalom feldolgozása és az attempt naplózása után.
    /// Nem alkalmaz jutalmat; ismételt/késői jelzés nem módosít terminális eseményt.
    void OnRewardProcessed(in RewardApplication application, double gameTime);

    IReadOnlyList<ActiveMathEvent> ActiveEvents { get; }
    IReadOnlyList<ResolveCommand> PendingResolutions { get; }
    void ClearPendingResolutions();

    /// Fázis 4: adaptív időnyomás a következő eseményektől kezdve.
    /// A MÁR AKTÍV események paraméterei nem változnak (spec 7.5).
    void ApplyTimePressure(float thinkTimeMultiplier, float gateSpeedMultiplier);
}

public sealed class ActiveMathEvent
{
    public MathEventId EventId { get; }
    public MathEventState State { get; }
    public ProblemType Type { get; }
    /// Resource-kapunál csak Approaching-tól (a fokozatos felfedés miatt), minden
    /// más típusnál Preview-tól. Ugyanaz az előre legenerált MathProblemId.
    public MathProblemId? ProblemId { get; }
    /// Az előkészítés során legenerált, immutable feladat. Nem-resource típusnál
    /// Preview-tól, resource-kapunál Approaching-tól NEM null; előtte null.
    /// Az Approaching-ba lépés CSAK referencia-hozzárendelés (nincs generálás,
    /// erőforrás-olvasás, RNG vagy allokáció — G4).
    public MathProblem Problem { get; }
    /// A fix jutalom erőforrása; az ADR-018 óta mindig ResourceType.Health.
    public ResourceType RewardResource { get; }
    /// A fix jutalom összege (Problem.Reward.FlatAmount), már Preview-ban ismert.
    public int RewardAmount { get; }
    public GateGoal Goal { get; }
    public int? TargetValue { get; }
    public double PreviewStartedAt { get; }
    public double DecisionOpenedAt { get; }
    public double DeadlineGameTime { get; }
    /// ResourceOperationComparison esetén a feladat közös, GENERÁLT bázisértéke
    /// (Problem.GateBaseValue); Approaching előtt és más típusnál 0.
    /// NEM erőforrásállás (ADR-018).
    public int GateBaseValue { get; }
    public IReadOnlyList<GateCarrier> Carriers { get; }
}

public readonly struct GateCarrier
{
    public int OptionIndex { get; }
    public LaneIndex Lane { get; }
    public Vec2 Position { get; }          // a Presentation ezt képezi le 3D-re
    public string DisplayLabel { get; }    // "×2", "+25", "56"
    public bool IsCorrect { get; }         // a Presentation NEM jelenítheti meg döntés előtt!
}

public readonly struct ResolveCommand
{
    public MathEventId EventId { get; }
    public LaneIndex SelectedLane { get; }
    public bool TimedOut { get; }
    public double GameTime { get; }
}
```

**Kivezetett tagok (ADR-019):** `ActiveMathEvent.ResourceGate`
(`ResourceGateBinding?`), `SnapshotValue`, `SnapshotResource`. Visszavezetésük
kontraktsértés.

**Állapotgép-időzítés:**

```
t0                      : Inactive -> Preview
                          (resource: cél + fix jutalom; más: teljes feladat)
t0 + PreviewDuration    : a kész MathProblem publikálása -> Approaching (resource);
                          kapuk/carrierek spawnolása, GateBaseValue láthatóvá válik
kapuk a döntési zónában : Approaching -> AwaitingDecision
DecisionOpenedAt + ThinkTime : -> Expired, ha nincs választás
LaneSelected            : AwaitingDecision -> Resolving  (ResolveCommand sorba állítva)
RewardProcessed         : Resolving -> Completed (Applied vagy SkippedIncorrect)
```

**Függetlenségi szerződés (spec 14., 23./4. — kikényszerített, nem csak leírt):**

| Kikényszerítés | Hogyan |
|---|---|
| `IMathEventDirector` konstruktorában/`Initialize`-jában nincs `ICombatService` és nincs `IWaveDirector` paraméter | fordítási szinten lehetetlen a hívás |
| `ICombatService`/`IWaveDirector` konstruktorában nincs `IMathEventDirector` | ua. |
| A matek-állapotgép nem hívhat `IGameClock.Pause()`-t | a `IGameClock` mutáló metódusait csak a `GameSessionService` kapja meg |
| A `CombatService` entitáslistája nem tartalmaz `GateCarrier`-t | külön lista, külön típus (3. szakasz 4. szabály) |
| **A matek-feladat tartalma nem függ a harctól** (ADR-018, G8) | a `MathEventDirector` `Tick`-je nem hív `IResourceService`-t; a feladat a `Tick` előtt készen van |
| Teszt-kapu | `MathEventDirectorIndependenceTests`: `MathEventDirector` 10 000 Tick-en végigfut `ICombatService` példány NÉLKÜL; `CombatService` ugyanígy `IMathEventDirector` nélkül; továbbá ugyanazon seeddel generált esemény tartalma azonos, akármilyen erőforrás-állapot mellett futtatjuk |

> `IsCorrect` a `GateCarrier`-ben **csak a feloldás után** használható fel vizuálisan.
> Döntés előtt a Presentation nem jeleníthet meg semmit belőle, és az opciók
> **értékét** sem (spec 5.1: „A két kapu eredményét a döntés előtt ne jelenítsd meg”).
> A Presentation a műveleti címkét kizárólag a `Carriers[].DisplayLabel`-ből veheti,
> az `ActiveMathEvent.Problem.Options` nem prezentációs adat. Ezt a code-reviewer ellenőrzi.

### 5.1. Implementációs határok és lezárás

A konkrét `MathEventDirector` konstruktora az 5.2. szerinti Unity-mentes beállítást
és meglévő `IResourceService`/`IGameEventSink` függőséget kapja; továbbra sem kaphat
`ICombatService` vagy `IWaveDirector` referenciát. Az `IResourceService` függőség
**kizárólag** a jutalom-oldali diagnosztikára és a hostkonfiguráció ellenőrzésére
szolgál: a director `Tick`-ben és `Prepare`-ben **nem olvas** erőforrásállást vagy
`Max`-ot (ADR-018; a korábbi `CaptureSnapshot`/`GetMax` hívás törölve). A konfigurációs
assetek későbbi implementációja nem jogosít beégetett balance-értékekre: a director
tesztben explicit bemeneti értékekkel is létrehozható.

Az `ApplyTimePressure` az ezután `Preview`-ba belépő események időnyomását állítja.
A director belépéskor rögzíti az esemény effektív idejét és sebességét. A gondolkodási
alapidő a schedule pozitív override-ja, különben a profil alapideje; az effektív idő
ennek és a rögzített `thinkTimeMultiplier`-nek a szorzata. A határidő
`DecisionOpenedAt + effektív idő` (ARCHITECTURE 6.3.). Az előkészített, immutable
`MathProblem.ThinkTimeSeconds` alapidőt hordozhat; a szorzó és az effektív idő a
director futásidejű állapota. Ehhez nem kell `MathProblem`-et mutálni vagy
`Tick`-ben másolatot létrehozni. Az előzőleg már aktív esemény effektív ideje,
sebessége és határideje későbbi `ApplyTimePressure` hívásra sem változik.

`OnRewardProcessed` a már előírt jutalom utáni lezárás bejövő útja. A session a
`RewardApplication` eredményét közvetíti; a director nem hívja a feloldó- vagy
jutalomszolgáltatást. Azonos EventId-jú, `Resolving` eseménynél `Applied` és
`SkippedIncorrect` → `Completed` + pontosan egy `MathEventCompleted` az application
`AppliedDelta`/`Resource` adataival; hibás válasznál a delta 0. Az `AlreadyApplied`
és `SkippedExpired` nem indít új `Completed` átmenetet. Ismeretlen EventId és
már terminális esemény jelzése hatástalan. Az ismételt visszajelzés nem ismétel
jutalmat, átmenetet vagy completion-eseményt. A már `Expired` esemény nem válhat
`Completed`-dé. A jelzés önmagában nem mutál erőforrást, és nem engedélyez
Tick-allokációt. Indoklás: [ADR-015 4.](../decisions/ADR-015.md#4-resolving--completed-bejövő-visszajelzés).

### 5.2. Előkészítés és hibák — F3 szerződés (ADR-018/019 szerinti végleges alak)

```csharp
// Namespace: MathHorde.GameLogic. Immutable, másolt olvasó nézetű konfiguráció.
public sealed class MathEventDirectorSettings
{
    public MathEventDirectorSettings(int maxConcurrentMathEvents, float gateSpawnDepth,
        float decisionZoneDepth, float gateApproachSpeed, float feedbackDisplaySeconds,
        GateGenerationLimits gateLimits,
        IReadOnlyDictionary<int, RewardSpec> rewardsByScheduleIndex,
        IReadOnlyDictionary<int, int> targetsByScheduleIndex = null);
    public int MaxConcurrentMathEvents { get; }
    public float GateSpawnDepth { get; }
    public float DecisionZoneDepth { get; }
    public float GateApproachSpeed { get; }
    public float FeedbackDisplaySeconds { get; }
    /// A MathEventConfigSO globális kapu-korlátai (SO-séma 6.), amelyeket a director
    /// változatlanul továbbít a ProblemGenerationRequest.GateLimits mezőjébe.
    /// Érvénytelen tartomány startup konfigurációs hiba (GATE_LIMITS_INVALID).
    public GateGenerationLimits GateLimits { get; }
    public IReadOnlyDictionary<int, RewardSpec> RewardsByScheduleIndex { get; }
    public IReadOnlyDictionary<int, int> TargetsByScheduleIndex { get; }
}

public MathEventDirector(IResourceService resources, IGameEventSink events,
                         MathEventDirectorSettings settings);
```

A schedule-index az eredeti `LevelDefinition.MathEvents` lista 0-alapú indexe;
idő szerinti rendezés után is ezt kell használni az ID-hez és a mapekhez. Az
értékek/tartományok a SO-séma 5.1., 6., 6.1.-ből jönnek; SpawnDepth > ZoneDepth,
sebesség pozitív és véges. Reward bejegyzés minden schedule-elemre kötelező:
`Resource == Health`, `FlatAmount > 0`, `0 <= IncorrectAmount <= FlatAmount`
(MVP-ben `IncorrectAmount == 0`) — ADR-018. `UseSnapshotDelta` mező nem létezik.
`TargetValueSelection`, illetve `ResourceOperationComparison` + `ReachTarget` esetén
a targetmap bejegyzése kötelező, máskor nem alkalmazandó. A konfigurációs adapter
oldja fel a SO override-jait/tábláit **Initialize előtt**, nem a director talál ki
jutalmat vagy hiányzó célértéket.

Initialize a teljes schedule méretéhez előre lefoglalja az eseményobjektumokat,
állapotgépeket, legfeljebb négy carrier/hely per eseményt, pending command- és
diagnosztikai buffereket. A Tick nem növelhet listakapacitást. Az EventId a session
seedjéből és az eredeti schedule-indexből jön; a session seed az Initialize random
forrásának Seed értéke (nem új húzás). Az esemény generálási seedje
`random.Derive("math-event:" + index invariant-decimal)` forrás Seedje; ez a
string és az ID is Initialize/Prepare alatt keletkezik, nem Tickben.

**Prepare (a session Ticken kívül):** minden előkészítetlen schedule-elemhez
`IMathProblemGenerator.Generate`-tel teljes, validált `MathProblem`-et készít —
`ResourceOperationComparison`-ra és minden más típusra **ugyanazon az úton**
(MathEngine 7.). A request összeállítása:

| Request-mező | Forrás |
|---|---|
| `Seed` | `random.Derive("math-event:<index>")` Seed értéke |
| `Type`, `Goal` | `MathEventScheduleEntry` |
| `Difficulty` | az `Initialize`-ban átadott `DifficultyProfile` |
| `LaneCount` | `LevelDefinition.LaneCount` |
| `ThinkTimeSeconds` | pozitív schedule-override, különben `Difficulty.BaseThinkTimeSeconds` |
| `RewardResource`, `Reward` | `RewardsByScheduleIndex[index]` (kötelezően `Health`) |
| `GateLimits` | `Settings.GateLimits`, **csak** `ResourceOperationComparison`-nál; máskor `null` |
| `TargetValue` | `TargetsByScheduleIndex[index]`, ha a típus/cél megköveteli |

A director **nem** olvas `GetCurrent`/`GetMax`-ot, nem számol kapacitást és nem
rendel hozzá erőforrásállást. Hibakódok Prepare-ből (`Result.Fail`):
hiányzó/érvénytelen reward → `MATH_EVENT_CONFIG_INVALID`; hiányzó célérték →
`GATE_TARGET_MISSING`; hiányzó/ellentmondó kapu-korlát → `GATE_LIMITS_INVALID`;
tiltott jutalom-erőforrás → `REWARD_RESOURCE_NOT_ALLOWED` (Ammo:
`AMMO_NOT_ALLOWED`); generátor-kimerülés → `GENERATION_EXHAUSTED`.
Nincs kitalált fallback resource, profil, cél vagy jutalom.

Resource `Preview`-ben `Problem` és `ProblemId` null, `Carriers` üres,
`GateBaseValue == 0`, és a Preview-esemény `DisplayExpression = string.Empty`;
csak a `Goal`, a `TargetValue` és a fix `RewardAmount` előjelezhető. Nem-resource
`Preview`-ben a `Problem`/`ProblemId` és a teljes kifejezés már elérhető.

A Preview lejártakor a resource-esemény **egyszerűen publikálja** az előre
legenerált feladatot: `Problem`/`ProblemId` beállítása, `GateBaseValue =
Problem.GateBaseValue.Value`, a carrierek feltöltése az opciók
`Lane`/`DisplayLabel`/`IsCorrect` adataiból, állapot `Approaching`, majd
`MathEventGatesSpawned(EventId, GateBaseValue, t)`. Ez a konkrét feladat
kezdete: innentől a bázisérték és a műveletek láthatók, az **eredmények** nem.
Nincs `Bind`, nincs erőforrás-olvasás, nincs új RNG-húzás és nincs allokáció.
A pozíció a rögzített `GateApproachSpeed * gateSpeedMultiplier` és a
játékidő-különbség alapján halad `SpawnDepth`-től `ZoneDepth`-ig; zónába lépéskor
nyílik a döntési ablak. A `GateApproachSeconds` design-idő nem második,
geometriafüggetlen ütközési óra.

**Erőforrás-változás hatása:** nincs. Mivel a feladatnak nincs erőforrás-bemenete,
a `Max` vagy a `Current` menet közbeni változása nem érvénytelenít előkészített
vagy aktív eseményt, és nem igényel újraelőkészítést (a korábbi
„Kapacitásváltozás” szabály törölve). A jutalom jóváírásakor a `Max` vághat
(2.3.), ami normál működés.

**Hibaág:** Prepare az első hibakódot `Result.Fail`-ként adja vissza, de minden
schedule-elemet ellenőriz; a hibás elemeket megjelöli, a sikeres cache-t megőrzi.
Még az első gameplay Tick előtt észlelt hiba esetén a host konfigurációs hibaként
jelenti, és nem indít szimulációs frame-et. Ez nem játék közbeni matekszünet.
Ha futó sessionben egy új elem előkészítése hibás, az érintett, még nem
véglegesített esemény `Expired` lesz, `MathEventPreparationFailed` és
`MathEventExpired(PREPARATION_FAILED)` keletkezik. Nincs `ResolveCommand`, jutalom,
`MathAnswer`/`QuestionAttempt` vagy gyermek-timeout; a host a frame után jelzi a
tartalomhibát. A harci Tick sorrendje és a többi érvényes esemény tovább fut.
Előkészítés nélkül esedékes esemény ugyanezt az ágat használja
`MATH_EVENT_NOT_PREPARED` kóddal, Tickbeli lusta generálás helyett.
Ez nem normál pályaelfogadási siker: F3-integráció nem lehet zöld ilyen diagnosztikával.
A deadline miatti Expired továbbra is `TimedOut = true` attempt; sessionvégi megszakítás
`SESSION_ENDED` okú, és nem gyárt gyermek-timeoutot egy el nem indult feladatra.

A host pontos hívási helye a [Presentation-kontrakt 1.](presentation.md)-ben van.
A `Tick` belsejében és az onnan szinkron hívott eseménykezelőkben a G4 változatlan.

Az ActiveEvents stabil, előre lefoglalt olvasó nézet: nem tartalmaz Inactive
elemet; a terminális elemet FeedbackDisplaySeconds ideig még megtartja, hogy a
Tick utáni render lássa. A párhuzamossági korlát a nem terminális eseményekre
vonatkozik. Esedékes, de szabad helyre váró esemény Inactive marad, nem vész el;
azonos triggeridőnél az eredeti schedule-index dönt. A PreviewStartedAt a tényleges
belépési idő, ezért a várakozás nem rövidíti a Preview-t. Egyazon EventId választása,
határideje és completion-je továbbra is legfeljebb egyszer kerül sorba.
Előkészítési hibára/sessionvégre az F3-01 helper SessionEnded átmenete használható
az Inactive/Preview → Expired lezáráshoz; a külső ReasonCode ettől függetlenül a
valódi ok. Új állapotgép-trigger vagy Enum-érték nem szükséges.

---

## 6. IGateResolutionService

```csharp
public interface IGateResolutionService
{
    /// Kiértékel egy feloldási parancsot. IDEMPOTENS: ugyanarra az EventId-ra a második
    /// hívás GateResolution.Duplicate-et ad vissza, és NEM nyúl az erőforrásokhoz.
    GateResolution Resolve(in ResolveCommand command, ActiveMathEvent mathEvent);

    /// Már feloldott-e az esemény (mentés-visszatöltés után is helyes).
    bool IsResolved(MathEventId eventId);

    /// A session kezdetekor / visszatöltéskor az idempotencia-napló helyreállítása.
    void RestoreResolvedEvents(IReadOnlyCollection<Guid> resolvedEventIds);
}

public readonly struct GateResolution
{
    public MathEventId EventId { get; }
    public GateResolutionOutcome Outcome { get; }
    public bool WasCorrect { get; }
    public int SelectedOptionIndex { get; }     // -1, ha timeout
    /// A választott opció matematikai eredménye — KIZÁRÓLAG a visszajelzés
    /// megjelenítéséhez (spec 5.1 „lássa mindkét művelet eredményét”).
    /// Jutalmat NEM képez (ADR-018). Timeoutnál 0.
    public int OperationResult { get; }
    /// ResourceOperationComparison esetén a feladat generált bázisértéke
    /// (visszajelzéshez); más típusnál 0. NEM erőforrásállás.
    public int GateBaseValue { get; }
    /// A FIX jutalom: helyes válasznál RewardSpec.FlatAmount, hibásnál
    /// RewardSpec.IncorrectAmount (alap 0), timeout/duplikátum esetén 0.
    /// MINDKÉT kötelező feladattípusnál ugyanez a szabály (ADR-018 2. pont).
    public int RewardDelta { get; }
    public ResourceType Resource { get; }       // mindig Health
    public MathAnswerSnapshot Answer { get; }   // readonly struct; Analytics később materializálja
}

public enum GateResolutionOutcome
{
    CorrectRewarded = 0, IncorrectNoReward = 1, Expired = 2, Duplicate = 3
}
```

**Feloldási szabályok:**

A feloldó **mindkét** típusnál ugyanabból az adatból dolgozik:
`mathEvent.Problem.Options` (Lane, OptionIndex, Value, IsCorrect) és
`mathEvent.Problem.Reward`. Nem generál, nem bindol, nem olvas erőforrást és nem
dereferálhat null `Problem`-et: ha a `Problem` még null (az esemény nincs
`AwaitingDecision`-ben), normál választással nem oldható fel; a
deadline-commandhoz a már publikált feladat metaadatai tartoznak.
`PREPARATION_FAILED` nem timeout-command. A `MathAnswerSnapshot` ProblemId-je
mindkét úton ugyanaz, amelyet az `ActiveMathEvent` a konkrét feladat kezdetétől
publikál.

| Eset | `Outcome` | `RewardDelta` |
|---|---|---|
| helyes sáv, `ResourceOperationComparison` | `CorrectRewarded` | `Reward.FlatAmount` |
| helyes sáv, `AnswerLaneSelection` | `CorrectRewarded` | `Reward.FlatAmount` |
| hibás sáv (bármely típus) | `IncorrectNoReward` | `Reward.IncorrectAmount` (alap 0) |
| határidő lejárt | `Expired` | 0 |
| ugyanaz az `EventId` másodszor | `Duplicate` | 0, erőforrás nem változik |

A korábbi `RewardDelta = OperationResult − SnapshotValue` szabály **törölve**
(ADR-018). A két mechanika jutalma ugyanazzal a mechanizmussal működik; csak a
konfigurált összeg tér el (SO-séma 6.1.).

---

## 7. IRewardService

```csharp
public interface IRewardService
{
    /// Idempotens jutalomkönyvelés (ARCHITECTURE 6.2).
    /// Első hívás az EventId-ra: alkalmaz és Applied-et ad.
    /// Bármely további hívás: AlreadyApplied, erőforrás-változás NÉLKÜL.
    RewardApplication ApplyReward(in GateResolution resolution);

    /// Nem matematikai jutalom — nem idempotencia-köteles, mert nincs esemény-azonosítója.
    /// Meglévő használata: BOSS_PHASE: boss-fázisok közti életerő-jóváírás (GAME_DESIGN.md 6.1: +15 HP),
    /// illetve (F5-től) a boss legyőzéséért adott pajzs-jóváírás — ez NEM matematikai jutalom.
    ResourceChange ApplyCombatReward(ResourceType type, int amount, string reasonCode);

    IReadOnlyCollection<Guid> AppliedRewardEventIds { get; }
    void RestoreAppliedRewards(IReadOnlyCollection<Guid> eventIds);
}

public readonly struct RewardApplication
{
    public MathEventId EventId { get; }
    public RewardApplicationStatus Status { get; }
    public int RequestedDelta { get; }
    public int AppliedDelta { get; }     // Max-vágás után
    public ResourceType Resource { get; }
}

public enum RewardApplicationStatus
{
    Applied = 0, AlreadyApplied = 1, SkippedIncorrect = 2, SkippedExpired = 3
}
```

**Jutalomhatár (ADR-017 + ADR-018):** `ApplyReward` kizárólag `Health` jutalmat
könyvelhet el. `Ammo` ⇒ `ArgumentException` (programozói hiba, ADR-017);
`Shield`/`EnemyArmor`/`Energy` matematikai jutalomként ⇒ ugyancsak
`ArgumentException`, a pool és az idempotencia-napló módosítása **előtt**.
A `RewardApplicationStatus` nem kaphat hamis `Applied`/`SkippedIncorrect` értéket.
`ApplyCombatReward` továbbra is módosíthat `Shield`-et `"BOSS_PHASE"`/`"PICKUP"`
indokkal — ez harci, nem matematikai jutalom, ezért nem sérti az ADR-018-at.
`RequestedDelta` a fix összeg, `AppliedDelta` a `Max`-vágás utáni tényleges érték.

---

## 8. IGameSessionService — a kompozíciós gyökér

```csharp
public interface IGameSessionService
{
    GameSessionId SessionId { get; }
    SessionResult Result { get; }
    bool IsRunning { get; }

    // A meglévő F1 explicit kompozíció végrehajtható bemenete, F1-21-ben listára bővült:
    void StartSession(LevelDefinition level, IReadOnlyList<WeaponDefinition> weapons,
                      IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog, int seed);
    IReadOnlyList<WeaponDefinition> AvailableWeapons { get; }
    string CurrentWeaponId { get; }
    Result<bool> SelectWeapon(string weaponId);

    /// AZ EGYETLEN helyen definiált Tick-sorrend (ARCHITECTURE 6.1).
    /// A Presentation (MonoBehaviour.Update) ezt hívja, mást nem.
    void Tick(float deltaTime);

    void Pause();
    void Resume();

    /// Pontosan egyszer fut le; idempotens. Előállítja a GameSessionRecord-ot.
    GameSessionRecord Finalize(SessionResult result, FailureReason reason);
}
```

**Kötelező `Tick` implementációs sorrend (ettől eltérni TILOS):**

```csharp
// pszeudokód, a szerződés része
_clock.Advance(deltaTime);
double t = _clock.GameTime;
if (_clock.IsPaused) return;          // szünetben sem harc, sem határidő nem halad

_waveDirector.Tick(t);
foreach (var cmd in _waveDirector.PendingSpawns)
    _combatService.SpawnEnemy(cmd.EnemyId, cmd.Lane, cmd.SpawnDepth);
_waveDirector.ClearPendingSpawns();

_combatService.Tick(t, deltaTime);    // HARC — a mateseménytől FÜGGETLENÜL

_mathEventDirector.Tick(t);           // matek állapotgép — harcot NEM módosít,
                                      // erőforrást NEM olvas (G8)

foreach (var res in _mathEventDirector.PendingResolutions)
{
    var resolution = _gateResolutionService.Resolve(res, _mathEventDirector.Find(res.EventId));
    var application = _rewardService.ApplyReward(resolution); // idempotens
    _performanceTracker.Record(resolution.Answer);   // struct + immutable metadata előre foglalt sorba
    _mathEventDirector.OnRewardProcessed(application, t);
}
_mathEventDirector.ClearPendingResolutions();

EvaluateVictoryOrDefeat(t);
```

A `PrepareUpcomingEvents` **nem** ebben a hurokban fut: a host a frame előtt/után,
a szimulációs lépésen kívül hívja (Presentation-kontrakt 1.).

**Győzelem/vereség (spec 1.2, 1.3):**
- Győzelem: `WaveDirector.AllWavesCompleted && !IsBossActive && Health > 0 &&
  MathAccuracy >= LevelDefinition.RequiredMathAccuracy`.
- Vereség: `Health <= 0` ⇒ `Defeat / HealthDepleted`.
- Ammo-kifogyás és új AmmoStarvation diagnózis nincs (ADR-017); történeti ordinal
  olvasható, de nem vezérel DDA-t. A tüzelés a készlettől független.

---

## 9. ILevelProgressionService (Fázis 5 — vázlat)

```csharp
public interface ILevelProgressionService
{
    PlayerProgress RegisterSessionResult(PlayerProgress progress, GameSessionRecord session);
    bool IsLevelUnlocked(PlayerProgress progress, LevelId levelId);
    /// A WorldDefinition.UnlockRequirement MINDEN kitöltött tagját ellenőrzi (ÉS-kapcsolat):
    /// csillag-arány + készség-fokozat (SkillProfile.GetMasteryLevel) + opcionális CL.
    bool IsWorldUnlocked(PlayerProgress progress, WorldId worldId);
    /// 0..3 csillag. A küszöbök NEM kódba égetettek: `StarRatingSO`
    /// (scriptable-object-schema.md 14.), GAME_DESIGN.md 7.1 értékeivel.
    int CalculateStars(GameSessionRecord session, LevelDefinition level);
    IReadOnlyList<string> GrantRewards(PlayerProgress progress, GameSessionRecord session);
}
```

**Csillag-szabályok (GAME_DESIGN.md 7.1, `StarRatingSO`-ból):**

| Csillag | Feltétel |
|---|---|
| ★1 | `Result == Victory` |
| ★2 | ★1 **és** `MathAccuracy >= Star2MinAccuracy` (alap 0.70) |
| ★3 | ★2 **és** `MathAccuracy >= Star3MinAccuracy` (alap 0.90) **és** `TimedOutQuestions <= Star3MaxTimeouts` (alap 0) |

Sebességtől függő csillag **nincs** (spec 12.).

---

## 10. IAdaptiveDifficultyService — VÁZLAT (Fázis 4, spec 7.)

> Ez a szakasz még **nem kötelező érvényű kontrakt**; a Fázis 4 indulásakor az architect
> mélyíti. A szignatúrák iránymutatók, a viselkedési táblázat viszont már most köti a
> balance-designer agentet.

```csharp
public interface IAdaptiveDifficultyService
{
    /// Pályák közötti igazítás (spec 7.4).
    DifficultyAdjustment EvaluateBetweenLevels(PlayerProgress progress,
                                               GameSessionRecord lastSession);

    /// Pályán belüli segítség (spec 7.5) — csak az ÚJ eseményekre és hullámokra hat.
    InLevelAssistance EvaluateInLevel(IReadOnlyList<MathAnswer> recentAnswers,
                                      ResourceState resources, double gameTime);

    /// A három dimenzió szétválasztása (spec 7.3).
    DifficultyProfile Recalculate(DifficultyProfile current, SkillProfile skills,
                                  PerformanceWindow window);
}

public readonly struct InLevelAssistance
{
    public int Tier { get; }                      // 0 = nincs segítség, 1..4
    public float ThinkTimeMultiplier { get; }     // 1.0 .. 1.8
    public float GateSpeedMultiplier { get; }     // 0.6 .. 1.0
    public int MathComplexityDelta { get; }       // -2 .. 0
    public int LaneCountOverride { get; }         // 0 = nincs felülírás, egyébként 2..4
    public CombatModifiers CombatModifiers { get; }
    public bool TriggerRescueWave { get; }
}
```

**Már most rögzített viselkedési szabályok:**

| Trigger | Reakció |
|---|---|
| pálya első próbálkozásra teljesítve | `ChallengeLevel + 1` |
| második próbálkozásra teljesítve | nincs változás |
| ugyanazon pálya 2 egymást követő bukása | `ChallengeLevel − 1` |
| további 2 egymást követő bukás | ismét `−1` |
| mindig | `1 <= ChallengeLevel <= 10` |
| 3 egymást követő hibás matek-válasz | `InLevelAssistance` aktiválódik |
| `LossDiagnosis == EnemyBreakthrough` és pontosság >= 80% | csak `CombatDifficulty` csökken |
| jól számol, de lassan (timeout-arány > 20%) | csak `TimePressure` csökken (több idő) |
| globális szint emelkedik | **nem** old fel nem tanult készséget (`SkillProfile.GetMasteryLevel(tag) >= Introduced` kötelező) |
| minden változtatás | `DifficultyAdjustment` naplóba kerül (visszakövethetőség, spec 7.6) |

Hiszterézis: a `ChallengeLevel` legfeljebb `±1` egy pálya után, és a `Recalculate` a három
dimenziót pályánként legfeljebb `±1`-gyel mozdítja (spec 7.6 „lassan változzon”).
A konkrét számértékek: `balance/difficulty-model.md` 6. szakasz; a konfiguráció helye
`AdaptiveDifficultyConfigSO` + `InLevelAssistSO` (scriptable-object-schema.md 8.1, 8.2).

---

## 11. Infrastruktúra-portok (a GameLogic által definiált, kívül implementált interfészek)

```csharp
public interface ISaveRepository
{
    PlayerProgress LoadProgress();                       // sérült fájl ⇒ alapértelmezett profil
    void SaveProgress(PlayerProgress progress);          // atomi írás
    void AppendSession(GameSessionRecord session);
    IReadOnlyList<GameSessionRecord> LoadSessions(int maxCount);
    void DeleteAllData();
}

public interface IConfigurationProvider
{
    LevelDefinition GetLevel(LevelId id);
    WorldDefinition GetWorld(WorldId id);
    IReadOnlyList<WorldDefinition> GetAllWorlds();
    EnemyDefinition GetEnemy(string enemyId);
    WeaponDefinition GetWeapon(string weaponId);
    DifficultyProfile GetDifficultyForLevel(int challengeLevel);
    T GetBalanceSection<T>(string sectionKey) where T : class;
}
```

Implementáció: `LocalSaveRepository`, `GameConfigurationRepository` (Infrastructure réteg).

---

## 12. Elfogadási kapcsolat (spec 21.)

| Kritérium | Mely kontrakt-elem fedi |
|---|---|
| A. Folyamatos harc | `ICombatService.Tick`, `IWaveDirector.Tick` |
| B. Erőforrás-kapu (összehasonlító kapu) | `IMathEventDirector` + `ProblemType.ResourceOperationComparison` (önálló bázisérték) + `IGateResolutionService` + `IResourceService.ApplyDelta(Health, FlatAmount, "MATH_GATE")` |
| C. Eredményválasztó sávok | `IMathEventDirector` + `AnswerLaneSelection` + ugyanaz a fix `Health`-jutalom-út |
| F. Valós idejű működés | `IGameSessionService.Tick` sorrend (8. szakasz), G5/G8 szabály, 5. szakasz függetlenségi táblázata |
| G. Erőforráshiány | elmaradó `Health`-jutalom, kontaktsebzés és `HealthDepleted` |
| H. Adaptív nehézség | `IAdaptiveDifficultyService` (Fázis 4) |
