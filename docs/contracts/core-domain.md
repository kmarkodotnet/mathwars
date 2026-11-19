# Kontrakt — Core / Domain réteg

> Assembly: `MathHorde.Core` · Namespace: `MathHorde.Core.*`
> **`noEngineReferences: true` — ebben az assemblyben `using UnityEngine;` TILOS és
> fordítási hibát okoz.** Csak a .NET BCL használható (`System`, `System.Collections.Generic`,
> `System.Linq` — utóbbi forró ciklusban kerülendő).
> Mélység: **MÉLY** (Fázis 1–3 kötelező érvényű).
>
> **Revízió r2 (2026-11-16, architect):** `LevelDefinition.MaxShield` felvéve;
> `EnemyType` 5 értékűre bővítve (`Swarmlet`) explicit numerikus értékekkel (ADR-008);
> `SkillMasteryLevel` bevezetve (ADR-010); `WorldDefinition` feloldási feltétele
> kibővítve; a definíció/snapshot szétválasztás explicit megjegyzéssel rögzítve.
> A dokumentum-hierarchia (kontrakt > design-dokumentum) ADR-009.
>
> **Revízió r3 (2026-11-19, architect, ADR-013):** a `MathProblem` megőrzi az
> erőforrás-kapacitást; a kapuk nyers és kapacitással vágott optimuma egyaránt
> egyértelmű; `ReachTarget` erőforrás-kapu is hordozhat célértéket. Az ADR-012-ben
> már rögzített, önálló `DifficultyProfile.MaxResultValue` mező átvezetve.

## 0. Általános szabályok

| Szabály | Indoklás |
|---|---|
| Minden domén-típus **immutable** vagy explicit mutáló metódussal rendelkezik; publikus settert csak DTO kap | determinizmus, szimulálhatóság |
| Az MVP kizárólag `int` aritmetika; `long`-ra csak túlcsordulás-ellenőrzésnél váltunk | spec 6.2 |
| Minden azonosító erős típus (`readonly struct`), nem csupasz `Guid`/`string` | összekeverhetetlenség |
| Kivétel csak programozói hibánál; üzleti hiba `Result<T>` / enum | headless szimuláció robusztussága |
| `System.Random` példányosítása TILOS (a teljes megoldásban) — minden véletlen `IRandomSource`-on keresztül, `DeterministicRandomSource` implementációval | ADR-006 |
| **Statikus adat és futásidejű állapot külön típus.** `*Definition` = konfigurációból fordított, immutable, session-független adat. `*Snapshot` / `Active*` / `*State` = futásidejű, per-instance állapot a GameLogic rétegben. A kettő **soha nem egyesítendő** (ADR-009) | a definíció megosztható több instance között, poolozható, és a szimuláció tetszőleges számú instance-t futtathat ugyanarra a definícióra |
| **Minden perzisztált vagy SO-ban szerkesztett enum értékei explicit numerikusak** (`Basic = 0, …`), és új érték **csak a lista végére**, új számmal kerülhet; meglévő érték számát átírni TILOS | Unity az enumot `int`-ként szerializálja; sorrend-változás csendben átírja a kész assetek és mentések jelentését |

---

## 1. Common

```csharp
namespace MathHorde.Core.Common;

/// Monoton játékóra. Egyetlen példány él egy GameSession alatt.
public interface IGameClock
{
    /// Eltelt játékidő másodpercben a session kezdete óta. Szünetben nem nő.
    double GameTime { get; }
    bool IsPaused { get; }
    float TimeScale { get; }          // alap: 1.0f; dev-eszköz állíthatja
    void Advance(float deltaTime);    // csak a GameSessionService hívja
    void Pause();
    void Resume();
}

/// Determinisztikus véletlenforrás. Implementáció: DeterministicRandomSource
/// (saját xoshiro128** PRNG, NEM System.Random) — platformfüggetlen seed-stabilitás
/// a dotnet/Unity hibrid teszt-stackhez (ADR-006).
public interface IRandomSource
{
    int Seed { get; }
    int Next(int minInclusive, int maxExclusive);   // maxExclusive > minInclusive
    double NextDouble();                            // [0,1)
    /// Derivált, független gyerek-forrás (pl. "distractors", "waves") — a hívásszám
    /// változása az egyik ágon nem mozdítja el a másikat.
    IRandomSource Derive(string purposeKey);
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }           // csak IsSuccess esetén érvényes
    public string ErrorCode { get; }  // pl. "DIVISION_BY_ZERO"; siker esetén null
    public static Result<T> Ok(T value);
    public static Result<T> Fail(string errorCode);
}

/// 0-bázisú sávindex. Érvényes tartomány: 0 .. LaneCount-1 (LaneCount 2..4).
public readonly struct LaneIndex : IEquatable<LaneIndex>
{
    public int Value { get; }
    public LaneIndex(int value);      // value < 0 → ArgumentOutOfRangeException
}

/// Unity-mentes 2D vektor a szimulációhoz (X = sáv-tengely, Y = mélység/előre).
public readonly struct Vec2 { public float X { get; } public float Y { get; } }
```

---

## 2. Expressions — AST (spec 6.2)

```csharp
namespace MathHorde.Core.Expressions;

public enum BinaryOperator
{
    Addition = 0, Subtraction = 1, Multiplication = 2,
    Division = 3,            // egész osztás; maradék nélküli oszthatóságot a validátor kényszeríti
    Modulo = 4,              // a % b, b > 0
    IntegerDivisionWithRemainder = 5  // eredménye hányados+maradék pár, ld. EvaluationResult
}
```

```csharp
/// Minden csomópont immutable. A látogatót generikusan is lehet használni.
public abstract class ExpressionNode
{
    public abstract TResult Accept<TResult>(IExpressionVisitor<TResult> visitor);
    /// Precedencia-helyes, ember által olvasható alak (pl. "(6 + 4) × 3").
    /// Unicode operátorjelek: + − × ÷ mod. Szám: invariáns kultúra.
    public abstract string ToDisplayString();
    /// Strukturális egyenlőség (nem referencia).
    public abstract override bool Equals(object obj);
    public abstract override int GetHashCode();
}

public sealed class ConstantNode : ExpressionNode
{
    public int Value { get; }
    public ConstantNode(int value);
}

/// A keresett ismeretlen ("?"). Egy kifejezésfában legfeljebb EGY UnknownNode lehet
/// (ProblemValidator kényszeríti) — a spec 5.2 "7 + ? = 3 × 8" esete.
public sealed class UnknownNode : ExpressionNode
{
    public string Symbol { get; }     // alap: "?"
}

public sealed class BinaryOperationNode : ExpressionNode
{
    public BinaryOperator Operator { get; }
    public ExpressionNode Left { get; }
    public ExpressionNode Right { get; }
    /// true, ha a megjelenítéskor a precedencia miatt NEM szükséges, de a feladat
    /// kifejezetten kiírandó zárójelet tartalmaz. ToDisplayString ezt tiszteletben tartja.
    public bool ForceParentheses { get; }
    public BinaryOperationNode(BinaryOperator op, ExpressionNode left, ExpressionNode right,
                               bool forceParentheses = false);
}

/// Egyenlet: Left = Right. Az ismeretlen bármelyik oldalon lehet.
public sealed class EqualityNode : ExpressionNode
{
    public ExpressionNode Left { get; }
    public ExpressionNode Right { get; }
}

public interface IExpressionVisitor<out TResult>
{
    TResult VisitConstant(ConstantNode node);
    TResult VisitUnknown(UnknownNode node);
    TResult VisitBinary(BinaryOperationNode node);
    TResult VisitEquality(EqualityNode node);
}
```

**Precedencia-táblázat (kötelező):**

| Szint | Operátorok | Asszociativitás |
|---|---|---|
| 1 (legerősebb) | zárójel | — |
| 2 | `×`, `÷`, `mod`, `÷r` | balra |
| 3 | `+`, `−` | balra |

**AST-invariánsok:**
1. `BinaryOperationNode.Left/Right` soha nem `null`.
2. `EqualityNode` csak a fa **gyökereként** szerepelhet.
3. `IntegerDivisionWithRemainder` csak **gyökér** vagy `EqualityNode` közvetlen gyermeke lehet
   (mert az eredménye pár, nem skalár).
4. A megoldandó `UnknownNode` legfeljebb egy példányban, és kizárólag
   `ProblemType.MissingOperand` / `EquationSolving` típusú feladatban szerepel.
   A resource-gate műveleti sablon külön eset: opcióként pontosan egy UnknownNode
   a még nem rögzített erőforrás-snapshot behelyettesítési helye (3.1., ADR-015).
5. Az MVP bővítési pontja (spec 6.0.1–6.0.4): új csomóponttípus csak új `ExpressionNode`
   leszármazott + `IExpressionVisitor` bővítés, meglévő típus módosítása nélkül.

---

## 3. Problems

```csharp
namespace MathHorde.Core.Problems;

public enum ProblemType
{
    ResourceOperationComparison = 0,  // 5.1 — erőforrás-módosító kapuk
    AnswerLaneSelection = 1,          // 5.2 — eredményválasztó sávok
    MissingOperand = 2,               // 7 + ? = 3 × 8
    EquationSolving = 3,              // (? + 17) × 3 = 6 × (14 − 3)
    TargetValueSelection = 4          // "Érd el a célértéket!"
}

/// A kapu/sáv-feladat CÉLJA — kizárólag az optimalizálás IRÁNYA, erőforrás-független.
/// A "melyik erőforrásról van szó" kérdést a MathProblem.Reward.Resource /
/// MathEventScheduleEntry.RewardResource adja meg. A kettő ORTOGONÁLIS:
/// "Több lőszert!" = (Maximize, Ammo); "Kevesebb páncél!" = (Minimize, EnemyArmor);
/// "Több pajzsot!" = (Maximize, Shield); "Érd el a 60-at!" = (ReachTarget, bármi).
/// A GAME_DESIGN.md 3.4 táblázatának `MoreAmmo` / `LessEnemyArmor` / `MoreShield`
/// nevei PREZENTÁCIÓS címkék (ikon + string-tábla kulcs), NEM enum-értékek.
public enum GateGoal { Maximize = 0, Minimize = 1, ReachTarget = 2 }

public readonly struct MathProblemId : IEquatable<MathProblemId>
{ public Guid Value { get; } public static MathProblemId New(IRandomSource rng); }

/// A kiértékelés eredménye. Maradékos osztásnál Remainder != null.
public readonly struct EvaluationResult
{
    public int Value { get; }        // fő érték (hányados maradékos osztásnál)
    public int? Remainder { get; }   // csak IntegerDivisionWithRemainder esetén
    public string ToDisplayString(); // "7" vagy "7 maradék 1"
}

/// Egy felkínált válaszopció (sávon vagy kapun megjelenő egység).
public sealed class AnswerOption
{
    public int OptionIndex { get; }          // 0..N-1, a generátor adja
    public LaneIndex Lane { get; }           // melyik sávon érkezik
    public EvaluationResult Value { get; }   // AnswerLaneSelection esetén a szám
    public ExpressionNode GateOperation { get; } // ResourceOperationComparison esetén
                                                 //   pl. BinaryOp(×, Unknown, 2) → "×2"
    public bool IsCorrect { get; }
    public ErrorCategory? DistractorErrorModel { get; } // null, ha IsCorrect
    public string DisplayLabel { get; }      // "56" vagy "×2" vagy "+25"
}

public enum ErrorCategory
{
    None = 0,
    NeighbourInTimesTable = 1,   // szorzótáblában szomszédos
    OnesDigitError = 2,          // egyesek elrontása
    TensDigitError = 3,          // tízesek elrontása
    CarryError = 4,              // átvitel elrontása
    OperatorPrecedenceError = 5, // rossz műveleti sorrend
    IgnoredParentheses = 6,      // zárójel figyelmen kívül hagyása
    RemainderMisread = 7,        // osztási maradék félreértelmezése
    SignError = 8,               // előjelhiba
    ReverseCalculationError = 9, // visszafelé számolási hiba
    Timeout = 10,                // nem adott választ (csak attempt-naplóban)
    Unclassified = 11
}

/// A készség-taxonómia. Új érték CSAK a lista végére (0. szakasz enum-szabálya).
/// FIGYELEM: a `ProblemType.EquationSolving` egy FELADATTÍPUS, nem SkillTag —
/// az egyenletes feladatok készségei: `MissingOperand` + `MultiStepExpressions`
/// (+ `Parentheses`, ha van zárójel). A design-dokumentumok ne hivatkozzanak
/// `SkillTag.EquationSolving`-ra vagy `SkillTag.MixedOperations`-ra; utóbbi helyes
/// neve `MultiStepExpressions`.
public enum SkillTag
{
    Addition = 0, Subtraction = 1, Multiplication = 2, Division = 3, Remainder = 4,
    OperatorPrecedence = 5, Parentheses = 6, MissingOperand = 7,
    MultiStepExpressions = 8, ResourceComparison = 9,
    // finomszemcsés készségek (spec 8.)
    SingleDigitAddition = 10, TwoDigitAddition = 11, CrossingTen = 12,
    SubtractionWithBorrowing = 13,
    TimesTable2 = 14, TimesTable3 = 15, TimesTable4 = 16, TimesTable5 = 17,
    TimesTable6 = 18, TimesTable7 = 19, TimesTable8 = 20, TimesTable9 = 21,
    TimesTable10 = 22,
    TwoDigitMultiplication = 23, DivisionWithRemainder = 24
}

/// Mit ad a helyes válasz. RewardDelta = OperationResult − SnapshotValue (5.1).
public sealed class RewardSpec
{
    public ResourceType Resource { get; }
    /// AnswerLaneSelection esetén fix érték (pl. +30 lőszer).
    /// ResourceOperationComparison esetén 0 — ott a delta számított.
    public int FlatAmount { get; }
    public bool UseSnapshotDelta { get; }   // true ⇒ ResourceOperationComparison
    public int IncorrectAmount { get; }     // alap: 0 (spec 9.: nincs extra büntetés)
}

public sealed class MathProblem
{
    public MathProblemId Id { get; }
    public ProblemType Type { get; }
    public ExpressionNode Expression { get; }      // a megjelenítendő fa
    public string DisplayExpression { get; }       // "7 × 8 = ?" — előszámolt
    public EvaluationResult CorrectAnswer { get; }
    public IReadOnlyList<AnswerOption> Options { get; }  // 2..4 elem; üres TargetValue-nál nem
    public int DifficultyLevel { get; }            // 1..10
    public IReadOnlyList<SkillTag> SkillTags { get; }    // legalább 1 elem
    public float ThinkTimeSeconds { get; }         // > 0
    public RewardSpec Reward { get; }
    public int Seed { get; }                       // reprodukálhatóság (6.3)
    public GateGoal Goal { get; }                  // kapu célja, ikonhoz
    public int? TargetValue { get; }               // TargetValueSelection vagy ReachTarget erőforrás-kapu
    public int? ResourceCapacity { get; }          // ResourceOperationComparison: rögzített ResourcePool.Max (> 0); máskor null
    public string Explanation { get; }             // opcionális, lehet null (6.5)
}
```

**A kapacitás bemeneti szerződése (ADR-013):** a `ResourceCapacity` a
`Reward.Resource` pooljának a feladat létrehozásakor rögzített `Max` értéke,
nem nehézségtábla-adat és nem élő erőforrás-lekérdezés. `ResourceOperationComparison`
esetén kötelező és pozitív; minden más feladattípusnál `null`. A nullable típus
az eltérő feladattípusokat és a validálandó jelölteket szolgálja, nem jelent
„korlátlan erőforrást”. A hiányzó/érvénytelen kapacitás üzleti validációs hiba
(`GATE_CAPACITY_INVALID`), nem konstruktor-kivétel.

A meglévő `MathProblem` konstruktor végére opcionális `int? resourceCapacity = null`
paraméter kerül: a jelenlegi forrásszintű hívások továbbra is fordulnak. A régi,
kapacitás nélküli erőforrás-kapujelölt azonban nem válik automatikusan érvényessé.
A `TargetValue` eddigi konstruktor-őrét ki kell bővíteni: megadható
`TargetValueSelection`-nél, illetve `ResourceOperationComparison` + `ReachTarget`
esetén; más kombinációban továbbra is programozói hiba. `ReachTarget`
erőforrás-kapunál a hiányzó célértéket a validátor jelzi (`GATE_TARGET_MISSING`).

**MathProblem-invariánsok (a `ProblemValidator` kényszeríti, spec 6.4):**

| # | Invariáns |
|---|---|
| I1 | `CorrectAnswer` megegyezik az `ExpressionEvaluator` eredményével |
| I2 | `Options` között **pontosan egy** `IsCorrect == true` |
| I3 | `Options` `Value`-i páronként különbözők (maradékkal együtt) |
| I4 | `2 <= Options.Count <= 4`, és minden `Lane` egyedi, `0..Count-1` |
| I5 | `SkillTags` nem üres, és tükrözi a fában ténylegesen előforduló műveleteket |
| I6 | nincs nullával osztás, nincs nem egész osztáseredmény (kivéve `IntegerDivisionWithRemainder`) |
| I7 | minden részkifejezés és a végeredmény `int` tartományban marad (`checked` ellenőrzés) |
| I8 | `MissingOperand`/`EquationSolving` esetén az ismeretlen **egyértelmű** (pontosan egy egész megoldás) |
| I9 | a helyes válasz sávpozíciója az `IRandomSource`-ból sorsolt, hosszú távon egyenletes |
| I10 | `ResourceOperationComparison` esetén pozitív `ResourceCapacity` kötelező. Minden kapupár nyers és effektív (`min(Value.Value, ResourceCapacity)`) eredménye különböző, az effektív különbség legalább `MinGateGap`. A `Goal` szerinti nyers és effektív optimum egyaránt egyértelmű, és ugyanaz az opció; `ReachTarget` esetén a megadott `TargetValue`-tól vett távolság dönt (math-engine.md 5., 7.1.) |
| I11 | negatív végeredmény csak akkor engedett, ha a `DifficultyProfile.AllowNegativeResults == true` |

```csharp
/// A játékos által leadott válasz (egy MathEvent-hez).
public sealed class MathAnswer
{
    public MathProblemId ProblemId { get; }
    public int? SelectedOptionIndex { get; }  // null ⇒ timeout
    public LaneIndex? SelectedLane { get; }
    public bool WasCorrect { get; }
    public float ResponseTimeSeconds { get; } // AwaitingDecision belépésétől a választásig
    public bool TimedOut { get; }
    public ErrorCategory ErrorCategory { get; }  // None, ha helyes; Timeout, ha lejárt
    public double AnsweredAtGameTime { get; }
}
```

---

### 3.1. Előkészített resource gate és végleges binding (ADR-015, ELFOGADVA)

Namespace: `MathHorde.Core.Problems`. Ezek **Unity- és MathEngine-független**
Core típusok. A `MathProblem`, `AnswerOption`, `MathAnswer` meglévő aláírása és
immutabilitása nem változik. A valós idejű `ResourceOperationComparison` a
következő reprezentációt használja; a régi teljes MathProblem továbbra is
érvényes offline generálási/validációs tesztadat lehet.

```csharp
public readonly struct ResourceGateOptionTemplate
{
    public int OptionIndex { get; }
    public LaneIndex Lane { get; }
    public ExpressionNode GateOperation { get; } // pontosan egy snapshot-UnknownNode
    public string DisplayLabel { get; }          // előformázott: "+25", "×2"
}

public sealed class ResourceGateTemplate
{
    public IReadOnlyList<ResourceGateOptionTemplate> Options { get; } // 2..4
    public IReadOnlyList<SkillTag> SkillTags { get; }
}

public sealed class PreparedResourceGate
{
    public MathProblemId Id { get; }
    public int Seed { get; }
    public DifficultyProfile Difficulty { get; }
    public float ThinkTimeSeconds { get; }       // skálázatlan alapidő
    public RewardSpec Reward { get; }
    public GateGoal Goal { get; }
    public int? TargetValue { get; }
    public int PreparedCapacity { get; }         // támogatott kontextus, nem élő Max
    public IReadOnlyList<ResourceGateTemplate> Candidates { get; } // determinisztikus sorrend
    public IReadOnlyList<SkillTag> GetSkillTags(int candidateIndex, int snapshotValue);
}

public readonly struct ResourceGateBinding
{
    public PreparedResourceGate Prepared { get; }
    public int CandidateIndex { get; }
    public ResourceGateTemplate Template { get; } // Prepared.Candidates[CandidateIndex]
    public MathProblemId ProblemId { get; }       // Prepared.Id
    public int SnapshotValue { get; }
    public int ResourceCapacity { get; }          // spawnkor ténylegesen rögzített Max
    public int OptionCount { get; }
    public int CorrectOptionIndex { get; }
    public IReadOnlyList<SkillTag> SkillTags { get; } // előkészített, kontextushelyes lista
    public EvaluationResult GetValue(int optionIndex);
}
```

A két sealed osztály és a sablon-opció immutable; bemeneti listáikat másolt,
visszakasztolással sem módosítható olvasó nézetben tárolják. A binding nem tartalmaz
új listát vagy dobozolt interfész-adatot: négy readonly EvaluationResult mezőből
olvas az indexelt getter; a nem használt mezők defaultok. Érvénytelen index
programozói hiba. A default binding (`Prepared == null`) nem érvényes feladat.
Az eseményazonosító GameLogic-adat, ezért a Core binding nem függ MathEventId-től:
az `ActiveMathEvent` tartja össze az EventId-t és a binding ProblemId-jét.
A GetSkillTags a Prepare során a konkrét snapshothoz és jelölthöz előre
osztályozott immutable listát adja vissza, allokáció nélkül. A Template.SkillTags
csak a szimbolikus műveletek tagje; a binding.SkillTags a régi validátorral
egyezően a helyes konkrét kifejezés és minden szimbolikus kapuművelet uniója.
Így az átlépés/többjegyűség tagje nem vész el, és Bindben nem fut Classify.

Minden opció nyers értéke `EvaluateWith(GateOperation, SnapshotValue)`. A binding
I2–I7, I9–I11 egyenértékű ellenőrzésén átmegy; I1-ben a kijelölt helyes opció
behelyettesített művelete adja a helyes választ, I8 megoldandó ismeretlenje itt
nem alkalmazandó. A sablonban még nincs Value/IsCorrect/ResourceCapacity:
ezekről előkészítéskor nem szabad végleges snapshotadatot állítani. Az operandus-,
eredmény-, készség-, kapacitás- és réskorlátok nem lazulnak. A binding egyszer
keletkezik és utána változatlan; a későbbi harc sem értékeit, sem Maxát nem írja át.

---

### 3.2. Allokációmentes válaszadat a feloldási sorban

```csharp
// Namespace: MathHorde.Core.Problems
public readonly struct MathAnswerSnapshot
{
    public MathProblemId ProblemId { get; }
    public int? SelectedOptionIndex { get; }
    public LaneIndex? SelectedLane { get; }
    public bool WasCorrect { get; }
    public float ResponseTimeSeconds { get; }
    public bool TimedOut { get; }
    public ErrorCategory ErrorCategory { get; }
    public double AnsweredAtGameTime { get; }
}
```

A mezőinvariánsok a meglévő MathAnsweréval azonosak; ez annak readonly struct
adatátviteli párja, nem új kiértékelés. A GateResolution ezt viszi tovább Tickben.
A meglévő sealed MathAnswer/QuestionAttempt csak a Ticken kívüli analitikai
drainben materializálható; a feloldási út nem allokálhatja ezeket. Ez nem változtat
a napló tartalmán, pontosságán vagy idempotenciáján.

---

## 4. Skills és Difficulty

```csharp
namespace MathHorde.Core.Skills;

/// A készség-elsajátítás diszkrét fokozatai (ADR-010).
/// A generátor és a világ-feloldás KIZÁRÓLAG ezen keresztül kérdezi a tudást —
/// nyers `MasteryEstimate` float összehasonlítás a hívó oldalon TILOS.
/// Küszöbök: SkillPrerequisiteSO (MasteryThreshold, MinAttemptsForMastery).
public enum SkillMasteryLevel
{
    /// Soha nem találkozott vele, és az előfeltételei sem teljesülnek.
    /// A generátor NEM adhat ilyen készségű feladatot.
    Locked = 0,
    /// Előfeltételei teljesülnek, a generátor adhat ilyen feladatot.
    /// (Attempts == 0 is ide tartozik, ha az előfeltételek rendben.)
    Introduced = 1,
    /// Attempts >= MinAttemptsForMastery/2 (alap 4) és MasteryEstimate >= 0.5.
    Practiced = 2,
    /// MasteryEstimate >= MasteryThreshold (alap 0.8) és Confidence >= 0.6.
    Mastered = 3
}

/// Egyetlen készség mért statisztikája (spec 8. + 15. SkillStatistics).
public sealed class SkillStatistics
{
    public SkillTag SkillId { get; }
    public int DifficultyLevel { get; }          // 1..10, melyik szinten mérve
    public int Attempts { get; }                 // >= 0
    public int CorrectAnswers { get; }           // 0 <= CorrectAnswers <= Attempts
    public float Accuracy { get; }               // származtatott: Correct/Attempts, 0 ha Attempts==0
    public float AverageResponseTimeSeconds { get; }
    public float MedianResponseTimeSeconds { get; }
    public DateTimeOffset? LastPracticedAt { get; }
    public float MasteryEstimate { get; }        // 0.0..1.0
    /// Megbízhatóság: 0.0, ha Attempts < MinSamplesForConfidence (alap 8);
    /// 1.0-hoz tart ~30 minta felett. A Parent Dashboard ezt MUTATJA (spec 11.3).
    public float Confidence { get; }
    public IReadOnlyDictionary<ErrorCategory, int> TypicalErrors { get; }
}

public sealed class SkillProfile
{
    public IReadOnlyDictionary<SkillTag, SkillStatistics> Skills { get; }
    public DateTimeOffset UpdatedAt { get; }
    public SkillStatistics GetOrEmpty(SkillTag tag);
    /// A diszkrét fokozat. Ez az EGYETLEN megengedett tudás-lekérdezés a
    /// MathEngine, a GameLogic és a világ-feloldás számára (ADR-010).
    SkillMasteryLevel GetMasteryLevel(SkillTag tag);
    /// Elsajátítottnak tekintett készségek (GetMasteryLevel == Mastered).
    /// SOHA nem elég egyetlen helyes válasz (spec 8.).
    public IReadOnlyCollection<SkillTag> MasteredSkills { get; }
    /// Gyakorlásra szoruló készségek, fontossági sorrendben.
    public IReadOnlyList<SkillTag> SkillsNeedingPractice { get; }
}

/// A nehézség három dimenziója (spec 7.3) + a globális szint.
public sealed class DifficultyProfile
{
    public int ChallengeLevel { get; }       // 1..10 — a HUD-on látható globális szint
    public int MathComplexity { get; }       // 1..10
    public int TimePressure { get; }         // 1..10
    public int CombatDifficulty { get; }     // 1..10
    /// Csak ezek az operátorok generálhatók (szülői beállítás + készségelőfeltétel metszete).
    public IReadOnlyCollection<BinaryOperator> AllowedOperators { get; }
    public IReadOnlyCollection<SkillTag> UnlockedSkills { get; }
    public int MinOperandValue { get; }      // alap 0
    public int MaxOperandValue { get; }      // szintfüggő, 10..999
    public int MaxResultValue { get; }       // > 0; önálló végeredmény-felsőkorlát, nem MaxOperandValue-ból származtatott (ADR-012)
    public int MinOperandCount { get; }      // >= 2
    public int MaxOperandCount { get; }      // <= 5 (MVP)
    public bool AllowParentheses { get; }
    /// 0 ⇒ nincs zárójel. 1 ⇒ egy szint (CL7–9), 2 ⇒ beágyazott (CL10).
    /// Invariáns: `AllowParentheses == (MaxParenthesisDepth > 0)`.
    public int MaxParenthesisDepth { get; }
    public bool AllowUnknownOperand { get; }
    /// Hány `UnknownNode` kerülhet a fába (0 vagy 1 az MVP-ben).
    /// Invariáns: `AllowUnknownOperand == (UnknownOperandCount > 0)`.
    public int UnknownOperandCount { get; }
    public bool AllowNegativeResults { get; }
    public int AnswerOptionCount { get; }    // 2..4
    public float BaseThinkTimeSeconds { get; }
    /// A `ResourceOperationComparison` két kapueredménye közti minimális eltérés (I10).
    public int MinGateGap { get; }
    public bool AutoAdaptationEnabled { get; }
}
```

**Invariánsok:** `1 <= ChallengeLevel <= 10` és ugyanez a három dimenzióra.
`UnlockedSkills` bővítése **kizárólag** a `SkillProfile` alapján történhet — a globális szint
emelése önmagában soha nem old fel nem tanult műveletet (spec 7.2, 7.4).
`UnlockedSkills` a `WorldDefinition.IntroducedSkills` és a `SkillProfile`-ban legalább
`Introduced` fokozatú készségek **metszete** (worlds.md 1.).

---

## 5. Resources

```csharp
namespace MathHorde.Core.Resources;

/// Generikus erőforrásmodell (spec 4.3) — új típus hozzáadása nem igényel kódmódosítást
/// a ResourceService-ben.
public enum ResourceType
{
    Ammo = 0,
    Health = 1,
    Shield = 2,
    /// Session-szintű, NEM per-ellenség pool: a pálya összes `Armored` ellenségének
    /// páncél-bónuszát skálázó, csökkentendő erőforrás. A `(Minimize, EnemyArmor)`
    /// kapuk ezt viszik lejjebb; `Current == Min` ⇒ a páncél hatása 0.
    /// A per-ellenség páncélértéket az `EnemyDefinition.ArmorPoints` adja (F5).
    EnemyArmor = 3,
    Energy = 4
}

public sealed class ResourcePool
{
    public ResourceType Type { get; }
    public int Current { get; }       // 0 <= Current <= Max
    public int Max { get; }           // > 0
    public int Min { get; }           // alap 0
    public bool IsDepleted { get; }   // Current == Min
}

/// Egy session teljes erőforrás-állapota. Immutable snapshot-képes.
public sealed class ResourceState
{
    public IReadOnlyDictionary<ResourceType, ResourcePool> Pools { get; }
    public int GetCurrent(ResourceType type);     // ismeretlen típus → 0
    public int GetMax(ResourceType type);
    public ResourceState WithPool(ResourcePool pool);   // immutable módosítás
}

/// Egy alkalmazott változás leírása — az eseménynaplóba és a HUD-animációhoz.
public readonly struct ResourceChange
{
    public ResourceType Type { get; }
    public int PreviousValue { get; }
    public int NewValue { get; }
    public int RequestedDelta { get; }
    public int AppliedDelta { get; }   // Max/Min vágás miatt eltérhet RequestedDelta-tól
    public bool WasClamped { get; }
    public string ReasonCode { get; }  // "WEAPON_FIRE", "MATH_GATE", "ENEMY_CONTACT", ...
}
```

**Invariáns:** `Min <= Current <= Max` **mindig** fennáll; a vágás soha nem dob kivételt,
hanem `WasClamped = true`-t jelez (spec 17.3 „amennyiben nincs kapacitáskorlátba ütközés”).

**Kapacitás-szemantika (a GAME_DESIGN.md „soft cap” fogalmának kötelező értelmezése):**
a `ResourcePool.Max` **kemény** felső korlát; a `Current` soha nem lépheti túl. A design-dokumentum
„200-as soft cap, a felesleg pontszámmá válik” megfogalmazása azt jelenti, hogy
`MaxAmmo == 200`, a vágott (`WasClamped == true`) `AppliedDelta − RequestedDelta` különbséget
pedig a Fázis 5-ös pontszámítás írja jóvá (`ScoringService`, `AmmoBudgetSO.OverflowScorePerAmmo`).
A Fázis 1–4-ben a vágott felesleg egyszerűen elveszik. **Nincs olyan állapot, amelyben a
`Current > Max`.**

---

## 6. Progress és Session

```csharp
namespace MathHorde.Core.Progress;

public readonly struct PlayerProfileId { public Guid Value { get; } }
public readonly struct GameSessionId  { public Guid Value { get; } }
public readonly struct LevelId        { public string Value { get; } }  // "w1-l03"
public readonly struct WorldId        { public string Value { get; } }  // "w1"

public sealed class PlayerProfile       // spec 15.
{
    public PlayerProfileId Id { get; }
    public string DisplayName { get; }               // lehet null ⇒ névtelen helyi profil
    public DateTimeOffset CreatedAt { get; }
    public int CurrentChallengeLevel { get; }        // 1..10
    public int HighestChallengeLevel { get; }
    public IReadOnlyCollection<WorldId> UnlockedWorlds { get; }
    public IReadOnlyCollection<LevelId> CompletedLevels { get; }
}

/// Pályánkénti teljesítés- és próbálkozás-napló.
/// A balance-dokumentumok „LevelAttemptRecord” néven hivatkoztak rá — a kontrakt
/// szerinti név EZ (`LevelCompletionRecord`), más típus nincs.
public sealed class LevelCompletionRecord
{
    public LevelId LevelId { get; }
    public int Stars { get; }            // 0..3
    public int Attempts { get; }         // a 7.4 szabályokhoz pályánként követve
    public int ConsecutiveFailures { get; }
    public DateTimeOffset? FirstCompletedAt { get; }
    public float BestMathAccuracy { get; }
}

public sealed class PlayerProgress
{
    public PlayerProfile Profile { get; }
    public SkillProfile Skills { get; }
    public DifficultyProfile Difficulty { get; }
    public IReadOnlyDictionary<LevelId, LevelCompletionRecord> Levels { get; }
    public TimeSpan TotalPlayTime { get; }
    public IReadOnlyCollection<string> UnlockedRewardIds { get; }  // fegyver, skin, képesség
}

public enum SessionResult { InProgress = 0, Victory = 1, Defeat = 2, Abandoned = 3 }

/// MIÉRT ért véget vereséggel — a játékszabály szintjén. ZÁRT lista:
/// a lőszerhiány és az ellenség-áttörés NEM önálló vereség-ok (spec 1.3), hanem
/// HealthDepleted-hez vezető ÚT. A diagnosztikát a LossDiagnosis adja.
public enum FailureReason
{
    None = 0, HealthDepleted = 1, TimeLimit = 2, PlayerQuit = 3, MathMinimumNotMet = 4
}

/// Fázis 4 — az adaptációs motor bemenete. NEM játékszabály, hanem MÉRT diagnózis:
/// miért fogyott el az életerő. A difficulty-model.md 6.2 dimenzió-választó mátrixa
/// erre hivatkozik (korábbi, hibás nevei: `EnemyBreakthrough`, `AmmoStarvation`).
public enum LossDiagnosis
{
    None = 0,
    /// A vereség előtti 30 s-ban a lőszer > 20%-ban 0 volt (WeaponDryFire arány magas).
    AmmoStarvation = 1,
    /// Volt lőszer, de az ellenségek átjutottak (kontakt-sebzés dominált).
    EnemyBreakthrough = 2,
    /// A matematikai pontosság nem érte el a RequiredMathAccuracy-t.
    MathAccuracyShortfall = 3,
    /// A boss-fázisok gyengepont-feladatai maradtak megoldatlanul.
    BossPhaseStall = 4
}

/// Egy lejátszott pálya naplója (spec 15. GameSession).
public sealed class GameSessionRecord
{
    public GameSessionId SessionId { get; }
    public LevelId LevelId { get; }
    public int Seed { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? FinishedAt { get; }
    public int InitialChallengeLevel { get; }
    public int FinalChallengeLevel { get; }
    public SessionResult Result { get; }
    public FailureReason FailureReason { get; }
    public LossDiagnosis LossDiagnosis { get; }   // Fázis 4; F1–F3-ban None
    public float MathAccuracy { get; }      // 0..1
    public int TotalQuestions { get; }
    public int CorrectQuestions { get; }
    public int TimedOutQuestions { get; }
    public IReadOnlyList<QuestionAttempt> Attempts { get; }
    /// Idempotencia-napló: azok az esemény-azonosítók, amelyek jutalma már jóváíródott.
    /// MENTÉSRE KERÜL, hogy visszatöltés után se duplázódjon (ARCHITECTURE 6.2).
    public IReadOnlyCollection<Guid> AppliedRewardEventIds { get; }
}

public sealed class QuestionAttempt      // spec 15.
{
    public MathProblemId QuestionId { get; }
    public GameSessionId SessionId { get; }
    public IReadOnlyList<SkillTag> SkillTags { get; }
    public int Difficulty { get; }
    public string CorrectAnswer { get; }     // display-alak, pl. "7 maradék 1"
    public string SelectedAnswer { get; }    // null, ha timeout
    public bool WasCorrect { get; }
    public float ResponseTimeSeconds { get; }
    public bool Timeout { get; }
    public ErrorCategory ErrorCategory { get; }
    public DateTimeOffset AnsweredAt { get; }
}

public sealed class DifficultyAdjustment  // spec 15.
{
    public int PreviousLevel { get; }
    public int NewLevel { get; }
    public string Reason { get; }   // "LEVEL_PASSED_FIRST_TRY", "TWO_CONSECUTIVE_FAILURES", ...
    public DateTimeOffset Timestamp { get; }
    public IReadOnlyDictionary<string, double> TriggerMetrics { get; }
}
```

---

## 7. Levels (spec 10., 16.)

```csharp
namespace MathHorde.Core.Levels;

/// Viselkedési archetípusok. ZÁRT enum, explicit értékekkel — minden új archetípus
/// a lista VÉGÉRE kerül, új számmal (0. szakasz enum-szabálya, ADR-008).
/// A spec 4.1 „legalább négy típust” ír elő; az ötödik (`Swarmlet`) a horde-élmény
/// miatt kötelező és F1-től létezik (ADR-008, GAME_DESIGN.md 5.).
public enum EnemyType
{
    Basic = 0,
    Fast = 1,
    Armored = 2,
    Boss = 3,
    /// Apró, 1 HP-s, 4–6 fős kötelékben spawnoló „ágyútöltelék”.
    /// A horde-survival műfaj „tömeg lekaszálása” élményének hordozója; 3 HP-s
    /// ellenségekkel ez nem elérhető (GAME_DESIGN.md 5., difficulty-model.md 4.
    /// „Rajzó arány” oszlop CL2-től).
    Swarmlet = 4
}
```

**`EnemyType` bővítési útiterv (NEM része a Fázis 1–4 enumnak).**
A `balance/worlds.md` további archetípusokat vezet be a W4–W8 világokban. Ezek **csak az
F5 „Világok és progresszió” fázisban** kerülnek az enumba, az itt lefoglalt számokkal — addig
a design-dokumentum sem tekintheti őket létező értéknek:

| Jövőbeli érték | Lefoglalt szám | Világ | Viselkedés | Fázis |
|---|---|---|---|---|
| `Splitter` | 5 | W4 | halálakor 2 `Swarmlet`-re esik szét | F5 |
| `Shifter` | 6 | W5 | 2 s-onként szomszédos sávba lép | F5 |
| `Mirror` | 7 | W6 | csak szomszédos sávból sebezhető | F5 |
| `Prime` | 8 | W7 | gyengepont csak prím válasszal nyílik | F5 |
| `Cipher` | 9 | W8 | HP binárisan jelenik meg | F6+ |

Új archetípus bevezetésének kötelező lépései: (1) enum-érték a lista végére a fenti számmal,
(2) `IEnemyBehaviour` stratégia a GameLogic rétegben, (3) `EnemyDefinitionSO` asset,
(4) ADR-008 revíziója. **Enum-érték nélküli, `string EnemyId`-ra épülő „rejtett típus” TILOS.**

```csharp
/// Unity-mentes ellenség-leíró; a ScriptableObject-ből fordítódik (spec 4.1).
///
/// SZÁNDÉKOS SZÉTVÁLASZTÁS (ADR-009): ez a típus KIZÁRÓLAG statikus konfigurációt
/// tartalmaz, egyetlen példánya osztódik meg a pálya összes azonos ellenségén.
/// A futásidejű állapot (`CurrentHealth`, `Position`, `Lane`, `IsAlive`) a GameLogic
/// `EnemySnapshot` struct-jában él (game-logic.md 3.). A kettőt összevonni TILOS:
/// a definíció immutable és poolozható, a snapshot per-instance és Tickenként változik.
/// A spec 4.1 „aktuális életerő” követelményét az `EnemySnapshot.CurrentHealth` fedi.
public sealed class EnemyDefinition
{
    public string EnemyId { get; }         // "basic_bot"
    public EnemyType Type { get; }
    public int MaxHealth { get; }          // > 0
    public float MovementSpeed { get; }    // világegység/mp, > 0
    public int ContactDamage { get; }      // >= 0 — a játékos vonalát elérve okozott sebzés
    public float AttackCooldownSeconds { get; } // >= 0
    /// A kill jutalma: ennyi **lőszer** íródik vissza (`ApplyCombatReward(Ammo, …)`),
    /// NEM pontszám. A balance-modell „killRefund” tétele (difficulty-model.md 5.);
    /// alapérték 1. A pontszám ettől független, az F5 `ScoringService` számolja.
    public int RewardValue { get; }        // >= 0
    public string PrefabKey { get; }       // Presentation oldja fel

    // --- Fázis 5 (F5) bővítés — F1–F4-ben nem szerepel a konstruktorban ---
    /// `Armored` páncélpontok (GAME_DESIGN.md 5.: 6). 0 ⇒ nincs páncél.
    /// A tényleges hatást a session-szintű `ResourceType.EnemyArmor` pool skálázza.
    public int ArmorPoints { get; }            // >= 0, alap 0           [F5]
    /// Elölről érkező sebzés csökkentése (GAME_DESIGN.md 5.: 0.6 = 60%).
    public float FrontalDamageReduction { get; }  // 0.0..0.9, alap 0    [F5]
    /// `Splitter`: halálakor létrejövő ellenség azonosítója; null ⇒ nem osztódik.
    public string SplitOnDeathEnemyId { get; }    // alap null           [F5]
    public int SplitOnDeathCount { get; }         // 0..6, alap 0        [F5]
}

public sealed class WeaponDefinition      // spec 4.2
{
    public string WeaponId { get; }
    public int Damage { get; }             // > 0
    public float FireRate { get; }         // lövés/mp, > 0
    public float Range { get; }            // > 0
    public int AmmoPerShot { get; }        // >= 1
    public float ProjectileSpeed { get; }  // > 0; 0 ⇒ hitscan
    public TargetingMode TargetingMode { get; }
    public string VisualEffectKey { get; }
}

public enum TargetingMode
{
    OwnLaneNearestOnly = 0, OwnLaneThenAdjacent = 1, GlobalNearest = 2
}

public sealed class WaveDefinition
{
    public int WaveIndex { get; }
    public double StartGameTime { get; }        // mp a session kezdetétől
    public double DurationSeconds { get; }
    public IReadOnlyList<WaveSpawnEntry> Spawns { get; }
}

public sealed class WaveSpawnEntry
{
    public string EnemyId { get; }
    public int Count { get; }                   // >= 1
    public float SpawnIntervalSeconds { get; }  // > 0
    public IReadOnlyList<LaneIndex> AllowedLanes { get; }  // üres ⇒ bármelyik
}

public sealed class MathEventScheduleEntry
{
    public double TriggerGameTime { get; }      // mikor lép Preview-ba
    public ProblemType ProblemType { get; }
    public float PreviewDurationSeconds { get; }
    public float ThinkTimeSecondsOverride { get; }  // <= 0 ⇒ a DifficultyProfile-ból
    public ResourceType RewardResource { get; }
    public GateGoal Goal { get; }
}

public sealed class LevelDefinition
{
    public LevelId Id { get; }
    public WorldId WorldId { get; }
    public string DisplayNameKey { get; }        // string-táblabeli kulcs, NEM magyar szöveg
    public int LaneCount { get; }                // 2..4
    public double TargetDurationSeconds { get; } // 180..300 (spec 10.1)
    public IReadOnlyList<WaveDefinition> Waves { get; }
    public IReadOnlyList<MathEventScheduleEntry> MathEvents { get; } // >= 10 (spec 10.1)
    public string BossEnemyId { get; }           // null ⇒ nincs boss
    public int StartingAmmo { get; }
    public int MaxAmmo { get; }
    public int StartingHealth { get; }
    public int MaxHealth { get; }
    public int StartingShield { get; }
    /// A pajzs-pool kapacitása. A spec 4.3 minden erőforrásra `aktuális + maximális`
    /// párt ír elő, ezért a pajzs is kap sajátot — NEM vezethető le a
    /// `StartingShield`-ből (a pajzs tipikusan 0-ról indul és kapukkal töltődik,
    /// tehát `StartingShield == 0`, `MaxShield == 50`).
    /// Alapérték a `LevelDefinitionSO`-ban: 50 (GAME_DESIGN.md 6.1).
    public int MaxShield { get; }
    public float RequiredMathAccuracy { get; }   // 0 ⇒ nincs matek-minimum
    public string WeaponId { get; }
}
```

**`LevelDefinition` validációs invariánsok (a konstruktor kényszeríti):**

| # | Invariáns | Hiba |
|---|---|---|
| L1 | `2 <= LaneCount <= 4` | `ArgumentOutOfRangeException` |
| L2 | `TargetDurationSeconds > 0` | `ArgumentOutOfRangeException` |
| L3 | `MaxAmmo > 0`, `MaxHealth > 0` | `ArgumentOutOfRangeException` |
| L4 | **`MaxShield >= 0`** — 0 megengedett (pajzs-mechanika nélküli pálya) | `ArgumentOutOfRangeException` |
| L5 | `0 <= StartingAmmo <= MaxAmmo`, `0 <= StartingHealth <= MaxHealth`, **`0 <= StartingShield <= MaxShield`** | `ArgumentOutOfRangeException` |
| L6 | `0 <= RequiredMathAccuracy <= 1` | `ArgumentOutOfRangeException` |

**Konstruktor-paraméter sorrend (kötelező):** `… startingHealth, maxHealth, startingShield,
maxShield, requiredMathAccuracy, weaponId` — azaz a `maxShield` közvetlenül a
`startingShield` **után**, a többi `Starting*/Max*` pár mintájára.

> **Megjegyzés a `ResourcePool.Max > 0` invariánshoz:** `MaxShield == 0` esetén a
> `ResourceService.Initialize` **nem hoz létre** `Shield` poolt (a `ResourceState.GetCurrent`
> ismeretlen típusra 0-t ad, ami pontosan a kívánt viselkedés). Kerülő megoldás
> (`Max = max(StartingShield, 1)`) TILOS.

```csharp
/// Egy világ (tartalmi kapu). A világ NEM nehézségi szint (worlds.md 1.).
public sealed class WorldDefinition
{
    public WorldId Id { get; }
    public string DisplayNameKey { get; }
    public int OrderIndex { get; }
    public IReadOnlyList<LevelId> Levels { get; }
    /// Mely készségek jelenhetnek meg EGYÁLTALÁN ebben a világban. A generátor
    /// megengedett halmaza ennek és a `DifficultyProfile.UnlockedSkills`-nek a
    /// METSZETE; üres metszet esetén a világ szűkebb halmaza nyer (worlds.md 1.).
    public IReadOnlyCollection<SkillTag> IntroducedSkills { get; }
    /// Mely kapucélok használhatók. W1: csak `Maximize`; `Minimize` W3-tól,
    /// `ReachTarget` W2-től (a W2 boss `TargetValueSelection` feladatot ad).
    public IReadOnlyCollection<GateGoal> EnabledGateGoals { get; }
    /// Mely ellenség-archetípusok spawnolhatnak itt (vizuális/mechanikai tartalmi kapu).
    public IReadOnlyCollection<EnemyType> IntroducedEnemyTypes { get; }
    /// A világ feloldási feltétele. null ⇒ kezdettől nyitva (W1).
    public WorldUnlockRequirement UnlockRequirement { get; }
}

/// Összetett feloldási feltétel (worlds.md 3.). MINDEN kitöltött tagnak teljesülnie kell (ÉS).
/// A korábbi `UnlockRequirementLevelId` (egyetlen pálya) nem volt elegendő: a design
/// csillag-arányt ÉS készség-előfeltételt is megkövetel (pl. W4 ⇐ `Multiplication: Practiced`).
public sealed class WorldUnlockRequirement
{
    public WorldId RequiredWorldId { get; }              // default(WorldId) ⇒ nincs
    /// A `RequiredWorldId` pályái közül legalább ekkora arány legyen >= 1 csillagos.
    public float RequiredStarRatio { get; }              // 0.0..1.0, alap 0.6
    /// Készség → minimálisan szükséges fokozat. Üres ⇒ nincs készség-előfeltétel.
    public IReadOnlyDictionary<SkillTag, SkillMasteryLevel> RequiredSkills { get; }
    /// Csak a W7 használja. 0 ⇒ nincs szint-előfeltétel (a világ ≠ kihívásszint elv miatt
    /// ez a KIVÉTEL, nem a szabály — worlds.md 3./W7).
    public int RequiredChallengeLevel { get; }           // 0..10
}
```

---

## 8. Mi NEM tartozik ide

| Nem Core | Hová tartozik |
|---|---|
| `MonoBehaviour`, `ScriptableObject`, `Vector3`, `Transform` | Presentation / Infrastructure |
| Feladatgenerálás algoritmusa | MathEngine |
| Harci szimuláció, állapotgépek, **futásidejű ellenség-/esemény-állapot** | GameLogic |
| Mentés-szerializáció, fájl-IO | Infrastructure |
| Magyar nyelvű felhasználói szöveg | `Resources/Strings/hu.json` (kulcs-alapú) |
| Konkrét balance-ÉRTÉKEK (10 szint számai, jutalmak) | `docs/balance/*` → ScriptableObject assetek |
