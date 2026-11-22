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
> `MathProblem` része; a validátor aláírása változatlan. (Ezt az r4 felülírta.)
>
> **Revízió r4 (2026-11-22, architect, ADR-018 + ADR-019) — ÉRVÉNYES:**
> a matematikai feladatoknak nincs élő játékos-értékből származó operandusa.
> A `ResourceOperationComparison` egy **önálló, generált bázisértékre** alkalmazott
> **konkrét** műveletek összehasonlítása; a jutalom mindkét kötelező mechanikán
> **fix, pozitív `Health`-összeg**. A snapshot-alapú `PrepareResourceGate` /
> `BindResourceGate` út, a kapacitás-vágás és a kapacitás-hibakódok **kivezetve**
> (7.3.). Az ADR-016 operandusőr-kivétele **megszűnt**: a kapu minden száma
> generált operandus.

## 0. Erőforrás- és jutalomkizárások (ADR-017, ADR-018, ADR-019)

Ez a szakasz minden belépési pontra (`Generate`, `GenerateBatch`, `Validate`,
`IsValid`) kötelező, a kifejezésgenerálás **előtt** ellenőrzött, keresés és
profilváltoztatás nélkül.

| # | Szabály | Hiba |
|---|---|---|
| R1 | A feladat **egyetlen** száma sem származhat élő, harc közben változó játékos-értékből (Életerő/Pajzs pillanatnyi állása). A MathEngine nem is kap ilyen bemenetet: a requestben nincs snapshot-, kapacitás- vagy erőforrásállás-mező. | — (szerkezeti) |
| R2 | A jutalom erőforrása kizárólag `ResourceType.Health`. `Reward.Resource` ÉS `RewardResource` egyaránt `Health` kell legyen. | `Shield`/`EnemyArmor`/`Energy`: `REWARD_RESOURCE_NOT_ALLOWED`; `Ammo`: `AMMO_NOT_ALLOWED` (elsőbbséggel, ADR-017); eltérő pár: `INVALID_REQUEST` |
| R3 | `Reward.FlatAmount > 0` mindkét kötelező feladattípusnál; `0 <= Reward.IncorrectAmount <= Reward.FlatAmount` (MVP-ben `IncorrectAmount == 0`). | `REWARD_AMOUNT_INVALID` |
| R4 | A jutalom összege **nem** a feladat aritmetikájából származik. Nincs delta-képzés, nincs `UseSnapshotDelta` (a mező törölve). | — (szerkezeti) |
| R5 | A feladattípus, a `Goal`, az opciócímke és a `DisplayLabel` nem rejthet el tiltott erőforrást vagy számolt jutalmat. Nincs szöveges alias, nincs csendes erőforráscsere. | `REWARD_RESOURCE_NOT_ALLOWED` |

Az `EnemyArmor` és az `Energy` **enumértéke megmarad** (ordinal-stabilitás), de
aktív matematikai jutalomként le van tiltva; újraengedélyezése új termékdöntést
(ADR) igényel — nem a MathEngine implementációs szabadsága. A generikus AST számai
önmagukban erőforrásfüggetlenek.

---

## 0.1. Tervezési alapelvek

| # | Elv |
|---|---|
| P1 | **Nincs string-parszolás, nincs `eval`.** Minden kifejezés `ExpressionNode` fa (spec 6.2). |
| P2 | **Teljes determinizmus.** Azonos `(seed, GenerationRequest)` ⇒ bitre azonos `MathProblem`. Semmilyen `DateTime.Now`, `Guid.NewGuid()`, hash-sorrend-függés nem használható. |
| P3 | **Tiszta függvények.** Az engine osztályai állapotmentesek (vagy csak konfigurációt tárolnak); minden változó bemenet paraméter. Így szálbiztosak és triviálisan tesztelhetők. |
| P4 | **Hibajelzés `Result<T>`-vel**, nem kivétellel. Kivétel csak szerződésszegésnél (`null` argumentum). |
| P5 | **Validálás kötelező.** A generátor a visszaadás előtt saját maga futtatja a `ProblemValidator`-t; invalid feladatot soha nem ad vissza, hanem újrapróbál (korlátozott iterációval), majd determinisztikusan egyszerűsít. |
| P6 | Minden aritmetika `checked` kontextusban; túlcsordulás ⇒ `OVERFLOW` hibakód, nem `OverflowException` a hívó felé. |
| P7 | Az engine **nem olvas konfigurációt**: minden korlát a `DifficultyProfile`-ból és a request mezőiből jön. ScriptableObject-hozzáférés itt nem létezik. |
| P8 | Az engine **nem olvas futásidejű játékállapotot** (erőforrás, harc, idő). A generálás teljes bemenete a request; ezért a kész feladat a kiírástól a megoldásig változatlan (ADR-018). |

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
    /// MissingOperand/EquationSolving út; a kapu-feladat NEM használja (ADR-019).
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
9. **Minden** generált levél `ConstantNode` értéke a zárt `[MinOperandValue, MaxOperandValue]`
   tartományban van. Ez alól **nincs kivétel** — az ADR-016 snapshot-kivétele megszűnt
   (ADR-019 2.4.).

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
6. **A `ResourceOperationComparison` NEM használ disztraktorgenerátort** (7.1.): ott a
   „rossz” opciók valódi alternatív műveletek, nem hibamodellből származó számok. Minden
   kapu-opció `DistractorErrorModel == null`.

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

A két belépés szerződése azonos: `IsValid(problem, profile) == Validate(problem, profile).IsValid`.
A validátor **nem** kap új paramétert, nem olvas session-/globális állapotot és nem
feltételez alapértéket. Egy jelölt több hibakódot is hordozhat.

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
| `RESULT_RANGE_VIOLATION` | **minden** opcióérték és a végeredmény `<= profile.MaxResultValue`; önálló konfigurált felső korlát, nem `MaxOperandValue`-ból származtatott (ADR-012) | Error |
| `UNKNOWN_NOT_UNIQUE` | `MissingOperand`/`EquationSolving` esetén egyértelmű megoldás | Error |
| `UNKNOWN_COUNT_MISMATCH` | `CountUnknowns(expr) == profile.UnknownOperandCount` | Error |
| `PARENTHESIS_DEPTH_EXCEEDED` | `MeasureParenthesisDepth(expr) <= profile.MaxParenthesisDepth` | Error |
| `OPERAND_RANGE_VIOLATION` | **minden** `ConstantNode` a zárt `[profile.MinOperandValue, profile.MaxOperandValue]` tartományban; kivétel nincs (ADR-019 2.4.) | Error |
| `THINK_TIME_NON_POSITIVE` | `ThinkTimeSeconds > 0` | Error |
| `REWARD_RESOURCE_NOT_ALLOWED` | `Reward.Resource != Health` (`Shield`/`EnemyArmor`/`Energy`) — ld. 0. R2 | Error |
| `AMMO_NOT_ALLOWED` | `Reward.Resource == Ammo` tombstone (ADR-017; elsőbbség az előző kóddal szemben) | Error |
| `REWARD_AMOUNT_INVALID` | `Reward.FlatAmount <= 0`, vagy `IncorrectAmount < 0`, vagy `IncorrectAmount > FlatAmount` | Error |
| `GATE_BASE_VALUE_MISSING` | `ResourceOperationComparison`: `GateBaseValue` hiányzik; más feladattípusnál nem `null` | Error |
| `GATE_BASE_MISMATCH` | `ResourceOperationComparison`: van olyan opció, amelynek `GateOperation` bal operandusa nem a közös `GateBaseValue` konstans | Error |
| `GATE_OPERATION_NOT_CONCRETE` | `ResourceOperationComparison`: valamely `GateOperation` `UnknownNode`-ot tartalmaz, vagy nem `BinaryOperationNode(op, Constant, Constant)` alakú | Error |
| `GATE_OPERATOR_NOT_SUPPORTED` | `ResourceOperationComparison`: `Modulo` vagy `IntegerDivisionWithRemainder` kapuműveletként, illetve bármely opcióérték `Remainder != null` | Error |
| `GATE_DUPLICATE_OPERATION` | `ResourceOperationComparison`: két opció `(operátor, operandus)` párja — és így a `DisplayLabel`-je — azonos | Error |
| `GATE_TARGET_MISSING` | `ResourceOperationComparison` + `ReachTarget`: `TargetValue` hiányzik | Error |
| `GATE_TIE` | `ResourceOperationComparison`: bármely két opcióérték egyenlő, vagy a `Goal` szerinti optimum nem egyértelmű (`ReachTarget`: azonos legkisebb céltávolság) | Error |
| `GATE_GAP_TOO_SMALL` | bármely opciópárra `|A − B| < profile.MinGateGap` (CL1–3: 8, CL4+: 4 — GAME_DESIGN 3.5/1.) | Error |
| `DISTRACTOR_TOO_FAR` | disztraktor nagyságrendje a 4.2 szabályon kívül (nem kapu-típusnál) | Warning |
| `GUESSABLE_PATTERN` | a helyes válasz pl. mindig a legnagyobb/legkisebb opció | Warning |

**Kivezetett kódok (ADR-019 2.5.):** `GATE_CAPACITY_INVALID`,
`GATE_ORDER_INVERTED_BY_CAP`, `GATE_CONTEXT_CHANGED`, `GATE_SNAPSHOT_INVALID`.
Ezek kódstringek, nem szerializált ordinalok, ezért a kivezetés nem
adatformátum-változás. Új kód nem veheti át a nevüket. A kapacitás-vágás és a
„nyers vs effektív optimum” fogalompár **megszűnt**: a jutalom fix, ezért az
erőforrás `Max`-ja nem befolyásolja az optimális opciót.

**Egyértelműségi szabály (`Goal`):** `Maximize` ⇒ a legnagyobb érték az egyetlen
helyes; `Minimize` ⇒ a legkisebb; `ReachTarget` ⇒ a legkisebb `|érték − TargetValue|`.
A célértéket nem vágjuk és nem módosítjuk. A különbség- és távolságszámítás
szélesített köztes értéken (`long`) ellenőrzött, hogy `int`-túlcsordulás ne fordulhasson elő.

Példák (`profile.MinGateGap = 8`, CL1): `GateBaseValue = 2`, opciók `+ 7` → 9 és
`− 2` → 0: rés 9, `Maximize` ⇒ a `+ 7` az egyetlen helyes. `GateBaseValue = 9`,
opciók `− 1` → 8 és `− 9` → 0: rés 8, `Maximize` ⇒ a `− 1` helyes (a **nagyobb
operandus itt a rosszabb** választás, ami a GAME_DESIGN 3.5/4. heurisztika-kvótáját
szolgálja). Ellenpélda: `GateBaseValue = 5`, `+ 2` → 7 és `+ 4` → 9: rés 2 ⇒
`GATE_GAP_TOO_SMALL`.

**Statisztikai invariáns (külön teszt, nem per-feladat):** 10 000 seedelt feladaton a helyes
válasz sávpozíciójának eloszlása egyenletes, khí-négyzet próba `p > 0.01` (spec 6.4/5. és 17.2).
Ugyanezen a mintán a helyes válasz nem lehet szisztematikusan a legnagyobb/legkisebb érték
(spec 6.4/6.: nincs számolás nélküli nyerő stratégia) — a „mindig a legnagyobbat választom”
heurisztika találati aránya ne haladja meg a `1/OptionCount + 10%` értéket.
`ResourceOperationComparison` kapupárokon ugyanilyen mintán a „nagyobb műveleti
operandust választom” heurisztika aránya legfeljebb
`1 − MathEventConfigSO.NonObviousGatePairRatioMin` (0.70) — GAME_DESIGN 3.5/4.

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

`ResourceOperationComparison` esetén a feladat `SkillTags` listája **az összes opció
konkrét kifejezésének** osztályozásának az uniója, plusz a `ResourceComparison`.
Mivel minden kapuművelet konkrét, nincs szükség snapshot-függő újraosztályozásra
(a korábbi `PreparedResourceGate.GetSkillTags` ezért is megszűnt).

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
    /// Teljes, validált MathProblem-et állít elő MINDEN feladattípusra, beleértve a
    /// ResourceOperationComparison kaput is (ADR-019 2.2). Determinisztikus:
    /// azonos (request.Seed, request minden mezője) ⇒ azonos MathProblem.
    /// A visszaadott problem GARANTÁLTAN átmegy az IProblemValidator-on.
    /// A hívó a session Ticken KÍVÜL hívja (GameLogic 5.2 PrepareUpcomingEvents).
    Result<MathProblem> Generate(ProblemGenerationRequest request);

    /// Kötegelt generálás a prefetchhez és a fuzz-teszthez.
    IReadOnlyList<Result<MathProblem>> GenerateBatch(ProblemGenerationRequest request, int count);
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
    public ResourceType RewardResource { get; init; }   // kötelezően Health (0. R2)
    public RewardSpec Reward { get; init; }             // FlatAmount > 0 (0. R3)
    public GateGoal Goal { get; init; }

    /// ResourceOperationComparison esetén KÖTELEZŐ, minden más típusnál null.
    /// Forrása a MathEventConfigSO globális kapu-korlátai (SO-séma 6.), amelyeket a
    /// konfigurációs adapter → MathEventDirectorSettings → request úton kell átadni.
    public GateGenerationLimits? GateLimits { get; init; }

    /// TargetValueSelection vagy ResourceOperationComparison + ReachTarget esetén kötelező,
    /// máskor null.
    public int? TargetValue { get; init; }
}

/// Namespace: MathHorde.MathEngine. Immutable, allokációmentes bemeneti korlát.
public readonly struct GateGenerationLimits
{
    public int OperandMin { get; }      // >= 1
    public int OperandMax { get; }      // >= OperandMin
    public int MultiplierMin { get; }   // >= 2
    public int MultiplierMax { get; }   // >= MultiplierMin
    public GateGenerationLimits(int operandMin, int operandMax,
                               int multiplierMin, int multiplierMax);
}
```

**Kivezetett request-mezők (ADR-018/019):** `ResourceSnapshotValue`, `ResourceCapacity`.
A MathEngine semmilyen formában nem fogad erőforrásállást vagy kapacitást. Ilyen mező
visszavezetése kontraktsértés.

**Generálási folyamat (kötelező lépéssorrend):**

```
1. rng      = new DeterministicRandomSource(request.Seed)
2. rngExpr  = rng.Derive("expression")
3. rngDist  = rng.Derive("distractors")       // kapu-típusnál nem használt (4./6.)
4. rngLane  = rng.Derive("lane-shuffle")
5. requestőrök: 0. R2/R3, típusfüggő mezők (GateLimits, TargetValue) ellenőrzése
6. kifejezés(ek) generálása a Difficulty + GateLimits + SkillProfile korlátaiból
7. helyes válasz kiértékelése / egyenlet megoldása / Goal szerinti optimum kijelölése
8. nem-kapu típus: disztraktorok generálása (AnswerOptionCount - 1 darab)
9. opciók összeállítása, sávokhoz rendelés rngLane-nel keverve;
   request.TargetValue és (kapunál) a GateBaseValue megőrzése a MathProblem-en
10. SkillClassifier.Classify  →  SkillTags (kapunál az opciók uniója)
11. ProblemValidator.Validate  →  ha Error, vissza a 6. lépésre (max 16 kör),
    utána fokozatos egyszerűsítés, végül Result.Fail("GENERATION_EXHAUSTED")
```

Az 5. lépés hibái nem javíthatók új véletlen húzással: a generátor
`Result.Fail`-lel tér vissza (`INVALID_REQUEST`, `REWARD_RESOURCE_NOT_ALLOWED`,
`AMMO_NOT_ALLOWED`, `REWARD_AMOUNT_INVALID`, `GATE_LIMITS_INVALID`,
`GATE_TARGET_MISSING`), és nem pótolja a mezőket, nem cserél profilt, célt vagy
erőforrást. `null` kötelező referencia továbbra is programozói kivétel (P4).

### 7.1. `ResourceOperationComparison` — összehasonlító kapu (spec 5.1, ADR-018/019)

**Alak.** A feladat egy közös, **a feladat generálásakor egyszer sorsolt**
bázisértékre (`MathProblem.GateBaseValue`) alkalmazott, egymástól különböző
műveletek összehasonlítása. Minden opció kifejezése **teljesen konkrét**:

```
GateOperation = BinaryOperationNode(op, ConstantNode(GateBaseValue), ConstantNode(k))
```

`UnknownNode` itt nem fordulhat elő (`GATE_OPERATION_NOT_CONCRETE`). Példa:
`GateBaseValue = 6`, opciók `6 × 2` (12) és `6 + 25` (31). `DisplayLabel` a művelet
rövid alakja (`"×2"`, `"+25"`); `MathProblem.DisplayExpression` a bázisérték invariáns
decimális alakja (`"6"`) — a HUD ezt és a célikont mutatja, az opciók **eredménye** a
döntés előtt soha nem jeleníthető meg (spec 5.1).

**Kötelező generálási szabályok:**

1. **Effektív operandustartomány:**
   `lo = max(profile.MinOperandValue, GateLimits.OperandMin)`,
   `hi = min(profile.MaxOperandValue, GateLimits.OperandMax)`. Ha `lo > hi` ⇒
   `Result.Fail("GATE_LIMITS_INVALID")`. A `GateBaseValue` és minden műveleti
   operandus ebből a zárt tartományból jön.
2. **Engedélyezett kapuműveletek:**
   `profile.AllowedOperators ∩ { Addition, Subtraction, Multiplication, Division }`.
   Üres halmaz ⇒ `Result.Fail("GATE_OPERATOR_SET_EMPTY")`. `Modulo` és
   `IntegerDivisionWithRemainder` kapuműveletként tilos (az eredmény
   `(hányados, maradék)` pár nem rendezhető egyértelműen).
3. **Szorzó/osztó operandus:** `Multiplication` és `Division` esetén
   `k ∈ [max(GateLimits.MultiplierMin, 2), GateLimits.MultiplierMax] ∩ [lo, hi]`;
   üres metszet ⇒ `GATE_LIMITS_INVALID`. `Addition`/`Subtraction` esetén
   `k ∈ [lo, hi]`.
4. **Opciószám:** pontosan `profile.AnswerOptionCount` (2..4) opció. Két opció
   `(op, k)` párja nem lehet azonos (`GATE_DUPLICATE_OPERATION`). **Ugyanaz az
   operátor több opcióban megengedett** (pl. `− 1` vs `− 9`); a generátor
   *preferálja* a különböző operátorokat, de kizárólag emiatt nem hibázhat
   (az ADR-016 ide tartozó megállapítása érvényben marad).
5. **Bázisérték választása:** a generátor először az `(op, k)` párokat sorsolja,
   majd `[lo, hi]`-ből determinisztikus, `rngExpr`-rel permutált sorrendben az első
   olyan bázisértéket választja, amelyre **minden** opció érvényes:
   egész eredmény (`Division` esetén maradék nélkül), `>= 0`
   (`profile.AllowNegativeResults == false`), `<= profile.MaxResultValue`,
   `Remainder == null`. Nincs „sorsolok, majd eldobom” ciklus a `MaxAttempts`-en túl.
6. **Egyértelműség:** minden opcióérték páronként különböző (`GATE_TIE`), a
   páronkénti eltérés `>= profile.MinGateGap` (`GATE_GAP_TOO_SMALL`), és a `Goal`
   szerinti optimum pontosan egy opció (`ReachTarget`: egyedi legkisebb
   céltávolság). Ütközés ⇒ visszalépés a 4. lépésre.
7. **Készségek:** az opciók konkrét kifejezéseinek osztályozási uniója plusz
   `ResourceComparison`; minden tag `profile.UnlockedSkills` eleme kell legyen
   (`SKILL_NOT_UNLOCKED`). Ez CL1-en tényleges megszorítás: pl. `2 + 8 = 10`
   `CrossingTen`-t vált ki, ami CL1-en nincs feloldva, ezért nem választható.
8. **Egyszerűsítési lánc kimerülés előtt (determinisztikus, ebben a sorrendben):**
   (a) a kapuműveletek halmazát `{ Addition, Subtraction }`-ra szűkítjük;
   (b) a `[lo, hi]` tartomány felső felét elhagyjuk;
   (c) a `k` tartományt `[lo, min(hi, lo + MinGateGap)]`-re szűkítjük.
   Ezek után is sikertelen ⇒ `Result.Fail("GENERATION_EXHAUSTED")`. A profil
   `MaxResultValue`, `MinGateGap`, készség- és operandushatárai **nem lazíthatók**,
   és a `Goal` sem cserélhető a kudarc elfedésére.
9. **Jutalom:** `Reward.FlatAmount` (fix, pozitív, `Health`). A jutalom **nem**
   származik a feladat számaiból: nincs `OperationResult − bázis` delta, nincs
   kapacitás-vágás a generátorban. `MathProblem.CorrectAnswer` az optimális opció
   `EvaluationResult`-ja, `MathProblem.Expression` pedig ugyanannak az opciónak a
   `GateOperation` fája (így az `ANSWER_MISMATCH` ellenőrzés változatlanul értelmes).
10. **Sávkiosztás:** `rngLane` szerint, egyenletes eloszlással; a helyes opció
    sávpozíciója hosszú távon egyenletes (I9, 5. szakasz statisztikai invariánsa).

**Referencia-esetek (kötelező regressziós fixture-ök):**

| Profil | `GateBaseValue` | Opciók | Goal | Helyes | Megjegyzés |
|---|---:|---|---|---|---|
| CL1 (`MaxResultValue 10`, `MinGateGap 8`, ops `{+,−}`, effektív operandus `2..10`) | 2 | `+ 7` → 9, `− 2` → 0 | `Maximize` | `+ 7` | CL1 valóban megoldható; nincs `CrossingTen` |
| CL1 | 9 | `− 1` → 8, `− 9` → 0 | `Maximize` | `− 1` | azonos operátor, eltérő operandus; a **nagyobb operandus a rosszabb** |
| CL1 | 9 | `− 1` → 8, `− 9` → 0 | `Minimize` | `− 9` | a `Minimize` fix `Health`-jutalommal is érvényes (ADR-019 2.8.) |
| CL5-szintű explicit profil (`MaxResultValue >= 45`, `MinGateGap 4`) | 20 | `× 2` → 40, `+ 25` → 45 | `Maximize` | `+ 25` | a spec 5.1. példájának számai **tiszta aritmetikaként**, snapshot nélkül |

A `Minimize` cél **nem** igényel csökkentendő erőforrást: a jutalom fix `Health`,
a cél csak az optimalizálás iránya (`GateGoal` × fix jutalom ortogonális —
GAME_DESIGN 3.4). A korábbi `Goal == Minimize ⇒ RewardResource == EnemyArmor`
szabály megszűnt.

### 7.2. `AnswerLaneSelection` specialitásai (spec 5.2)

- `AnswerOptionCount` darab opció, pontosan egy helyes, a maradék disztraktor
  (4. szakasz hibamodelljei).
- `DisplayExpression` a „`7 × 8 = ?`” alak; a `=` és a `?` a megjelenítéshez tartozik.
- `GateBaseValue == null`; `GateOperation == null` minden opción.
- `RewardSpec.FlatAmount` adja a jutalmat, `Health` erőforrásra (a konkrét összeg
  forrása: `RewardTableSO`, SO-séma 6.1.). A kapu és a sáv jutalma **ugyanazzal a
  mechanizmussal** (fix összeg) működik; csak a konfigurált érték tér el.

### 7.3. KIVEZETVE — snapshot-alapú `Prepare`/`Bind` (ADR-015/016 → ADR-018/019)

A `IMathProblemGenerator.PrepareResourceGate`, `BindResourceGate`, valamint a
`ResourceGatePreparationRequest`, `PreparedResourceGate`, `ResourceGateBinding`,
`ResourceGateTemplate`, `ResourceGateOptionTemplate` típusok és a hozzájuk tartozó
`0..Max` lefedettségi követelmény **nem részei a hatályos kontraktnak**. Indok:
a kapu-feladatnak nincs futásidejű bemenete, ezért a Prepare/Bind kettéosztás
nem hordozna információt (ADR-019 2.1.).

- Ezeknek a típusoknak/metódusoknak az implementálása, visszavezetése vagy
  „kompatibilitási” megtartása **kontraktsértés** — a code-reviewer elutasítja.
- A G4 (Tick-allokációmentesség) érvényesítése nem gyengül: a kapu-feladat a
  Ticken **kívül**, a `PrepareUpcomingEvents` alatt generálódik, és a `Tick`
  mindössze egy kész, immutable `MathProblem` **referenciáját** publikálja
  (GameLogic 5.2.). A `Tick` nem hív generátort, validátort, klasszifikátort,
  nem olvas erőforrást és nem sorsol.
- Az ADR-015 teljes `0..Max` lefedettsége és az ADR-016 snapshot-pontosítása
  **elvi** szinten érvényes marad, de **nincs hatályos alkalmazási területe**:
  a termékben nincs snapshot-vezérelt matematikai feladat. Ilyen mechanika
  jövőbeli bevezetése új termékdöntést (ADR) és új kontraktszakaszt igényel;
  addig a fenti API-t nem kell és nem is szabad feléleszteni.

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
| Hibaágak | `DIVISION_BY_ZERO`, `OVERFLOW`, `NO_INTEGER_SOLUTION`, `GENERATION_EXHAUSTED`, `GATE_LIMITS_INVALID`, `GATE_OPERATOR_SET_EMPTY`, `REWARD_RESOURCE_NOT_ALLOWED`, `REWARD_AMOUNT_INVALID` mindegyikére explicit teszt |
| Kapu-invariánsok | 10 000 generált kapupár: egyik sem sérti a `GATE_TIE`, `GATE_GAP_TOO_SMALL`, `GATE_DUPLICATE_OPERATION`, `GATE_BASE_MISMATCH`, `GATE_OPERATION_NOT_CONCRETE` szabályt (GAME_DESIGN 10./1.) |
| **CL1-játszhatóság** | a szállított CL1 profillal + a szállított `MathEventConfigSO` kapu-korlátokkal `GenerateBatch` 10 000 seeden **0 `GENERATION_EXHAUSTED`** hibát ad `ResourceOperationComparison`-ra. Ez az ADR-018 1. problémájának (coverage-lehetetlenség) tényleges lezárása. |
| Időbeli stabilitás | a kész `MathProblem` immutable: a generálás után bármennyi erőforrás-/harcváltozás mellett a `CorrectAnswer` és az `IsCorrect` kiosztás változatlan (ADR-018 2. problémája) |
| Erőforrás-függetlenség | a MathEngine assembly nem hivatkozik `IResourceService`-re és nem kap erőforrásállást; statikus ellenőrzés + fordítási kapu |

Részletek: `docs/TESTING.md` (qa-unity agent felelőssége).
