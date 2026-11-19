# Kontrakt — MathEngine réteg

> Assembly: `MathHorde.MathEngine` · Namespace: `MathHorde.MathEngine.*`
> References: **kizárólag** `MathHorde.Core`
> **`noEngineReferences: true` — `using UnityEngine;` TILOS, fordítási hibát okoz.**
> Mélység: **MÉLY** — ez a legkritikusabb kontrakt (Fázis 2).
> Lefedi: spec 6.1 (műveletek), 6.2 (AST), 6.3 (metaadatok), 6.4 (distraktorok + validáció),
> 6.5 (magyarázat, opcionális), 8. (készség-osztályozás), 17.1–17.2 (tesztelhetőség).
>
> **Revízió r2 (2026-11-16, architect):** `MaxParenthesisDepth` / `MaxResultValue` /
> `MinGateGap` átvezetve a `DifficultyProfile`-ból; új validátor-kódok
> (`GATE_GAP_TOO_SMALL`, `PARENTHESIS_DEPTH_EXCEEDED`, `UNKNOWN_COUNT_MISMATCH`,
> `RESULT_RANGE_VIOLATION`); a készség-jogosultság `SkillMasteryLevel`-re cserélve (ADR-010);
> az `EquationSolving` készség-leképezés tisztázva.
>
> **Revízió r3 (2026-11-19, architect, ADR-013):** a kapacitás a kész
> `MathProblem` része; a validátor aláírása változatlan. A kapacitás/célérték
> hiányának, a vágással létrejövő döntetlennek és az optimumváltásnak explicit szabálya van.
> A végeredmény-korlát az ADR-012 szerinti önálló `profile.MaxResultValue`.

## 0. Tervezési alapelvek

| # | Elv |
|---|---|
| P1 | **Nincs string-parszolás, nincs `eval`.** Minden kifejezés `ExpressionNode` fa (spec 6.2). |
| P2 | **Teljes determinizmus.** Azonos `(seed, GenerationRequest)` ⇒ bitre azonos `MathProblem`. Semmilyen `DateTime.Now`, `Guid.NewGuid()`, hash-sorrend-függés nem használható. |
| P3 | **Tiszta függvények.** Az engine osztályai állapotmentesek (vagy csak konfigurációt tárolnak); minden változó bemenet paraméter. Így szálbiztosak és triviálisan tesztelhetők. |
| P4 | **Hibajelzés `Result<T>`-vel**, nem kivétellel. Kivétel csak szerződésszegésnél (`null` argumentum). |
| P5 | **Validálás kötelező.** A generátor a visszaadás előtt saját maga futtatja a `ProblemValidator`-t; invalid feladatot soha nem ad vissza, hanem újrapróbál (korlátozott iterációval), majd determinisztikusan egyszerűsít. |
| P6 | Minden aritmetika `checked` kontextusban; túlcsordulás ⇒ `OVERFLOW` hibakód, nem `OverflowException` a hívó felé. |
| P7 | Az engine **nem olvas konfigurációt**: minden korlát a `DifficultyProfile`-ból és a request mezőiből jön. ScriptableObject-hozzáférés itt nem létezik. |

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

    /// A megjelenítésben tényleges zárójelek maximális beágyazási mélysége.
    /// 0 ⇒ nincs zárójel. A PARENTHESIS_DEPTH_EXCEEDED ellenőrzés használja.
    int MeasureParenthesisDepth(ExpressionNode expression);
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
    /// A VÉGEREDMÉNY felső korlátja (DifficultyProfile-ból, CL10: 999).
    /// Az operandus-tartománytól független: CL10-en tag <= 99, eredmény <= 999.
    public int MaxResultValue { get; init; }
    public int MinOperandCount { get; init; }                  // >= 2
    public int MaxOperandCount { get; init; }                  // <= 5 (MVP)
    public bool AllowParentheses { get; init; }
    /// 0..2. Ha `AllowParentheses == true`, ez korlátozza a beágyazást
    /// (CL7–9: 1, CL10: 2). A generátor soha nem lép túl ezen.
    public int MaxParenthesisDepth { get; init; }
    public bool RequireParentheses { get; init; }              // kényszerített zárójel-feladat
    public bool IncludeUnknown { get; init; }                  // MissingOperand/Equation
    public UnknownPlacement UnknownPlacement { get; init; }
    public EvaluationOptions Evaluation { get; init; }
    /// Ha nem üres, a generátor PREFERÁLJA ezeket a készségeket (nem garancia).
    public IReadOnlyCollection<SkillTag> PreferredSkills { get; init; }
    public int MaxAttempts { get; init; }                      // alap 64
}

public enum UnknownPlacement { None = 0, LeftOperand = 1, RightOperand = 2, EitherSide = 3 }

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
   precedencia miatt tényleges zárójelet kap a `ToDisplayString`-ben; a mélység
   `<= MaxParenthesisDepth`.
6. `IncludeUnknown == true` ⇒ a generátor egy kész, ismeretlen nélküli fát épít, utána egy
   levelet `UnknownNode`-ra cserél, és `EqualityNode`-ot képez a maradék értékkel; a megoldást
   `IEquationSolver.Solve` **verifikálja**. Pontosan **egy** `UnknownNode` keletkezik.
7. Minden visszaadott kifejezést a `IExpressionEvaluator` kiértékel; sikertelenség ⇒ újrapróba
   `MaxAttempts`-ig; kimerülés ⇒ `Result.Fail("GENERATION_EXHAUSTED")`, amit a hívó
   fokozatos egyszerűsítéssel (operandusszám −1, tartomány felezése) kezel.
8. A végeredmény `<= MaxResultValue`; túllépés ⇒ újrapróba (nem `OVERFLOW`, mert az a
   `int`-tartományt jelenti).

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

public enum ValidationSeverity { Warning = 0, Error = 1 }
```

**Kötelező ellenőrzések:**

| Kód | Ellenőrzés | Súlyosság |
|---|---|---|
| `ANSWER_MISMATCH` | `CorrectAnswer` == `Evaluate(Expression)` | Error |
| `NO_CORRECT_OPTION` / `MULTIPLE_CORRECT_OPTIONS` | pontosan egy `IsCorrect` | Error |
| `DUPLICATE_OPTIONS` | minden opció értéke különböző | Error |
| `OPTION_COUNT_OUT_OF_RANGE` | `2 <= Count <= 4` és `== profile.AnswerOptionCount` | Error |
| `DUPLICATE_LANE` / `LANE_OUT_OF_RANGE` | sávok egyediek, `0..Count-1` | Error |
| `EMPTY_SKILL_TAGS` / `SKILL_TAG_MISMATCH` | a tagek tükrözik a fában lévő műveleteket | Error |
| `SKILL_NOT_UNLOCKED` | minden `SkillTag` benne van a `DifficultyProfile.UnlockedSkills`-ben (spec 7.2) | Error |
| `OPERATOR_NOT_ALLOWED` | a fában csak `AllowedOperators` szerepel | Error |
| `DIVISION_BY_ZERO` / `NON_INTEGER_RESULT` / `OVERFLOW` | kiértékelési hibák | Error |
| `RESULT_RANGE_VIOLATION` | a végeredmény `<= profile.MaxResultValue`; önálló konfigurált felső korlát, nem `MaxOperandValue`-ból származtatott (ADR-012) | Error |
| `UNKNOWN_NOT_UNIQUE` | `MissingOperand`/`EquationSolving` esetén egyértelmű megoldás | Error |
| `UNKNOWN_COUNT_MISMATCH` | `CountUnknowns(expr) == profile.UnknownOperandCount` | Error |
| `PARENTHESIS_DEPTH_EXCEEDED` | `MeasureParenthesisDepth(expr) <= profile.MaxParenthesisDepth` | Error |
| `GATE_CAPACITY_INVALID` | `ResourceOperationComparison`: `ResourceCapacity` hiányzik vagy `<= 0`; más feladattípusnál nem `null` | Error |
| `GATE_TARGET_MISSING` | `ResourceOperationComparison` + `ReachTarget`: `TargetValue` hiányzik | Error |
| `GATE_TIE` | `ResourceOperationComparison`: bármely két nyers vagy effektív kapueredmény egyenlő, vagy a nyers/effektív `ReachTarget` optimum nem egyértelmű (azonos legkisebb céltávolság) | Error |
| `GATE_GAP_TOO_SMALL` | bármely kapupár effektív eredményére `|A − B| < profile.MinGateGap` (CL1–3: 8, CL4+: 4 — GAME_DESIGN 3.5/1.) | Error |
| `GATE_ORDER_INVERTED_BY_CAP` | a nyers és a kapacitással vágott eredmények egyértelmű `Goal` szerinti optimuma eltérő opció (spec 5.1 záró bekezdés) | Error |
| `OPERAND_RANGE_VIOLATION` | a generált operandusok a `[Min, Max]` tartományban; a resource sablon azonosított snapshot-helye külső `0..ResourceCapacity` kontextus (ADR-016, 7.1.), más literál nem kap kivételt | Error |
| `THINK_TIME_NON_POSITIVE` | `ThinkTimeSeconds > 0` | Error |
| `DISTRACTOR_TOO_FAR` | disztraktor nagyságrendje a 4.2 szabályon kívül | Warning |
| `GUESSABLE_PATTERN` | a helyes válasz pl. mindig a legnagyobb/legkisebb opció | Warning |

**Kapacitásellenőrzés — mindkét validátorbelépésre kötelező (ADR-013):**

Ez a meglévő MathProblem-et fogadó két belépés szerződése. Az ADR-015 bindingja
saját rögzített ResourceCapacity-t hordoz és ugyanezeket az üzleti szabályokat
allokációmentesen ellenőrzi (7.3.); nem kapacitás nélküli kivétel.

- A kapacitás egyetlen forrása `problem.ResourceCapacity`; a validátor nem kap
  új paramétert, nem olvas session-/globális állapotot, és nem feltételez alapértéket.
  `IsValid(problem, profile) == Validate(problem, profile).IsValid`.
- A nyers érték az `AnswerOption.Value.Value`; az effektív érték
  `min(nyers érték, ResourceCapacity)`. A kapu itt skalár eredményű; a nyers
  `EvaluationResult`, a megjelenítés és az I1/I3 kiértékelési/egyenlőségi szabályai
  ettől nem változnak. Ez a szerződés kizárólag a felső kapacitáskorlátot kezeli;
  a jutalom tényleges alkalmazása továbbra is a `ResourceService` feladata.
- `Maximize`: legnagyobb érték; `Minimize`: legkisebb érték; `ReachTarget`:
  legkisebb `|érték − TargetValue|`. Mind a nyers, mind az effektív optimumot
  ki kell számítani. A célértéket nem vágjuk a kapacitásra. A távolság és a
  különbség számításakor az `int` túlcsordulását szélesített köztes ellenőrzés kerüli el.
- A vágással keletkező döntetlen `GATE_TIE`; eltérő, egyértelmű nyertes
  `GATE_ORDER_INVERTED_BY_CAP`. A döntetlen önmagában nem optimumváltás, ezért
  arra nem kell az utóbbi kódot is kiadni. A túl kicsi effektív rés külön
  `GATE_GAP_TOO_SMALL` hiba; ugyanaz a jelölt több hibát is hordozhat.
- Érvénytelen kapacitásnál a kapacitást igénylő ellenőrzéseket kihagyjuk;
  a nyers értékek ellenőrzése és a többi független szabály továbbra is lefut.
  Hiányzó `ReachTarget` célértéknél csak a céltávolságot igénylő ellenőrzés marad ki.

Példák (`MinGateGap = 4`, ahol más érték nincs megadva; a többi validációs
feltétel teljesülése mellett): nyers `(40, 45)`,
`Maximize`, kapacitás 100 → változatlan jobb kapu; kapacitás 40 → `GATE_TIE` és
`GATE_GAP_TOO_SMALL`. Nyers `(40, 50)`, kapacitás 45, `MinGateGap = 8` →
`GATE_GAP_TOO_SMALL`, noha a nyertes nem változik. Nyers `(45, 80)`, cél 60:
kapacitás 75 → `GATE_TIE` (effektív távolságok 15/15); kapacitás 65 →
`GATE_ORDER_INVERTED_BY_CAP` (nyersen a 45, effektíven a 65 nyer).

**Statisztikai invariáns (külön teszt, nem per-feladat):** 10 000 seedelt feladaton a helyes
válasz sávpozíciójának eloszlása egyenletes, khí-négyzet próba `p > 0.01` (spec 6.4/5. és 17.2).
Ugyanezen a mintán a helyes válasz nem lehet szisztematikusan a legnagyobb/legkisebb érték
(spec 6.4/6.: nincs számolás nélküli nyerő stratégia) — a „mindig a legnagyobbat választom”
heurisztika találati aránya ne haladja meg a `1/OptionCount + 10%` értéket.
`ResourceOperationComparison` kapupárokon ugyanilyen mintán a „nagyobb operandust választom”
heurisztika aránya legfeljebb `1 − MathEventConfigSO.NonObviousGatePairRatioMin` (0.70) —
GAME_DESIGN 3.5/4.

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
    /// Implementáció: minden előfeltételre
    ///   profile.GetMasteryLevel(prereq) >= SkillPrerequisiteSO.PrerequisiteMinLevel
    /// (alap: Practiced) — ADR-010. Nyers MasteryEstimate-összehasonlítás TILOS.
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
| `ProblemType == EquationSolving` | `+ MissingOperand`, `+ MultiStepExpressions` — **nincs `SkillTag.EquationSolving`**, az egyenlet feladattípus, nem készség (core-domain.md 3.) |

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

    // ADR-015: a valós idejű resource-gate út. Prepare allokálhat, Bind NEM.
    Result<PreparedResourceGate> PrepareResourceGate(ResourceGatePreparationRequest request);
    Result<ResourceGateBinding> BindResourceGate(
        PreparedResourceGate prepared, int snapshotValue, int resourceCapacity);
}

public sealed class ProblemGenerationRequest
{
    public int Seed { get; init; }                      // kötelező, reprodukálhatóság
    public ProblemType Type { get; init; }
    /// Kötelező. MINDEN matematikai korlát innen jön: AllowedOperators, UnlockedSkills,
    /// Min/MaxOperandValue, MaxResultValue, Min/MaxOperandCount, AllowParentheses + MaxParenthesisDepth,
    /// AllowUnknownOperand + UnknownOperandCount, AnswerOptionCount, MinGateGap,
    /// AllowNegativeResults. A request nem duplikálja ezeket.
    public DifficultyProfile Difficulty { get; init; }
    public SkillProfile SkillProfile { get; init; }     // lehet null (Fázis 2-ben)
    public int LaneCount { get; init; }                 // 2..4; >= Difficulty.AnswerOptionCount
    public float ThinkTimeSeconds { get; init; }        // > 0
    public ResourceType RewardResource { get; init; }
    public RewardSpec Reward { get; init; }
    public GateGoal Goal { get; init; }

    // ResourceOperationComparison esetén kötelező, máskor null:
    public int? ResourceSnapshotValue { get; init; }    // a feladat matematikai bázisa (pl. 20)
    public int? ResourceCapacity { get; init; }         // > 0; változatlanul MathProblem.ResourceCapacity-ba másolandó
    // TargetValueSelection vagy ResourceOperationComparison + ReachTarget esetén:
    public int? TargetValue { get; init; }
}
```

**Generálási folyamat (kötelező lépéssorrend):**

```
1. rng      = new DeterministicRandomSource(request.Seed)
2. rngExpr  = rng.Derive("expression")
3. rngDist  = rng.Derive("distractors")
4. rngLane  = rng.Derive("lane-shuffle")
5. kifejezés generálása a Difficulty + SkillProfile korlátaiból
6. helyes válasz kiértékelése / egyenlet megoldása
7. disztraktorok generálása (AnswerOptionCount - 1 darab)
8. opciók összeállítása, sávokhoz rendelés rngLane-nel keverve;
   request.ResourceCapacity és request.TargetValue megőrzése a MathProblem-en
9. SkillClassifier.Classify  →  SkillTags
10. ProblemValidator.Validate  →  ha Error, vissza az 5. lépésre (max 16 kör),
    utána fokozatos egyszerűsítés, végül Result.Fail("GENERATION_EXHAUSTED")
```

### 7.1. `ResourceOperationComparison` specialitásai (spec 5.1, 17.3)

- A generátor **két** (`AnswerOptionCount == 2`) vagy több kapu-műveletet állít elő:
  `AnswerOption.GateOperation` egy `BinaryOperationNode(op, UnknownNode, ConstantNode)` alak,
  ahol az `UnknownNode` a snapshot helye. `DisplayLabel` pl. `"×2"`, `"+25"`.
- `Value` = `Evaluate(op alkalmazva ResourceSnapshotValue-ra)`.
- A snapshot külső erőforrás-kontextus, nem sorsolt operandus (ADR-016, spec 1.3.,
  5.1.). Érvényes tartománya `0..ResourceCapacity`; a generált ConstantNode-ok
  továbbra is a profil teljes `[MinOperandValue, MaxOperandValue]` tartományában
  maradnak. A teljes offline kifejezésben kizárólag a helyes művelettel
  szerkezetileg azonosítható snapshot-behelyettesítés mentesül az operandusőr
  alól, nem tetszőleges nulla/bal oldali literál. A behelyettesített számfüggő
  készségek, MaxResultValue és minden további invariáns változatlanul kötelező.
  Azonos operátor több opcióban megengedett; pl. snapshot=0, `+1`/`+9` CL1-ben
  érvényes lehet. A különböző operátor nem lehet öncélú generálási előfeltétel.
- `IsCorrect` = a `Goal` szerinti egyetlen nyers optimum (`Maximize` ⇒ legnagyobb,
  `Minimize` ⇒ legkisebb, `ReachTarget` ⇒ a kötelező `TargetValue`-hoz legközelebbi).
  A kapacitással vágott optimum ugyanez az opció kell legyen; a generátor nem
  címkézhet át egy nyersen rosszabb választ pusztán a vágás miatt.
- A `ResourceCapacity` kötelező, pozitív, és a jelöltbe változatlanul átmásolandó
  minden próbálkozásnál, egyszerűsítésnél és batch-elemben. A `TargetValue` is
  megőrzendő. Hiányzó/érvénytelen kontextus nem javítható új véletlen húzással:
  a generátor az 5. lépés előtt `Result.Fail("GATE_CAPACITY_INVALID")`, illetve
  `Result.Fail("GATE_TARGET_MISSING")` hibával tér vissza; nem pótolja a mezőket.
- Minden kapupár **effektív** eredménye közti eltérés `>= Difficulty.MinGateGap`;
  döntetlen vagy túl kicsi rés esetén újragenerálás (`GATE_TIE`, `GATE_GAP_TOO_SMALL`).
  A vágás nem hozhat létre céltávolság-döntetlent és nem cserélheti ki a nyertest
  (`GATE_ORDER_INVERTED_BY_CAP`); a részletes szabályokat az 5. szakasz rögzíti.
- `Goal == Minimize` esetén a `RewardResource` kötelezően `EnemyArmor` (GAME_DESIGN 3.4),
  és a `RewardDelta` negatív — ez a kívánt viselkedés.
- Referencia-eset (spec 17.3): snapshot = 20, bal `×2` ⇒ 40, jobb `+25` ⇒ 45,
  `Goal = Maximize` ⇒ a jobb kapu a helyes; `RewardSpec.UseSnapshotDelta = true`,
  így a jutalom `45 − 20 = +25`.
- A `Value`, `CorrectAnswer`, `GateResolution.OperationResult` és a
  `RewardDelta = OperationResult − SnapshotValue` továbbra is a **nyers**
  matematikai eredményből származik. A kapacitás az optimum validációját szolgálja;
  nem csökkenti előre a jutalomdeltát, nem írja felül a harc közben változó
  `Current` értéket, és nem módosítja a `ResourceService` tényleges vágását.

### 7.2. `AnswerLaneSelection` specialitásai (spec 5.2)

- `AnswerOptionCount` darab opció, pontosan egy helyes, a maradék disztraktor.
- `DisplayExpression` a „`7 × 8 = ?`” alak; a `=` és a `?` a megjelenítéshez tartozik.
- `RewardSpec.FlatAmount` adja a jutalmat (a `RewardTableSO.LaneRewardBase` CL-enkénti értéke),
  `UseSnapshotDelta = false`.

### 7.3. Valós idejű resource-gate Prepare/Bind (ADR-015, ELFOGADVA)

A 7.1. teljes MathProblem-generálása változatlan offline API. A director resource
gate-hez kizárólag a fenti új Prepare/Bind utat használja; nem hívhatja a Generate,
GenerateBatch, Validate vagy Classify allokáló útját Tickből.

```csharp
// Namespace: MathHorde.MathEngine; a típus init-only request, nem élő állapot.
public sealed class ResourceGatePreparationRequest
{
    public int Seed { get; init; }
    public DifficultyProfile Difficulty { get; init; }
    public SkillProfile SkillProfile { get; init; } // null megengedett, mint a régi requestnél
    public int LaneCount { get; init; }
    public float ThinkTimeSeconds { get; init; }
    public ResourceType RewardResource { get; init; }
    public RewardSpec Reward { get; init; }
    public GateGoal Goal { get; init; }
    public int? TargetValue { get; init; }
    public int ResourceCapacity { get; init; }      // támogatandó Max, >0
}
```

**Prepare:** ugyanazok a requestőrök, készség/operátor/aritmetikai korlátok és
determinista RNG-ágak érvényesek, mint a 7.-ben. A Type implicit
ResourceOperationComparison; UseSnapshotDelta kötelező. A MathProblemId a seedből,
a sávpermutáció a külön lane-ágból származik; Bind nem sorsol új ID-t vagy sávot.
Előkészíti az immutable jelölteket, címkéiket, készség- és statikus validációs
adataikat, majd az összes `snapshot = 0..ResourceCapacity` értékre ellenőrzi,
hogy a rögzített sorrendben legalább egy teljesen érvényes binding létezik.
Csak teljes lefedettségű, változatlanul tárolható PreparedResourceGate adható vissza.
A jelöltkeresés korlátja a meglévő 16 próbálkozás és determinisztikus egyszerűsítés;
exhaustionra nincs korlátlan retry vagy profil-/célcsere. Nem tárol kötelezően
snapshotonként új MathProblem-et: a lefedettségi bejárás a Bind fix méretű
eredményét használhatja. A profil/kapacitás páros összeegyeztethetetlensége hiba,
nem lazítható a gyermek nehézségi profilja emiatt.

**Bind:** tiszta, determinisztikus, mind siker-, mind üzleti hibaágán **0 managed
allokáció**. A PreparedCapacity egyezését és a `0 <= snapshot <= capacity`
feltételt ellenőrzi, majd az első érvényes jelöltet választja. Checked aritmetika,
előre elkészített EvaluationOptions és fix méretű értékbufferek használhatók;
nincs faépítés, stringformázás, LINQ, boxing, Classify, ValidationReport vagy új
gyűjtemény. A statikus ellenőrzések a Prepare-ből, a snapshotfüggő érték-/rés-/
optimum-/kapacitás-/túlcsordulás-ellenőrzések a Bindből jönnek. A nyers és az
effektív optimum ugyanaz az egyértelmű opció; a delta továbbra is nyers eredményből
számítódik. Nem címkézhetünk át hibás nyers optimumot a kapacitás miatt.

| Kimenet | Feltétel |
|---|---|
| `INVALID_REQUEST` | a régi requestnek megfelelő mező-/jutalom-/típusösszeférhetetlenség |
| `GATE_CAPACITY_INVALID` | Prepare vagy Bind kapacitása <=0 |
| `GATE_TARGET_MISSING` | ReachTarget célja hiányzik |
| `GATE_CONTEXT_CHANGED` | pozitív Bind-kapacitás != PreparedCapacity; új Prepare szükséges |
| `GATE_SNAPSHOT_INVALID` | snapshot <0 vagy > aktuális kapacitás |
| `GENERATION_EXHAUSTED` | Prepare nem fed le minden snapshotot, vagy Bind nem talál érvényes jelöltet |

Null kötelező referencia továbbra is programozói kivétel a P4 szerint; normál
adat-/kapacitás-/aritmetikai kudarc Result.Fail, statikus hibakód-stringgel.
A Bind sikeres kimenete a Core 3.1. teljes binding-invariánsát garantálja.
A régi `IProblemValidator` két aláírása változatlan. Az implementáció a két út
közös számítási/ellenőrzési magját használja; nem kell Bindben ideiglenes MathProblem
vagy ValidationReport. Kötelező regresszió: azonos opciók/snapshot/profile mellett
a régi validátor Error-szabályai és a binding elfogadása megegyeznek; a régi út
csak az ellenőrzés Ticken kívüli teszt-orákuluma. 10 000 seedelt resource gate,
határkapacitások, nullás snapshot, determinista helyessáv-eloszlás és mindkét
Bind-kimenet mérhető nulla-allokációja a kapu része.

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
| Kapu-invariánsok | 10 000 generált kapupár: egyik sem sérti a `GATE_TIE`, `GATE_GAP_TOO_SMALL`, `GATE_ORDER_INVERTED_BY_CAP` szabályt (GAME_DESIGN 10./1.) |

Részletek: `docs/TESTING.md` (qa-unity agent felelőssége).
