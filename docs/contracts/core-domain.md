# Kontrakt — Core / Domain réteg

> Assembly: `MathHorde.Core` · Namespace: `MathHorde.Core.*`
> **`noEngineReferences: true` — ebben az assemblyben `using UnityEngine;` TILOS és
> fordítási hibát okoz.** Csak a .NET BCL használható (`System`, `System.Collections.Generic`,
> `System.Linq` — utóbbi forró ciklusban kerülendő).
> Mélység: **MÉLY** (Fázis 1–3 kötelező érvényű).

## 0. Általános szabályok

| Szabály | Indoklás |
|---|---|
| Minden domén-típus **immutable** vagy explicit mutáló metódussal rendelkezik; publikus settert csak DTO kap | determinizmus, szimulálhatóság |
| Az MVP kizárólag `int` aritmetika; `long`-ra csak túlcsordulás-ellenőrzésnél váltunk | spec 6.2 |
| Minden azonosító erős típus (`readonly struct`), nem csupasz `Guid`/`string` | összekeverhetetlenség |
| Kivétel csak programozói hibánál; üzleti hiba `Result<T>` / enum | headless szimuláció robusztussága |
| `System.Random` példányosítása TILOS (a teljes megoldásban) — minden véletlen `IRandomSource`-on keresztül, `DeterministicRandomSource` implementációval | ADR-006 |

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
    Addition, Subtraction, Multiplication,
    Division,            // egész osztás; maradék nélküli oszthatóságot a validátor kényszeríti
    Modulo,              // a % b, b > 0
    IntegerDivisionWithRemainder  // eredménye hányados+maradék pár, ld. EvaluationResult
}

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
4. `UnknownNode` legfeljebb egy példányban, és kizárólag `ProblemType.MissingOperand` /
   `EquationSolving` típusú feladatban.
5. Az MVP bővítési pontja (spec 6.0.1–6.0.4): új csomóponttípus csak új `ExpressionNode`
   leszármazott + `IExpressionVisitor` bővítés, meglévő típus módosítása nélkül.

---

## 3. Problems

```csharp
namespace MathHorde.Core.Problems;

public enum ProblemType
{
    ResourceOperationComparison,  // 5.1 — erőforrás-módosító kapuk
    AnswerLaneSelection,          // 5.2 — eredményválasztó sávok
    MissingOperand,               // 7 + ? = 3 × 8
    EquationSolving,              // (? + 17) × 3 = 6 × (14 − 3)
    TargetValueSelection          // "Érd el a célértéket!"
}

public enum GateGoal { Maximize, Minimize, ReachTarget }

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
    None,
    NeighbourInTimesTable,   // szorzótáblában szomszédos
    OnesDigitError,          // egyesek elrontása
    TensDigitError,          // tízesek elrontása
    CarryError,              // átvitel elrontása
    OperatorPrecedenceError, // rossz műveleti sorrend
    IgnoredParentheses,      // zárójel figyelmen kívül hagyása
    RemainderMisread,        // osztási maradék félreértelmezése
    SignError,               // előjelhiba
    ReverseCalculationError, // visszafelé számolási hiba
    Timeout,                 // nem adott választ (csak attempt-naplóban)
    Unclassified
}

public enum SkillTag
{
    Addition, Subtraction, Multiplication, Division, Remainder,
    OperatorPrecedence, Parentheses, MissingOperand,
    MultiStepExpressions, ResourceComparison,
    // finomszemcsés készségek (spec 8.)
    SingleDigitAddition, TwoDigitAddition, CrossingTen, SubtractionWithBorrowing,
    TimesTable2, TimesTable3, TimesTable4, TimesTable5, TimesTable6,
    TimesTable7, TimesTable8, TimesTable9, TimesTable10,
    TwoDigitMultiplication, DivisionWithRemainder
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
    public int? TargetValue { get; }               // csak TargetValueSelection
    public string Explanation { get; }             // opcionális, lehet null (6.5)
}
```

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
| I10 | `ResourceOperationComparison` esetén a két kapu eredménye **különböző** és a `Goal` szerinti sorrend egyértelmű (nincs döntetlen), figyelembe véve a `ResourcePool.Max` vágást |
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

## 4. Skills és Difficulty

```csharp
namespace MathHorde.Core.Skills;

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
    /// Elsajátítottnak tekintett készségek (MasteryEstimate >= 0.8 && Confidence >= 0.6).
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
    public int MinOperandCount { get; }      // >= 2
    public int MaxOperandCount { get; }      // <= 5 (MVP)
    public bool AllowParentheses { get; }
    public bool AllowUnknownOperand { get; }
    public bool AllowNegativeResults { get; }
    public int AnswerOptionCount { get; }    // 2..4
    public float BaseThinkTimeSeconds { get; }
    public bool AutoAdaptationEnabled { get; }
}
```

**Invariánsok:** `1 <= ChallengeLevel <= 10` és ugyanez a három dimenzióra.
`UnlockedSkills` bővítése **kizárólag** a `SkillProfile` alapján történhet — a globális szint
emelése önmagában soha nem old fel nem tanult műveletet (spec 7.2, 7.4).

---

## 5. Resources

```csharp
namespace MathHorde.Core.Resources;

/// Generikus erőforrásmodell (spec 4.3) — új típus hozzáadása nem igényel kódmódosítást
/// a ResourceService-ben.
public enum ResourceType { Ammo, Health, Shield, EnemyArmor /* negatív cél */, Energy }

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

public enum SessionResult { InProgress, Victory, Defeat, Abandoned }
public enum FailureReason { None, HealthDepleted, TimeLimit, PlayerQuit, MathMinimumNotMet }

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

public enum EnemyType { Basic, Fast, Armored, Boss }

/// Unity-mentes ellenség-leíró; a ScriptableObject-ből fordítódik (spec 4.1).
public sealed class EnemyDefinition
{
    public string EnemyId { get; }         // "basic_bot"
    public EnemyType Type { get; }
    public int MaxHealth { get; }          // > 0
    public float MovementSpeed { get; }    // világegység/mp, > 0
    public int ContactDamage { get; }      // >= 0
    public float AttackCooldownSeconds { get; } // >= 0
    public int RewardValue { get; }        // >= 0
    public string PrefabKey { get; }       // Presentation oldja fel
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

public enum TargetingMode { OwnLaneNearestOnly, OwnLaneThenAdjacent, GlobalNearest }

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
    public float RequiredMathAccuracy { get; }   // 0 ⇒ nincs matek-minimum
    public string WeaponId { get; }
}

public sealed class WorldDefinition
{
    public WorldId Id { get; }
    public string DisplayNameKey { get; }
    public int OrderIndex { get; }
    public IReadOnlyList<LevelId> Levels { get; }
    public IReadOnlyCollection<SkillTag> IntroducedSkills { get; }
    public LevelId UnlockRequirementLevelId { get; }  // null ⇒ kezdettől nyitva
}
```

---

## 8. Mi NEM tartozik ide

| Nem Core | Hová tartozik |
|---|---|
| `MonoBehaviour`, `ScriptableObject`, `Vector3`, `Transform` | Presentation / Infrastructure |
| Feladatgenerálás algoritmusa | MathEngine |
| Harci szimuláció, állapotgépek | GameLogic |
| Mentés-szerializáció, fájl-IO | Infrastructure |
| Magyar nyelvű felhasználói szöveg | `Resources/Strings/hu.json` (kulcs-alapú) |
