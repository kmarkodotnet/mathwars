# Kontrakt — MathEngine réteg

> Assembly: `MathHorde.MathEngine` · Namespace: `MathHorde.MathEngine.*`
> References: **kizárólag** `MathHorde.Core`
> **`noEngineReferences: true` — `using UnityEngine;` TILOS, fordítási hibát okoz.**
> Mélység: **MÉLY** — ez a legkritikusabb kontrakt (Fázis 2).
> Lefedi: spec 6.1 (műveletek), 6.2 (AST), 6.3 (metaadatok), 6.4 (distraktorok + validáció),
> 6.5 (magyarázat, opcionális), 8. (készség-osztályozás), 17.1–17.2 (tesztelhetőség).

## 0. Tervezési alapelvek

| # | Elv |
|---|---|
| P1 | **Nincs string-parszolás, nincs `eval`.** Minden kifejezés `ExpressionNode` fa (spec 6.2). |
| P2 | **Teljes determinizmus.** Azonos `(seed, GenerationRequest)` ⇒ bitre azonos `MathProblem`. Semmilyen `DateTime.Now`, `Guid.NewGuid()`, hash-sorrend-függés nem használható. |
| P3 | **Tiszta függvények.** Az engine osztályai állapotmentesek (vagy csak konfigurációt tárolnak); minden változó bemenet paraméter. Így szálbiztosak és triviálisan tesztelhetők. |
| P4 | **Hibajelzés `Result<T>`-vel**, nem kivétellel. Kivétel csak szerződésszegésnél (`null` argumentum). |
| P5 | **Validálás kötelező.** A generátor a visszaadás előtt saját maga futtatja a `ProblemValidator`-t; invalid feladatot soha nem ad vissza, hanem újrapróbál (korlátozott iterációval), majd determinisztikusan egyszerűsít. |
| P6 | Minden aritmetika `checked` kontextusban; túlcsordulás ⇒ `OVERFLOW` hibakód, nem `OverflowException` a hívó felé. |

---

## 1. ExpressionEvaluator

```csharp
namespace MathHorde.MathEngine;

public interface IExpressionEvaluator
{
    /// Kiértékel egy ismeretlen nélküli fát. EqualityNode-ot NEM fogad el.
    /// Hibakódok: "DIVISION_BY_ZERO", "NON_INTEGER_RESULT", "OVERFLOW",
    ///            "UNKNOWN_IN_EXPRESSION", "NEGATIVE_RESULT_NOT_ALLOWED",
    ///            "MODULO_NON_POSITIVE_DIVISOR"
    Result<EvaluationResult> Evaluate(ExpressionNode expression, EvaluationOptions options);

    /// Behelyettesít egy értéket az UnknownNode helyére, majd kiértékel.
    Result<EvaluationResult> EvaluateWith(ExpressionNode expression, int unknownValue,
                                          EvaluationOptions options);

    /// Igaz-e az egyenlet a megadott ismeretlen-értékkel.
    Result<bool> CheckEquality(EqualityNode equation, int unknownValue,
                               EvaluationOptions options);

    /// Hány UnknownNode van a fában (ProblemValidator használja).
    int CountUnknowns(ExpressionNode expression);
}

public sealed class EvaluationOptions
{
    public bool AllowNegativeIntermediate { get; init; } = false;
    public bool AllowNegativeResult { get; init; } = false;
    public int MinValue { get; init; } = -9999;
    public int MaxValue { get; init; } = 99999;   // túllépés ⇒ "OVERFLOW"
    public static EvaluationOptions Default { get; }
}
```

**Kiértékelési szabályok (kötelező, teszttel fedett — spec 17.1):**

| Operátor | Szabály | Hiba |
|---|---|---|
| `Addition` | `a + b` | `OVERFLOW` ha kiesik `[MinValue, MaxValue]`-ból |
| `Subtraction` | `a - b` | `NEGATIVE_RESULT_NOT_ALLOWED`, ha `< 0` és nem engedett |
| `Multiplication` | `a * b` | `OVERFLOW` |
| `Division` | `a / b` | `DIVISION_BY_ZERO` ha `b == 0`; `NON_INTEGER_RESULT` ha `a % b != 0` |
| `Modulo` | `a % b` | `MODULO_NON_POSITIVE_DIVISOR` ha `b <= 0`; eredmény mindig `>= 0` |
| `IntegerDivisionWithRemainder` | `(a / b, a % b)` | `DIVISION_BY_ZERO` ha `b == 0`; `Remainder` kitöltve |

Precedencia és zárójelezés a `Core` AST-precedenciatáblázata szerint; a kiértékelés a fa
struktúráját követi, tehát a precedencia a **fa építésekor** dől el, nem kiértékeléskor.

**Kötelező smoke-értékek (spec 17.1):**
`7×8=56` · `37+28=65` · `83−47=36` · `72÷9=8` · `29÷r4=(7,1)` · `29 mod 4=1` ·
`(6+4)×3=30` · `8+4×6−7=25`.

---

## 2. EquationSolver

```csharp
public interface IEquationSolver
{
    /// Megoldja a pontosan egy UnknownNode-ot tartalmazó egyenletet egész számokra.
    /// Pl. 7 + ? = 3 × 8  ⇒  17 ;  (? + 17) × 3 = 6 × (14 − 3)  ⇒  5
    /// Hibakódok: "NO_UNKNOWN", "MULTIPLE_UNKNOWNS", "NO_INTEGER_SOLUTION",
    ///            "NOT_UNIQUE", "UNSUPPORTED_SHAPE"
    Result<int> Solve(EqualityNode equation, EvaluationOptions options);

    /// Kifejezés + célérték alakra (expr = target), MissingOperand feladathoz.
    Result<int> SolveForTarget(ExpressionNode expressionWithUnknown, int targetValue,
                               EvaluationOptions options);

    /// Egyértelműség-ellenőrzés: a megadott tartományban PONTOSAN egy egész megoldás van-e.
    /// Implementáció: a solver eredményének verifikálása + monotonitás/sávos ellenőrzés.
    bool HasUniqueIntegerSolution(EqualityNode equation, int searchMin, int searchMax,
                                  EvaluationOptions options);
}
```

**Megvalósítási elvárás (nem implementáció, de kötelező viselkedés):**
az MVP-ben az ismeretlen **lineárisan** fordul elő (egyszer, nem `Modulo` jobb oldalán,
nem osztó pozícióban). Ezeket az alakokat **szimbolikus inverz-lánccal** kell megoldani
(a külső operátort invertálva lépkedünk befelé), majd a megoldást **vissza kell helyettesíteni**
és `CheckEquality`-vel verifikálni. Ha az ismeretlen `Modulo` vagy `Division` osztójában,
vagy többször szerepel ⇒ `UNSUPPORTED_SHAPE`, és a generátor nem állíthat elő ilyen feladatot.

---

## 3. ExpressionGenerator

```csharp
public interface IExpressionGenerator
{
    /// Kifejezésfát generál a megadott korlátok szerint. Az eredmény garantáltan
    /// kiértékelhető (nincs 0-osztás, nincs tört, nincs túlcsordulás).
    Result<GeneratedExpression> Generate(ExpressionGenerationRequest request);
}

public sealed class ExpressionGenerationRequest
{
    public IRandomSource Random { get; init; }                 // kötelező
    public IReadOnlyCollection<BinaryOperator> AllowedOperators { get; init; } // nem üres
    public int MinOperandValue { get; init; }                  // >= 0 alapesetben
    public int MaxOperandValue { get; init; }                  // > MinOperandValue
    public int MinOperandCount { get; init; }                  // >= 2
    public int MaxOperandCount { get; init; }                  // <= 5 (MVP)
    public bool AllowParentheses { get; init; }
    public bool RequireParentheses { get; init; }              // kényszerített zárójel-feladat
    public bool IncludeUnknown { get; init; }                  // MissingOperand/Equation
    public UnknownPlacement UnknownPlacement { get; init; }
    public EvaluationOptions Evaluation { get; init; }
    /// Ha nem üres, a generátor PREFERÁLJA ezeket a készségeket (nem garancia).
    public IReadOnlyCollection<SkillTag> PreferredSkills { get; init; }
    public int MaxAttempts { get; init; }                      // alap 64
}

public enum UnknownPlacement { None, LeftOperand, RightOperand, EitherSide }

public sealed class GeneratedExpression
{
    public ExpressionNode Root { get; }           // EqualityNode, ha IncludeUnknown
    public EvaluationResult Value { get; }        // ismeretlen nélküli érték / a megoldás
    public int? UnknownSolution { get; }          // IncludeUnknown esetén az ismeretlen értéke
    public IReadOnlyList<SkillTag> DetectedSkills { get; }
    public int AttemptsUsed { get; }
}
```

**Generálási szabályok:**

1. A fa először **operandus-szám** szerint épül (`MinOperandCount..MaxOperandCount` sorsolva),
   majd operátorok rendelődnek hozzá az `AllowedOperators`-ból.
2. `Division` és `IntegerDivisionWithRemainder` esetén **felépítésből** biztosítjuk az
   érvényességet: előbb az osztót és a hányadost sorsoljuk, az osztandót szorzással
   állítjuk elő (`Division`), vagy hozzáadunk egy `0..divisor-1` maradékot
   (`IntegerDivisionWithRemainder`). Soha nem „sorsolunk, majd eldobunk”.
3. `Modulo`: osztó `2..min(12, MaxOperandValue)`, osztandó pozitív.
4. `AllowParentheses == false` ⇒ a fa **jobb oldali ága** nem tartalmazhat alacsonyabb
   precedenciájú operátort, azaz a lapos, balról építkező alak keletkezik.
5. `RequireParentheses == true` ⇒ legalább egy `BinaryOperationNode` `ForceParentheses` vagy
   precedencia miatt tényleges zárójelet kap a `ToDisplayString`-ben.
6. `IncludeUnknown == true` ⇒ a generátor egy kész, ismeretlen nélküli fát épít, utána egy
   levelet `UnknownNode`-ra cserél, és `EqualityNode`-ot képez a maradék értékkel; a megoldást
   `IEquationSolver.Solve` **verifikálja**.
7. Minden visszaadott kifejezést a `IExpressionEvaluator` kiértékel; sikertelenség ⇒ újrapróba
   `MaxAttempts`-ig; kimerülés ⇒ `Result.Fail("GENERATION_EXHAUSTED")`, amit a hívó
   fokozatos egyszerűsítéssel (operandusszám −1, tartomány felezése) kezel.

---

## 4. DistractorGenerator (spec 6.4)

```csharp
public interface IDistractorGenerator
{
    /// Hihető hibás válaszokat állít elő a megadott hibamodellek alapján.
    /// A visszaadott lista MINDEN eleme különbözik egymástól ÉS a helyes választól.
    Result<IReadOnlyList<Distractor>> Generate(DistractorRequest request);
}

public sealed class DistractorRequest
{
    public IRandomSource Random { get; init; }
    public ExpressionNode Expression { get; init; }    // a feladat fája
    public EvaluationResult CorrectAnswer { get; init; }
    public int Count { get; init; }                    // 1..3 (⇒ 2..4 opció)
    /// Preferált hibamodellek; üres ⇒ az engine az Expression alakjából választ.
    public IReadOnlyCollection<ErrorCategory> PreferredModels { get; init; }
    public int MinValue { get; init; }
    public int MaxValue { get; init; }
    public bool AllowNegative { get; init; }
}

public sealed class Distractor
{
    public EvaluationResult Value { get; }
    public ErrorCategory ErrorModel { get; }
    /// Rövid, gépi magyarázat a hibához (kulcs a string-táblához), pl. "err.carry".
    public string ExplanationKey { get; }
}
```

**Kötelező hibamodellek és a szabályuk:**

| `ErrorCategory` | Előállítási szabály | Mikor alkalmazható |
|---|---|---|
| `NeighbourInTimesTable` | `a×b` helyett `a×(b±1)` vagy `(a±1)×b` | gyökér `Multiplication` |
| `OnesDigitError` | helyes eredmény egyesei `±1..±3` (tízes átvitel nélkül) | bármely |
| `TensDigitError` | helyes eredmény tízesei `±1` (`±10`) | eredmény `>= 10` |
| `CarryError` | összeadásnál az átvitel elhagyása (jegyenkénti összeg, carry nélkül) | gyökér `Addition`, van átvitel |
| `OperatorPrecedenceError` | a fa **balról jobbra**, precedencia nélkül kiértékelve | legalább 2 különböző precedenciájú operátor |
| `IgnoredParentheses` | a `ForceParentheses`/zárójelek eldobása, majd precedencia szerinti kiértékelés | van zárójel |
| `RemainderMisread` | hányados helyett maradék (vagy fordítva) | `IntegerDivisionWithRemainder` / `Modulo` |
| `SignError` | `+`↔`−` csere a gyökér operátorban | gyökér `Addition`/`Subtraction` |
| `ReverseCalculationError` | `a−b` helyett `b−a`, `a÷b` helyett `b÷a` (ha egész) | nem kommutatív gyökér |

**Szabályok:**
1. Ha a modell a helyes válasszal egyező vagy már felvett értéket adna ⇒ eldobjuk, következő
   modell. Modellek elfogyása esetén **fallback**: `correct ± k`, ahol `k` növekvő, `k != 0`,
   és az érték a `[MinValue, MaxValue]` tartományban marad.
2. Minden disztraktornak a helyes válasz nagyságrendjéhez közel kell lennie: ajánlott
   `|d - correct| <= max(5, ceil(0.5 * |correct|))`, kivéve a precedencia-/zárójel-modelleket,
   ahol a tanulságos eltérés nagyobb lehet.
3. `IntegerDivisionWithRemainder` feladatnál a disztraktor is `(hányados, maradék)` pár.
4. Negatív érték csak `AllowNegative == true` esetén.
5. A generálás **determinisztikus**: azonos `Random` seed és kérés ⇒ azonos lista, azonos
   sorrendben.

---

## 5. ProblemValidator (spec 6.4 ellenőrzési lista)

```csharp
public interface IProblemValidator
{
    ValidationReport Validate(MathProblem problem, DifficultyProfile profile);
    /// Gyors invariáns-ellenőrzés a 10 000 seedelt fuzz-teszthez (spec 17.1).
    bool IsValid(MathProblem problem, DifficultyProfile profile);
}

public sealed class ValidationReport
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues { get; }
}

public sealed class ValidationIssue
{
    public string Code { get; }        // ld. a táblázatot
    public string Message { get; }     // ANGOL fejlesztői üzenet, nem játékosnak szól
    public ValidationSeverity Severity { get; }
}

public enum ValidationSeverity { Warning, Error }
```

**Kötelező ellenőrzések:**

| Kód | Ellenőrzés | Súlyosság |
|---|---|---|
| `ANSWER_MISMATCH` | `CorrectAnswer` == `Evaluate(Expression)` | Error |
| `NO_CORRECT_OPTION` / `MULTIPLE_CORRECT_OPTIONS` | pontosan egy `IsCorrect` | Error |
| `DUPLICATE_OPTIONS` | minden opció értéke különböző | Error |
| `OPTION_COUNT_OUT_OF_RANGE` | `2 <= Count <= 4` | Error |
| `DUPLICATE_LANE` / `LANE_OUT_OF_RANGE` | sávok egyediek, `0..Count-1` | Error |
| `EMPTY_SKILL_TAGS` / `SKILL_TAG_MISMATCH` | a tagek tükrözik a fában lévő műveleteket | Error |
| `SKILL_NOT_UNLOCKED` | minden `SkillTag` benne van a `DifficultyProfile.UnlockedSkills`-ben (spec 7.2) | Error |
| `OPERATOR_NOT_ALLOWED` | a fában csak `AllowedOperators` szerepel | Error |
| `DIVISION_BY_ZERO` / `NON_INTEGER_RESULT` / `OVERFLOW` | kiértékelési hibák | Error |
| `UNKNOWN_NOT_UNIQUE` | `MissingOperand`/`EquationSolving` esetén egyértelmű megoldás | Error |
| `GATE_TIE` | `ResourceOperationComparison`: a két kapu eredménye egyenlő | Error |
| `GATE_ORDER_INVERTED_BY_CAP` | a `ResourcePool.Max` vágás megfordítaná a `Goal` szerinti előnyt (spec 5.1 záró bekezdés) | Error |
| `OPERAND_RANGE_VIOLATION` | operandusok a `[Min, Max]` tartományban | Error |
| `THINK_TIME_NON_POSITIVE` | `ThinkTimeSeconds > 0` | Error |
| `DISTRACTOR_TOO_FAR` | disztraktor nagyságrendje a 4.2 szabályon kívül | Warning |
| `GUESSABLE_PATTERN` | a helyes válasz pl. mindig a legnagyobb/legkisebb opció | Warning |

**Statisztikai invariáns (külön teszt, nem per-feladat):** 10 000 seedelt feladaton a helyes
válasz sávpozíciójának eloszlása egyenletes, khí-négyzet próba `p > 0.01` (spec 6.4/5. és 17.2).
Ugyanezen a mintán a helyes válasz nem lehet szisztematikusan a legnagyobb/legkisebb érték
(spec 6.4/6.: nincs számolás nélküli nyerő stratégia) — a „mindig a legnagyobbat választom”
heurisztika találati aránya ne haladja meg a `1/OptionCount + 10%` értéket.

---

## 6. SkillClassifier (spec 8.)

```csharp
public interface ISkillClassifier
{
    /// Milyen készségeket gyakoroltat ténylegesen a kifejezés.
    IReadOnlyList<SkillTag> Classify(ExpressionNode expression, ProblemType type);

    /// Egy hibás válasz hibakategóriája a helyes és a választott érték alapján.
    /// Ha a választott opció disztraktor volt, annak ErrorModel-je elsőbbséget élvez.
    ErrorCategory ClassifyError(ExpressionNode expression, EvaluationResult correct,
                                EvaluationResult selected);

    /// Mely készségek előfeltételei a megadott készségnek (spec 7.2, 7.4).
    IReadOnlyCollection<SkillTag> GetPrerequisites(SkillTag skill);

    /// Taníthatja-e a profil jelenlegi állapotában ezt a készséget.
    bool IsEligible(SkillTag skill, SkillProfile profile);
}
```

**Osztályozási szabályok (kötelező):**

| Minta | Hozzárendelt `SkillTag`-ek |
|---|---|
| `Addition`, mindkét operandus `< 10` | `Addition`, `SingleDigitAddition` |
| `Addition`, operandus `>= 10` | `Addition`, `TwoDigitAddition` |
| `Addition`, `(a%10)+(b%10) >= 10` | `+ CrossingTen` |
| `Subtraction`, `(a%10) < (b%10)` | `Subtraction`, `SubtractionWithBorrowing` |
| `Multiplication`, egyik operandus `k` (2..10), másik `<= 10` | `Multiplication`, `TimesTable{k}` |
| `Multiplication`, mindkét operandus `>= 10` | `Multiplication`, `TwoDigitMultiplication` |
| `Division` | `Division` |
| `Modulo` vagy `IntegerDivisionWithRemainder` | `Division`, `Remainder`, `DivisionWithRemainder` |
| `>= 2` különböző precedenciájú operátor | `+ OperatorPrecedence`, `MultiStepExpressions` |
| tényleges zárójel a megjelenítésben | `+ Parentheses` |
| tartalmaz `UnknownNode` | `+ MissingOperand` |
| `ProblemType == ResourceOperationComparison` | `+ ResourceComparison` |

**Előfeltétel-gráf (alap, konfigból felülírható — `SkillPrerequisiteSO`):**
`SingleDigitAddition → TwoDigitAddition → CrossingTen` ·
`Addition → Subtraction → SubtractionWithBorrowing` ·
`Addition → Multiplication → TimesTable2..10 → TwoDigitMultiplication` ·
`Multiplication → Division → DivisionWithRemainder/Remainder` ·
`MultiStepExpressions → OperatorPrecedence → Parentheses` ·
`{Addition, Subtraction} → MissingOperand`.

---

## 7. MathProblemGenerator — a fő belépési pont

```csharp
public interface IMathProblemGenerator
{
    /// Teljes, validált MathProblem-et állít elő. Determinisztikus:
    /// azonos (request.Seed, request minden mezője) ⇒ azonos MathProblem.
    /// A visszaadott problem GARANTÁLTAN átmegy az IProblemValidator-on.
    Result<MathProblem> Generate(ProblemGenerationRequest request);

    /// Kötegelt generálás a prefetchhez és a fuzz-teszthez.
    IReadOnlyList<Result<MathProblem>> GenerateBatch(ProblemGenerationRequest request, int count);
}

public sealed class ProblemGenerationRequest
{
    public int Seed { get; init; }                      // kötelező, reprodukálhatóság
    public ProblemType Type { get; init; }
    public DifficultyProfile Difficulty { get; init; }  // kötelező
    public SkillProfile SkillProfile { get; init; }     // lehet null (Fázis 2-ben)
    public int LaneCount { get; init; }                 // 2..4
    public float ThinkTimeSeconds { get; init; }        // > 0
    public ResourceType RewardResource { get; init; }
    public RewardSpec Reward { get; init; }
    public GateGoal Goal { get; init; }

    // Csak ResourceOperationComparison esetén kötelező:
    public int? ResourceSnapshotValue { get; init; }    // a feladat matematikai bázisa (pl. 20)
    public int? ResourceCapacity { get; init; }         // Max — a GATE_ORDER_INVERTED_BY_CAP-hoz
    // Csak TargetValueSelection esetén:
    public int? TargetValue { get; init; }
}
```

**Generálási folyamat (kötelező lépéssorrend):**

```
1. rng      = new SeededRandom(request.Seed)
2. rngExpr  = rng.Derive("expression")
3. rngDist  = rng.Derive("distractors")
4. rngLane  = rng.Derive("lane-shuffle")
5. kifejezés generálása a Difficulty + SkillProfile korlátaiból
6. helyes válasz kiértékelése / egyenlet megoldása
7. disztraktorok generálása (LaneCount - 1 darab)
8. opciók összeállítása, sávokhoz rendelés rngLane-nel keverve
9. SkillClassifier.Classify  →  SkillTags
10. ProblemValidator.Validate  →  ha Error, vissza az 5. lépésre (max 16 kör),
    utána fokozatos egyszerűsítés, végül Result.Fail("GENERATION_EXHAUSTED")
```

### 7.1. `ResourceOperationComparison` specialitásai (spec 5.1, 17.3)

- A generátor **két** (`LaneCount == 2`) vagy több kapu-műveletet állít elő:
  `AnswerOption.GateOperation` egy `BinaryOperationNode(op, UnknownNode, ConstantNode)` alak,
  ahol az `UnknownNode` a snapshot helye. `DisplayLabel` pl. `"×2"`, `"+25"`.
- `Value` = `Evaluate(op alkalmazva ResourceSnapshotValue-ra)`.
- `IsCorrect` = a `Goal` szerinti optimum (`Maximize` ⇒ legnagyobb, `Minimize` ⇒ legkisebb,
  `ReachTarget` ⇒ `TargetValue`-hoz legközelebbi; döntetlen tilos).
- `ResourceCapacity` megadása esetén az **effektív** érték `min(Value, Capacity)` alapján kell
  eldönteni az optimumot; ha a vágás megfordítaná a sorrendet, a feladat érvénytelen és
  újragenerálódik.
- Referencia-eset (spec 17.3): snapshot = 20, bal `×2` ⇒ 40, jobb `+25` ⇒ 45,
  `Goal = Maximize` ⇒ a jobb kapu a helyes; `RewardSpec.UseSnapshotDelta = true`,
  így a jutalom `45 − 20 = +25`.

### 7.2. `AnswerLaneSelection` specialitásai (spec 5.2)

- `LaneCount` darab opció, pontosan egy helyes, a maradék disztraktor.
- `DisplayExpression` a „`7 × 8 = ?`” alak; a `=` és a `?` a megjelenítéshez tartozik.
- `RewardSpec.FlatAmount` adja a jutalmat (pl. +30 lőszer), `UseSnapshotDelta = false`.

---

## 8. ProblemExplanationService (spec 6.5 — opcionális, Fázis 5+)

```csharp
public interface IProblemExplanationService
{
    /// Gyermekbarát magyarázat KULCS + paraméterek formájában (a magyar szöveg a
    /// string-táblában él, nem itt — ADR-007).
    ExplanationModel Explain(MathProblem problem, MathAnswer answer);
}

public sealed class ExplanationModel
{
    public string TemplateKey { get; }                               // "explain.mul.double"
    public IReadOnlyDictionary<string, string> Parameters { get; }   // {"a":"7","b":"8",...}
    public IReadOnlyList<ExplanationStep> Steps { get; }
}

public sealed class ExplanationStep
{
    public string Expression { get; }  // "7 × 4 = 28"
    public string NoteKey { get; }     // "explain.step.double_it"
}
```

> Fázis 2–3-ban elég egy `NullExplanationService`, amely üres `Steps`-t ad vissza; a
> kontrakt viszont már most rögzített, hogy a `MathProblem.Explanation` mező feltölthető legyen.

---

## 9. Tesztelhetőségi szerződés (a qa-unity agent felé)

| Elvárás | Hogyan |
|---|---|
| 10 000 seedelt feladat invariáns-ellenőrzése (spec 17.1) | `GenerateBatch` + `IProblemValidator.IsValid`, `dotnet test`-tel a `tests/` solutionben |
| Determinizmus | ugyanaz a seed kétszer ⇒ `MathProblem` szerializált alakja bájtra azonos |
| Nincs `UnityEngine` | a `tests/` solution buildje Unity nélkül sikeres |
| Teljesítmény | `Generate` átlagos ideje `< 2 ms` közepes nehézségen (prefetch miatt nem blokkoló) |
| Hibaágak | `DIVISION_BY_ZERO`, `OVERFLOW`, `NO_INTEGER_SOLUTION`, `GENERATION_EXHAUSTED` mindegyikére explicit teszt |

Részletek: `docs/TESTING.md` (qa-unity agent felelőssége).
