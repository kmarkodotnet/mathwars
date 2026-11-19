# Kontrakt — ScriptableObject konfigurációs séma

> Helye: `Assets/_Project/Configuration/` · Assembly: `MathHorde.Infrastructure`
> (a `ScriptableObject` osztályok Unity-függők, ezért **nem** a Core-ban élnek)
> A `GameConfigurationRepository` minden SO-t **Core DTO-ra fordít**, mielőtt a GameLogic
> megkapja — a GameLogic soha nem lát `ScriptableObject`-et (ARCHITECTURE 8.).
>
> Ez a dokumentum a spec **16. szakaszának MINDEN** kódváltoztatás nélkül módosítható
> paraméterét lefedi. Ami itt nincs, az a kódban sem lehet beégetett „mágikus szám”.
>
> **Revízió r3 — ADR-017 (2026-11-19):** a véges ammo, kill-refund,
> lőszerbudget/overflow és azonos-DPS követelmény kivezetve. F1-20 az adatmigráció,
> F1-21 a fegyverkészlet/HUD megvalósítása; e célkontrakt nem kész funkció.
> A korábbi revíziók bizonyítékai a történeti done jegyekben maradnak.

## 0. Konvenciók

| Szabály | Részlet |
|---|---|
| Osztálynév | `<Téma>SO`, fájlnév = osztálynév, `[CreateAssetMenu(menuName = "MathHorde/...")]` |
| Validáció | minden SO implementálja a `IValidatableConfig` interfészt: `IReadOnlyList<string> Validate()`; az `OnValidate()` ezt hívja és Editor-konzolra ír |
| Tartomány | numerikus mezőn `[Range(min,max)]` vagy `[Min(x)]` attribútum — a lenti tartomány KÖTELEZŐ |
| Szöveg | magyar szöveg **soha** nincs SO-ban; csak `...NameKey` / `...DescriptionKey` string-tábla-kulcs (ADR-007). `displayNameHu` jellegű mező TILOS. |
| Azonosító | `string Id` mező, kisbetűs snake_case, assetnévvel egyező |
| Hivatkozás | SO→SO hivatkozás direkt referenciával (nem string-gel), kivéve a runtime-feloldott `PrefabKey` |
| Enum-mező | a hivatkozott Core-enumok értékei **explicit numerikusak** (core-domain.md 0.); új enum-érték csak a lista végére, különben a kész assetek jelentése csendben megváltozik |

**Fázis-jelölés:** F1 = Fázis 1 … F7 = Fázis 7 (spec 19.).

### 0.1. Névmegfeleltetés — KANONIKUS asset-nevek

A `balance/difficulty-model.md`, a `balance/worlds.md` és a `balance/SIM-plan.md` korábbi
revíziói saját, `*Config` végű munkaneveket használtak. **A kódban és az asset-nevekben
kizárólag a bal oldali, kanonikus név létezik**; a jobb oldali nevek ugyanarra az assetre
utaló, elavult hivatkozások (ADR-009).

| Kanonikus (kód + asset) | Elavult hivatkozás a balance-/design-dokumentumokban |
|---|---|
| `GameBalanceSO` | `GameBalanceConfig` |
| `MathDifficultyTableSO` | `ChallengeLevelTableConfig`, `ChallengeLevelDefinition[]` |
| `TimePressureTableSO` | `TimePressureTableConfig`, `TimePressureDefinition[]` |
| `CombatDifficultyTableSO` | `CombatDifficultyTableConfig`, `CombatDifficultyDefinition[]` |
| `RewardTableSO` | `RewardTableConfig` |
| `AdaptiveDifficultyConfigSO` | `AdaptationRulesConfig`, `AdaptationRulesSO` |
| `InLevelAssistSO` | `InLevelAssistConfig`, `AssistTierDefinition[]` |
| `LevelDefinitionSO` | `LevelDefinition` (SO-értelemben), `levelId`/`targetDurationSec` mezőnevek |
| `WorldDefinitionSO` | `WorldDefinition` (SO-értelemben), `UnlockRequirement` |
| `EnemyDefinitionSO` | `EnemyDefinition` (SO-értelemben), `BossDefinition` (ld. 1.1) |

---

## 1. `EnemyDefinitionSO` — **F1** (spec 4.1)

`menuName: "MathHorde/Enemy Definition"`

| Mező | Típus | Tartomány | Default | Fázis | Megjegyzés |
|---|---|---|---|---|---|
| `Id` | `string` | nem üres | `"basic_bot"` | F1 | egyedi |
| `DisplayNameKey` | `string` | nem üres | `"enemy.basic"` | F1 | string-tábla kulcs |
| `Type` | `EnemyType` | enum | `Basic` | F1 | **Basic/Fast/Armored/Boss/Swarmlet** (5 érték, ADR-008) |
| `MaxHealth` | `int` | 1 – 10000 | 10 | F1 | |
| `MovementSpeed` | `float` | 0.1 – 20.0 | 2.0 | F1 | világegység/mp |
| `ContactDamage` | `int` | 0 – 1000 | 10 | F1 | átjutáskor okozott sebzés |
| `AttackCooldownSeconds` | `float` | 0.0 – 10.0 | 1.0 | F1 | |
| `Prefab` | `GameObject` | nem null | — | F1 | Presentation |
| `TintColor` | `Color` | — | fehér | F1 | gyors vizuális megkülönböztetés (**önmagában nem elég** — spec 12.) |
| `ScaleMultiplier` | `float` | 0.2 – 5.0 | 1.0 | F1 | sziluett-méret |
| `ArmorPoints` | `int` | 0 – 100 | 0 | **F5** | `Armored`: 6 (GAME_DESIGN 5.) |
| `FrontalDamageReduction` | `float` | 0.0 – 0.9 | 0.0 | **F5** | `Armored`: 0.6 |
| `SplitOnDeathEnemy` | `EnemyDefinitionSO` | lehet null | null | **F5** | `Splitter` (W4) |
| `SplitOnDeathCount` | `int` | 0 – 6 | 0 | **F5** | |
| `SpawnGroupSize` | `int` | 1 – 8 | 1 | **F1** | `Swarmlet`: 4 (kötelék-spawn, GAME_DESIGN 5.) |

**Validáció:**
- `Type == Boss` ⇒ `MaxHealth >= 100`.
- `Type == Swarmlet` ⇒ `MaxHealth <= 2` **és** `SpawnGroupSize >= 2` (a „tömeg” jelentése).
- `FrontalDamageReduction > 0` vagy `ArmorPoints > 0` ⇒ `Type == Armored` (F5-ig mindkettő 0).
- `SplitOnDeathCount > 0` ⇔ `SplitOnDeathEnemy != null`.

### 1.1. A boss nem külön asset

A `balance/worlds.md` 6. pontja egy `BossDefinition` assetet említ. **Ilyen asset nincs:**
a boss egy `EnemyDefinitionSO`, amelynek `Type == Boss`; a fázisait az F5-ben bevezetendő
`BossPhaseEntry` lista hordozza **ugyanezen** az assetben (`HpThreshold`, `ProblemType`,
`DamageOnCorrect`, `PenaltyOnWrongSeconds`, `BehaviourFlags`). Így a boss is a szokásos
ellenség-katalógusból oldódik fel (`IConfigurationProvider.GetEnemy`).

---

## 2. `WeaponConfigSO` — **F1** (spec 4.2)

`menuName: "MathHorde/Weapon Config"`

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `Id` | `string` | nem üres | `"starter_blaster"` |
| `DisplayNameKey` | `string` | nem üres | `"weapon.starter"` |
| `Damage` | `int` | 1 – 1000 | 5 |
| `FireRate` | `float` | 0.1 – 30.0 (lövés/mp) | 3.0 |
| `Range` | `float` | 1.0 – 100.0 | 25.0 |
| `ProjectileSpeed` | `float` | 0 – 200 (0 = hitscan) | 40.0 |
| `TargetingMode` | `TargetingMode` | enum | `OwnLaneThenAdjacent` |
| `AdjacentLanePenalty` | `float` | 0.0 – 1.0 | 0.0 (sebzés-szorzó szomszédos sávra) |
| `ProjectilePrefab` | `GameObject` | nem null, ha `ProjectileSpeed > 0` | — |
| `MuzzleEffectKey` | `string` | — | `"vfx.muzzle.default"` |
| `HitEffectKey` | `string` | — | `"vfx.hit.default"` |

> **Balance (ADR-017):** a Damage/FireRate típusonként szabadon konfigurálható;
> a DPS eltérhet és nőhet. `ReferenceDps` csak referencia-mérték, nem validációs
> egyenlőségi korlát. Nincs AmmoPerShot vagy véges lőszert kapcsoló mező.
> F1-21 két SO-ja: starter_blaster (5×3=15 DPS), heavy_blaster (10×3=30 DPS),
> Range25, hitscan, OwnLaneThenAdjacent. `DisplayNameKey` a Core DTO-ba is átkerül.

---

## 3. `LevelDefinitionSO` — **F1** (bővül F3, F5) (spec 10., 16. „Pályahossz”)

`menuName: "MathHorde/Level Definition"`

| Mező | Típus | Tartomány | Default | Fázis |
|---|---|---|---|---|
| `Id` | `string` | `"w1-l01"` alak | — | F1 |
| `World` | `WorldDefinitionSO` | nem null | — | F5 |
| `DisplayNameKey` | `string` | nem üres | — | F1 |
| `TemplateType` | `LevelTemplate` enum (`Tutorial=0, Standard=1, BossLevel=2`) | enum | `Standard` | F1 |
| `LaneCount` | `int` | **2 – 4** | 3 | F1 |
| `TargetDurationSeconds` | `float` | 60 – 600 | 240 | F1 |
| `Waves` | `List<WaveDefinitionEntry>` | 1 – 50 elem | — | F1 |
| `MathEvents` | `List<MathEventScheduleEntry>` | 0 – 40 (F3-tól **>= 10**, `Tutorial` esetén **>= 8**) | — | F3 |
| `BossEnemy` | `EnemyDefinitionSO` | lehet null | null | F5 |
| `BossTriggerGameTime` | `float` | 0 – 600 | 200 | F5 |
| `StartingHealth` | `int` | 1 – 999 | 100 | F1 |
| `MaxHealth` | `int` | 1 – 999 | 100 | F1 |
| `StartingShield` | `int` | 0 – 999 | 0 | F1 |
| `MaxShield` | `int` | 0 – 999 | 50 | F1 |
| `RequiredMathAccuracy` | `float` | 0.0 – 1.0 (0 = nincs) | 0.0 | F3 |
| `Weapon` | `WeaponConfigSO` | nem null, kezdő típus | — | F1 |
| `AvailableWeapons` | `List<WeaponConfigSO>` | 1 – 4 egyedi, nem null, tartalmazza Weapon-t | első pálya: starter + heavy | F1-21 |
| `EnvironmentPrefab` | `GameObject` | nem null | — | F1 |

**Validáció (a `LevelDefinition` L1–L7 invariánsainak SO-oldali tükre):**
`StartingHealth <= MaxHealth`, **`StartingShield <= MaxShield`**.
`MaxShield == 0` megengedett ⇒ a pályán nincs pajzs-mechanika, és a `ResourceService`
nem hoz létre `Shield` poolt (game-logic.md 2.2).

F1-21: a repository AvailableWeapons sorrendjéből másolt immutable
`LevelDefinition.AvailableWeaponIds` készül. Minden szállított asset és builder explicit listára migrál. Üres, hiányos vagy
duplikált lista hiba; nincs implicit katalógus-fallback. A mentés továbbra is
LevelId-t tárol, nem a szállított SO-sémát.
A bootstrap csak a pálya listájában lévő definíciókat adja a sessionnek.

### 3.1. `WaveDefinitionEntry` (beágyazott `[Serializable]`)

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `WaveIndex` | `int` | 0 – 49 | 0 |
| `StartGameTime` | `float` | 0 – 600 | 0 |
| `DurationSeconds` | `float` | 1 – 300 | 20 |
| `Spawns` | `List<WaveSpawnEntry>` | 1 – 20 | — |

### 3.2. `WaveSpawnEntry`

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `Enemy` | `EnemyDefinitionSO` | nem null | — |
| `Count` | `int` | 1 – 200 | 5 |
| `SpawnIntervalSeconds` | `float` | 0.05 – 10.0 | 0.8 |
| `AllowedLanes` | `List<int>` | elemek 0..3; üres = bármelyik | üres |

### 3.3. `MathEventScheduleEntry`

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `TriggerGameTime` | `float` | 0 – 600 | 10 |
| `ProblemType` | `ProblemType` | enum | `AnswerLaneSelection` |
| `PreviewDurationSeconds` | `float` | 0.5 – 10.0 | 2.0 |
| `ThinkTimeSecondsOverride` | `float` | 0 – 60 (0 = profilból) | 0 |
| `RewardResource` | `ResourceType` | aktív nem-ammo enum | `Health` |
| `Goal` | `GateGoal` | enum (`Maximize`/`Minimize`/`ReachTarget`) | `Maximize` |
| `RewardAmountOverride` | `int` | 0 – 500 (0 = `RewardTableSO`-ból) | 0 |
| `TargetValueOverride` | `int` | 0 – 999 (0 = generált) | 0 |

**Validáció:**
- Ammo soha nem választható; a tombstone és hiányzó/letiltott reward-pool Error. Shield jutalomhoz MaxShield > 0 kell. Ez minden schedule-típusra és adapterbelépésre kötelező.
- `MathEvents` időpontjai között legalább `TimePressureTableSO.MinGapBetweenProblemsSeconds`
  (a pálya CL-jéhez tartozó sor) legyen — a korábbi, globális
  `MathEventConfigSO.MathEventCooldownSeconds` csak **alsó korlát** (hard floor).
- `LaneCount == 2` ⇒ `ProblemType` bármelyik lehet (2 sáv 2 opcióval is érvényes).
- `Goal == ReachTarget` ⇒ `ProblemType == TargetValueSelection`.
- `Goal == Minimize` ⇒ `RewardResource == EnemyArmor` (az egyetlen értelmes csökkentendő).
- Legfeljebb **2 egymást követő** azonos `ProblemType` (GAME_DESIGN 2.2).

---

## 4. `WorldDefinitionSO` — **F5** (spec 10.)

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `Id` | `string` | `"w1"` alak | — |
| `DisplayNameKey` | `string` | nem üres | — |
| `OrderIndex` | `int` | 0 – 50 | 0 |
| `Levels` | `List<LevelDefinitionSO>` | 1 – 30 | — |
| `IntroducedSkills` | `List<SkillTag>` | 0 – 10 | üres |
| `EnabledGateGoals` | `List<GateGoal>` | 1 – 3 | `{ Maximize }` |
| `IntroducedEnemyTypes` | `List<EnemyType>` | 1 – 5 | `{ Basic }` |
| `Unlock` | `WorldUnlockRequirementEntry` | lehet null | null |
| `ThemeColor` | `Color` | — | — |

### 4.1. `WorldUnlockRequirementEntry` (beágyazott `[Serializable]`)

A korábbi, egyetlen `UnlockRequirementLevel` referencia **nem** volt elegendő: a
`balance/worlds.md` 3. pontja csillag-arányt ÉS készség-fokozatot is megkövetel.

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `RequiredWorld` | `WorldDefinitionSO` | lehet null | null |
| `RequiredStarRatio` | `float` | 0.0 – 1.0 | **0.60** |
| `RequiredSkills` | `List<SkillRequirementEntry>` | 0 – 5 | üres |
| `RequiredChallengeLevel` | `int` | 0 – 10 (0 = nincs) | 0 |

`SkillRequirementEntry`: `{ SkillTag Skill; SkillMasteryLevel MinLevel; }`
(`SkillMasteryLevel`: `Locked=0, Introduced=1, Practiced=2, Mastered=3` — core-domain.md 4.)

**Validáció:** a `RequiredWorld` gráf körmentes; `RequiredChallengeLevel > 0` legfeljebb
**egy** világon (worlds.md W7 — a „világ ≠ kihívásszint” elv kivétele).

---

## 5. `MathDifficultyTableSO` — **F2** (spec 7.2, 16. „Matematikai szintek”, „Engedélyezett operátorok”, „Operandustartományok”, „Feladatösszetettség”, „Válaszsávok száma”)

> **Kötelezően 10 sor** — a spec 7.2 táblázata nem égethető a kódba.
> Az értékek forrása: `balance/difficulty-model.md` 2. szakasz.

`menuName: "MathHorde/Math Difficulty Table"` · mező: `List<MathDifficultyLevelEntry> Levels` (pontosan 10 elem)

### `MathDifficultyLevelEntry`

| Mező | Típus | Tartomány | Default (szint 1 → 10) |
|---|---|---|---|
| `Level` | `int` | 1 – 10 | 1..10 |
| `DescriptionKey` | `string` | nem üres | `"diff.l1"` … `"diff.l10"` |
| `AllowedOperators` | `List<BinaryOperator>` | nem üres | L1–L2: {Add, Sub}; L3–L4: +{Mul, Div}; L5: +{Mod, DivRem}; L6+: mind |
| `MinOperandValue` | `int` | 0 – 999 | 1 |
| `MaxOperandValue` | `int` | 1 – 999 | L1: 10, L2: 20, L3: 20, L4: 50, L5: 100, L6: 100, L7: 50, L8: 60, L9: 99, L10: 99 |
| `MaxResultValue` | `int` | 1 – 9999 | L1: 10, L2: 20, L3: 100, L4: 100, L5: 100, L6: 200, L7: 200, L8: 200, L9: 500, L10: 999 |
| `MinOperandCount` | `int` | 2 – 5 | L1–L7: 2, L7–L8: 3, L9: 3, L10: 4 |
| `MaxOperandCount` | `int` | 2 – 5 | L1–L5: 2, L6: 3, L7–L8: 3, L9: 4, L10: 5 |
| `AllowParentheses` | `bool` | — | L1–L7: false, L7+: true |
| `MaxParenthesisDepth` | `int` | 0 – 2 | L1–L7: 0, L7–L9: 1, L10: 2 |
| `AllowUnknownOperand` | `bool` | — | L1–L7: false, L8+: true |
| `UnknownOperandCount` | `int` | 0 – 1 | L1–L7: 0, L8+: 1 |
| `UnknownPlacement` | `UnknownPlacement` enum | enum | L8–L9: `RightOperand`, L10: `EitherSide` |
| `AllowNegativeResults` | `bool` | — | false (minden szinten) |
| `AnswerOptionCount` | `int` | **2 – 4** | L1–L2: 2, L3–L5: 3, L6+: 4 |
| `LaneCount` | `int` | **2 – 4** | = `AnswerOptionCount` |
| `MinGateGap` | `int` | 1 – 50 | L1–L3: **8**, L4+: **4** (GAME_DESIGN 3.5/1.) |
| `BaseThinkTimeSeconds` | `float` | 2.0 – 30.0 | **L1: 14.0, L2: 13.0, L3: 11.5, L4: 10.0, L5: 8.0, L6: 7.5, L7: 7.0, L8: 6.5, L9: 5.5, L10: 4.5** |
| `RequiredSkills` | `List<SkillTag>` | — | a szint által bevezetett készségek |
| `MathComplexity` | `int` | 1 – 10 | = `Level` |
| `TimePressure` | `int` | 1 – 10 | = `Level` |
| `CombatDifficulty` | `int` | 1 – 10 | = `Level` |

**Validáció:**
- `MaxOperandValue > MinOperandValue`; `MaxOperandCount >= MinOperandCount`.
- `AllowParentheses == (MaxParenthesisDepth > 0)`;
  `AllowUnknownOperand == (UnknownOperandCount > 0)` (core-domain.md 4. invariáns).
- `AllowUnknownOperand == true` ⇒ `AllowedOperators` tartalmaz `Addition`-t vagy
  `Multiplication`-t.
- `LaneCount >= AnswerOptionCount` (több sáv lehet, mint opció; kevesebb nem).
- `BaseThinkTimeSeconds` szigorúan monoton **csökkenő** a `Level` szerint.
- A `Levels` szigorúan monoton `Level` szerint, pontosan 10 elem.

A konkrét értékek forrása és indoklása: `docs/balance/difficulty-model.md` 2. szakasz
(balance-designer agent felelőssége) — **ez a tábla az egyetlen kódbeli igazság**.

---

## 5.1. `TimePressureTableSO` — **F2** (spec 7.3, 16. „Gondolkodási idő”)

> Kötelezően 10 sor. Értékek: `balance/difficulty-model.md` 3. táblázat.
> A `MathDifficultyTableSO.BaseThinkTimeSeconds` és az itteni `ThinkTimeSeconds`
> **ugyanaz a szám**; a kanonikus forrás ez a tábla, a `MathDifficultyTableSO` mezője
> az `Validate()`-ben ellenőrzött tükörérték (eltérés ⇒ Error).

`menuName: "MathHorde/Time Pressure Table"` · mező: `List<TimePressureLevelEntry> Levels` (10 elem)

| Mező | Típus | Tartomány | Default (L1 → L10) |
|---|---|---|---|
| `Level` | `int` | 1 – 10 | 1..10 |
| `ThinkTimeSeconds` | `float` | **4.0 – 30.0** | 14.0, 13.0, 11.5, 10.0, 8.0, 7.5, 7.0, 6.5, 5.5, 4.5 |
| `GateApproachSeconds` | `float` | 3.0 – 30.0 | 12.0, 11.0, 10.0, 9.0, 8.0, 7.5, 7.0, 6.5, 6.0, 5.0 |
| `AnswerSpawnDelaySeconds` | `float` | 0.5 – 5.0 | 2.5, 2.5, 2.0, 2.0, 1.5, 1.5, 1.5, 1.2, 1.0, 1.0 |
| `GateApproachSpeed` | `float` | 0.5 – 20.0 | 1.2, 1.3, 1.5, 1.7, 1.9, 2.1, 2.3, 2.6, 2.9, 3.3 |
| `MinGapBetweenProblemsSeconds` | `float` | **8.0** – 60.0 | 14, 13, 12, 11, 10, 10, 9, 9, 8, 8 |

**Validáció:** `ThinkTimeSeconds >= 4.0` minden szinten (a 7 éves korosztály motoros
sávváltási ideje ≈0,8 s — difficulty-model.md 3.); `MinGapBetweenProblemsSeconds >= 8.0`
(GAME_DESIGN 2.2: legalább 8 s tiszta harc két döntés között);
mindhárom időmező monoton csökkenő a `Level` szerint.

Futásidejű szorzó: `timePressureScale` (0.70 – 1.60, alap 1.0) — **nem** SO-mező, hanem
az `IAdaptiveDifficultyService` állapota; minden fenti időértékre hat (TimePressure
dimenzió, spec 7.3).

---

## 6. `MathEventConfigSO` — **F3** (spec 16. „Matematikai feladatok gyakorisága”)

> Pálya- és CL-független **globális** korlátok és geometria. Ami CL-függő
> (gondolkodási idő, kapu-sebesség, minimális szünet), az az 5.1-es táblában van;
> ütközés esetén a **szigorúbb** érvényesül.

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `MathEventIntervalSeconds` | `float` | 5 – 120 | 20 |
| `MathEventIntervalJitter` | `float` | 0 – 0.5 | 0.15 |
| `MathEventCooldownSecondsFloor` | `float` | 2 – 60 | 6 |
| `MaxConcurrentMathEvents` | `int` | 1 – 3 | 1 |
| `GateSpawnDepth` | `float` | 5 – 60 | 30 |
| `DecisionZoneDepth` | `float` | 0.5 – 10 | 3.0 |
| `FeedbackDisplaySeconds` | `float` | 0.5 – 5.0 | 1.5 |
| `GoalIconLeadSeconds` | `float` | 1.0 – 10.0 | 2.0 |
| `MaxConsecutiveSameProblemType` | `int` | 1 – 3 | 2 |
| `MaxConsecutiveSameGateSide` | `int` | 1 – 3 | 2 |
| `NonObviousGatePairRatioMin` | `float` | 0.0 – 1.0 | **0.30** |
| `IncorrectRewardAmount` | `int` | 0 – 100 | 0 |
| `ResourceGateOperandMin` | `int` | 1 – 100 | 2 |
| `ResourceGateOperandMax` | `int` | 2 – 200 | 50 |
| `ResourceGateMultiplierMin` | `int` | 2 – 5 | 2 |
| `ResourceGateMultiplierMax` | `int` | 2 – 10 | 4 |

`GoalIconLeadSeconds`: a kapucél-ikon ennyivel a döntési pont előtt megjelenik
(GAME_DESIGN 3.4: legalább 2 s). `NonObviousGatePairRatioMin`: pályánként legalább ennyi
arányban olyan kapupár, ahol a **nagyobb operandus nem a jó választás**
(GAME_DESIGN 3.5/4.) — a generátor ezt pályaszinten könyveli, a `SIM-plan.md` méri.

---

## 6.1. `RewardTableSO` — **F3** (spec 16. „Jutalmak”)

> 10 sor. Értékek: `balance/difficulty-model.md` 5.3.

| Mező | Típus | Tartomány | Default (L1 → L10) |
|---|---|---|---|
| `Level` | `int` | 1 – 10 | 1..10 |
| `GateRewardBase` | `int` | 1 – 500 | 30, 34, 40, 50, 60, 70, 82, 96, 112, 130 |
| `LaneRewardBase` | `int` | 1 – 500 | 20, 22, 26, 32, 38, 44, 52, 60, 70, 82 |
| `MathDecisionCount` | `int` | 8 – 24 | 10, 11, 12, 13, 14, 14, 15, 15, 16, 16 |
| `HealthRewardBase` | `int` | 0 – 200 | 15 (minden szinten) |
| `ShieldRewardBase` | `int` | 0 – 200 | 10 (minden szinten) |
| `EnemyArmorRewardBase` | `int` | 0 – 200 | 10 (minden szinten) |

Globális mezők ugyanezen az asseten:

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `RewardScalePerChallengeLevel` | `float` | 0.0 – 0.3 | 0.0 (a tábla már CL-enkénti) |
| `RescueWaveRewardMultiplier` | `float` | 1.0 – 3.0 | 2.0 |

**Ammo-szabály:** a tábla sem tartalmaz lőszeres oszlopot vagy ResourceType.Ammo sort.
Health lane jutalom: HealthRewardBase; Shield lane: ShieldRewardBase; EnemyArmor:
EnemyArmorRewardBase az engedélyezett iránnyal; más aktív resource csak explicit
hostkonfigurációval. Resource gate továbbra is nyers snapshot-delta, GateRewardBase
nem írja felül. A korábbi generikus Gate/Lane értékek megőrzött, nem-ammo referenciák,
nem igazolt új balance.

**Validáció:** `MathDecisionCount >= 10` az L1 kivételével (`Tutorial` sablon 8-at is
engedhet, de a **pálya**, nem a tábla szintjén — ld. 3.);
`GateRewardBase > LaneRewardBase` minden szinten (a kapu nagyobb tét).

---

## 6.2. Kivezetett lőszerbudget — ADR-017

Nincs aktív AmmoBudgetSO, kill-refund, combat-ammo share vagy overflow score.
A régi elnevezések nem implementálandó konfigurációk. A Health/Shield és más
aktív resource jutalom a meglévő RewardTableSO-ból, a tényleges profil és
kapacitás ellenőrzésével származik; szimulációs újramérés szükséges.

---

## 7. `CombatBalanceSO` — **F1** (spec 16. „Ellenségek sebessége/életereje”, „Ellenséghullámok mérete”)

> Globális szorzók és futásidejű korlátok. A **CL-függő** szorzókat a 7.1 tábla adja;
> a kettő **összemultiplikálódik** (`effektív = CombatBalanceSO × CombatDifficultyTableSO[CL]`).

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `EnemySpeedMultiplier` | `float` | 0.5 – 2.0 | 1.0 |
| `EnemyHealthMultiplier` | `float` | 0.5 – 2.0 | 1.0 |
| `EnemyDamageMultiplier` | `float` | 0.5 – 2.0 | 1.0 |
| `WaveSizeMultiplier` | `float` | 0.5 – 3.0 | 1.0 |
| `SpawnRateMultiplier` | `float` | 0.5 – 3.0 | 1.0 |
| `MaxWaveGapSeconds` | `float` | 0 – 10 | 2.0 |
| `PlayerLaneChangeSpeed` | `float` | 1 – 30 | 10.0 |
| `EnemyDespawnDepth` | `float` | -20 – 0 | -2.0 |
| `MaxActiveEnemies` | `int` | 10 – 300 | 60 |
| `EnemyPoolSize` | `int` | 10 – 400 | 80 |
| `ProjectilePoolSize` | `int` | 20 – 500 | 120 |
| `PerChallengeLevelSpeedStep` | `float` | 0.0 – 0.2 | 0.0 (a 7.1 tábla váltja ki) |
| `PerChallengeLevelHealthStep` | `float` | 0.0 – 0.3 | 0.0 (ua.) |
| `PerChallengeLevelWaveSizeStep` | `float` | 0.0 – 0.3 | 0.0 (ua.) |
| `ShieldAbsorbsBeforeHealth` | `bool` | — | true (GAME_DESIGN 6.1) |

> A `PerChallengeLevel*Step` mezők **megmaradnak**, de alapértékük 0: a CL-skálázás
> kanonikus helye a 7.1 tábla. Két párhuzamos skálázási mechanizmus egyszerre bekapcsolva
> duplán nehezítene — a `Validate()` figyelmeztet, ha a tábla és a step is nem-neutrális.

---

## 7.1. `CombatDifficultyTableSO` — **F1** (spec 7.3)

> 10 sor. Értékek: `balance/difficulty-model.md` 4. táblázat.

| Mező | Típus | Tartomány | Default (L1 → L10) |
|---|---|---|---|
| `Level` | `int` | 1 – 10 | 1..10 |
| `WaveCount` | `int` | 3 – 20 | 5, 6, 6, 7, 8, 8, 9, 9, 10, 10 |
| `EnemiesPerLevel` | `int` | 20 – 400 | 55, 65, 78, 92, 110, 126, 142, 160, 180, 205 |
| `EnemySpeedMultiplier` | `float` | 0.5 – 2.0 | 0.70 … 1.42 |
| `EnemyHealthMultiplier` | `float` | 0.5 – 2.0 | 0.70 … 1.60 |
| `EnemyDamageMultiplier` | `float` | 0.5 – 2.0 | 0.60 … 1.42 |
| `PeakSpawnRatePerSecond` | `float` | 0.2 – 4.0 | 0.6 … 1.8 |
| `ArmoredRatio` | `float` | 0.0 – 0.5 | 0.00, 0.00, 0.05, 0.08, 0.12, 0.15, 0.18, 0.22, 0.25, 0.28 |
| `SwarmletRatio` | `float` | 0.0 – 0.5 | 0.00, 0.10, 0.15, 0.18, 0.20, 0.22, 0.24, 0.25, 0.26, 0.28 |

**Validáció:** `ArmoredRatio + SwarmletRatio <= 0.6`; `EnemiesPerLevel` és minden szorzó
monoton nem csökkenő a `Level` szerint. A `SwarmletRatio > 0` szint **megköveteli**, hogy
a pálya világa `IntroducedEnemyTypes`-ban tartalmazza a `Swarmlet`-et — ha nem, a hiányzó
arány `Basic`-ra esik vissza (worlds.md: a `Swarmlet` a W3-ban jelenik meg; W1–W2-ben a
tábla CL2+ „rajzó arány” sorai `Basic`/`Fast` arányként realizálódnak).

---

## 8. `AdaptiveDifficultyConfigSO` — **F4** (spec 7.4–7.6, 16. „Adaptációs küszöbök”)

> Kanonikus név; a `difficulty-model.md` `AdaptationRulesConfig` és a game-logic.md
> `AdaptationRulesSO` hivatkozása **ugyanerre** az assetre utal (0.1).
> Pályák **közötti** adaptáció. A pályán belüli segítség: 8.1.

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `TargetAccuracyMin` | `float` | 0.5 – 0.95 | 0.75 |
| `TargetAccuracyMax` | `float` | 0.5 – 0.99 | 0.85 |
| `AccuracyDeadbandLow` | `float` | 0.3 – 0.9 | **0.65** |
| `AccuracyDeadbandHigh` | `float` | 0.6 – 0.99 | **0.88** |
| `ShortWindowSize` | `int` | 3 – 10 | 5 |
| `LongWindowSize` | `int` | 10 – 100 | 40 |
| `MinDecisionsBeforeChange` | `int` | 10 – 60 | 30 |
| `DirectionChangeCooldownLevels` | `int` | 0 – 5 | 2 |
| `MaxDimensionDeltaPerSession` | `int` | 1 – 3 | 2 |
| `ConsecutiveErrorsForAssistance` | `int` | 2 – 6 | 3 |
| `ConsecutiveTimeoutsForMoreTime` | `int` | 2 – 6 | 2 |
| `TimeoutRatioForMoreTime` | `float` | 0.05 – 0.5 | 0.20 |
| `MaxLevelChangePerSession` | `int` | 1 – 2 | 1 |
| `HysteresisBandAccuracy` | `float` | 0.0 – 0.2 | 0.05 |
| `MinSessionsBeforeAdjust` | `int` | 1 – 5 | 1 |
| `ConsecutiveFailuresForDecrease` | `int` | 2 – 5 | 2 |
| `TimePressureScaleMin` | `float` | 0.5 – 1.0 | 0.70 |
| `TimePressureScaleMax` | `float` | 1.0 – 2.0 | 1.60 |
| `TimePressureScaleStep` | `float` | 1.0 – 1.5 | 1.15 |
| `MinChallengeLevel` | `int` | 1 – 10 | 1 |
| `MaxChallengeLevel` | `int` | 1 – 10 | 10 |
| `DisplayWeightMath` | `float` | 0.0 – 1.0 | **0.5** |
| `DisplayWeightTime` | `float` | 0.0 – 1.0 | **0.2** |
| `DisplayWeightCombat` | `float` | 0.0 – 1.0 | **0.3** |

**Validáció:** `AccuracyDeadbandLow < TargetAccuracyMin <= TargetAccuracyMax <
AccuracyDeadbandHigh` (a célsávban semmi nem történik — difficulty-model.md 6.3);
`DisplayWeight*` összege pontosan 1.0 (a HUD 1–10 szintje súlyozott átlag).

---

## 8.1. `InLevelAssistSO` — **F4** (spec 7.5)

`mező: List<AssistTierEntry> Tiers` (pontosan 3 aktív elem, Tier=1,2,4; ADR-017) · értékek: difficulty-model.md 6.5

| Mező | Típus | Tartomány | Default (T1, T2, T4) |
|---|---|---|---|
| `Tier` | `int` | 1, 2, 4 | 1, 2, 4 |
| `TriggerType` | enum `AssistTrigger` (`ConsecutiveErrors=0, TimeoutsInWindow=1, HealthDropRate=3`; 2 inaktív tombstone) | enum | `ConsecutiveErrors`, `ConsecutiveErrors`, `HealthDropRate` |
| `TriggerThreshold` | `float` | 0.0 – 10.0 | 2, 3, 0.30 |
| `ThinkTimeMultiplier` | `float` | 1.0 – 2.0 | 1.25, 1.25, 1.0 |
| `GateSpeedMultiplier` | `float` | 0.4 – 1.0 | 0.85, 0.85, 1.0 |
| `MathComplexityDelta` | `int` | -2 – 0 | 0, -1, 0 |
| `LaneCountOverride` | `int` | 0 – 4 (0 = nincs) | 0, 3, 0 |
| `SpawnDensityMultiplier` | `float` | 0.4 – 1.0 | 1.0, 1.0, 0.75 |
| `EnemySpeedMultiplier` | `float` | 0.4 – 1.0 | 1.0, 1.0, 0.85 |
| `EnemyDamageMultiplier` | `float` | 0.4 – 1.0 | 1.0, 1.0, 0.80 |
| `RescueWaveEnabled` | `bool` | — | false (mindhárom) |
| `MaxActivationsPerLevel` | `int` | 1 – 99 | 99 (mindhárom) |
| `DurationProblems` | `int` | 0 – 10 | 2, 3, 0 |
| `DurationSeconds` | `float` | 0 – 120 | 0, 0, 25 |
| `RecoveryCorrectAnswers` | `int` | 1 – 5 | 2 (mindegyik) |

**Kötelező szabály:** a segítség **csak könnyíthet** és **csak az ezután létrejövő**
feladatokra/hullámokra hat; minden multiplikátor-mező `<= 1.0`, kivéve a
`ThinkTimeMultiplier`-t (`>= 1.0`). Nehezítő érték ⇒ `Validate()` Error.

---

A korábbi T3/AmmoBelowRatio ág inaktív; nincs helyettesítő trigger vagy ingyenes
jutalom. Sorszámát nem használjuk újra. A mentőfeladatok további mélyítése F4-00.

## 9. `SkillPrerequisiteSO` — **F4** (spec 7.2, 7.4)

Mező: `List<SkillPrerequisiteEntry> Entries`

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `Skill` | `SkillTag` | enum | — |
| `Prerequisites` | `List<SkillTag>` | 0 – 5 | üres |
| `PrerequisiteMinLevel` | `SkillMasteryLevel` | enum | `Practiced` |
| `MasteryThreshold` | `float` | 0.5 – 1.0 | 0.8 |
| `PracticedThreshold` | `float` | 0.2 – 0.8 | 0.5 |
| `MinAttemptsForMastery` | `int` | 3 – 50 | 8 |
| `MinSamplesForConfidence` | `int` | 3 – 50 | 8 |
| `ReviewIntervalDays` | `float` | 0.5 – 30 | 3 |

**Validáció:** az előfeltétel-gráf körmentes (DAG); `PracticedThreshold < MasteryThreshold`.
Ez az asset a `SkillMasteryLevel` küszöbeinek **egyetlen** forrása (core-domain.md 4.,
ADR-010) — a kódban küszöb-literál TILOS.

---

## 10. `GameBalanceSO` — **F1** (spec 7.6, 9.1)

> Egyetlen példány, `Resources/Config/`. A termék „alap-szigora”; játék közben NEM változik
> (difficulty-model.md 1.).

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `TargetAccuracyMin` | `float` | 0.5 – 0.95 | 0.75 |
| `TargetAccuracyMax` | `float` | 0.5 – 0.99 | 0.85 |
| `TargetSuccessRate` | `float` | 0.3 – 0.95 | 0.70 |
| `GuessingSuccessRateCap` | `float` | 0.0 – 0.3 | 0.15 |
| `ReferenceDps` | `float` | 1 – 100 | 15.0 (`Damage 5 × FireRate 3.0`) |
| `DefaultChallengeLevel` | `int` | 1 – 10 | 3 |

---

## 11. `ParentSettingsDefaultsSO` — **F6** (spec 11.5)

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `DailyPlayTimeLimitMinutes` | `int` | 0 – 240 (0 = nincs korlát) | 0 |
| `AllowedOperators` | `List<BinaryOperator>` | nem üres | mind |
| `FocusSkills` | `List<SkillTag>` | 0 – 10 | üres |
| `InitialChallengeLevel` | `int` | 1 – 10 | 3 |
| `AutoAdaptationEnabled` | `bool` | — | true |
| `SoundEnabled` | `bool` | — | true |
| `HapticsEnabled` | `bool` | — | true |
| `TimePressureLimited` | `bool` | — | false |
| `ThinkTimeBonusMultiplier` | `float` | 1.0 – 3.0 | 1.0 |
| `BreakSuggestionMinutes` | `int` | 10 – 120 | **25** (GAME_DESIGN 8.) |
| `HardStopMinutes` | `int` | 0 – 240 | 45 |
| `ParentPinLength` | `int` | 4 – 8 | 4 |

---

## 12. `DeveloperSettingsSO` — **F1** (spec 16. utolsó bekezdés)

> Build-ben kikapcsolt (`EnableDevTools = false` release-ben, `#if DEVELOPMENT_BUILD` kapu).

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `EnableDevTools` | `bool` | — | true (Editor) |
| `ForceChallengeLevel` | `int` | 0 – 10 (0 = nincs kényszerítés) | 0 |
| `ForceHealth` | `int` | -1 – 999 | -1 |
| `ForceNextProblemType` | `ProblemType?` | enum / null | null |
| `ForceSeed` | `int` | 0 = véletlen | 0 |
| `GodMode` | `bool` | — | false |
| `TimeScale` | `float` | 0.1 – 5.0 | 1.0 |
| `SkipToWaveIndex` | `int` | 0 – 49 | 0 |
| `ShowDebugOverlay` | `bool` | — | false |
| `LogAdaptationDecisions` | `bool` | — | true |

---

## 13. `AudioConfigSO` — **F7**

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `MasterVolume` / `MusicVolume` / `SfxVolume` | `float` | 0.0 – 1.0 | 1.0 / 0.6 / 0.9 |
| `Clips` | `List<AudioClipEntry{ Key:string, Clip:AudioClip }>` | — | — |
| `HapticsIntensity` | `float` | 0.0 – 1.0 | 0.5 |

---

## 14. `StarRatingSO` — **F5** (spec 10., GAME_DESIGN 7.1)

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `Star2MinAccuracy` | `float` | 0.0 – 1.0 | **0.70** |
| `Star3MinAccuracy` | `float` | 0.0 – 1.0 | **0.90** |
| `Star3MaxTimeouts` | `int` | 0 – 10 | **0** |
| `WorldUnlockStarRatio` | `float` | 0.0 – 1.0 | 0.60 |
| `ShardsPerStar` | `int` | 0 – 100 | 5 |
| `SkinStarMilestones` | `List<int>` | 1 – 500 | 10, 25, 50, 100 |

**Validáció:** `Star2MinAccuracy < Star3MinAccuracy`; **nincs** idő-/sebesség-alapú
csillagfeltétel (spec 12.) — ilyen mező felvétele kontraktsértés.

---

## 15. Spec 16. ↔ SO megfeleltetés (teljességi ellenőrzés)

| Spec 16. paraméter | SO | Mező |
|---|---|---|
| Matematikai szintek | `MathDifficultyTableSO` | `Levels[]` |
| Engedélyezett operátorok | `MathDifficultyTableSO`, `ParentSettingsDefaultsSO` | `AllowedOperators` |
| Operandustartományok | `MathDifficultyTableSO` | `Min/MaxOperandValue`, `MaxResultValue` |
| Feladatösszetettség | `MathDifficultyTableSO` | `Min/MaxOperandCount`, `AllowParentheses`, `MaxParenthesisDepth`, `AllowUnknownOperand`, `UnknownOperandCount` |
| Válaszsávok száma | `MathDifficultyTableSO`, `LevelDefinitionSO` | `AnswerOptionCount`, `LaneCount` |
| Gondolkodási idő | `TimePressureTableSO`, `MathEventScheduleEntry` | `ThinkTimeSeconds`, `ThinkTimeSecondsOverride` |
| Ellenségek sebessége | `EnemyDefinitionSO`, `CombatBalanceSO`, `CombatDifficultyTableSO` | `MovementSpeed`, `EnemySpeedMultiplier` |
| Ellenségek életereje | `EnemyDefinitionSO`, `CombatBalanceSO`, `CombatDifficultyTableSO` | `MaxHealth`, `EnemyHealthMultiplier` |
| Ellenséghullámok mérete | `LevelDefinitionSO`, `CombatBalanceSO`, `CombatDifficultyTableSO` | `Spawns[].Count`, `WaveSizeMultiplier`, `EnemiesPerLevel` |
| Fegyvererősség és típusválasztás | `WeaponConfigSO`, `LevelDefinitionSO` | `Damage`, `FireRate`, `AvailableWeapons`, `Weapon` |
| Jutalmak | `RewardTableSO` | aktív, nem-ammo `*RewardBase` |
| Adaptációs küszöbök | `AdaptiveDifficultyConfigSO`, `InLevelAssistSO` | teljes |
| Matematikai feladatok gyakorisága | `MathEventConfigSO`, `TimePressureTableSO` | `MathEventInterval*`, `MinGapBetweenProblemsSeconds` |
| Pályahossz | `LevelDefinitionSO` | `TargetDurationSeconds`, `Waves[]` |
| Főellenségparaméterek | `EnemyDefinitionSO` (Type=Boss, `BossPhaseEntry[]`), `LevelDefinitionSO` | `BossEnemy`, `BossTriggerGameTime` |
| Csillag-/jutalomküszöbök | `StarRatingSO` | teljes |
| Cél-pontossági sáv | `GameBalanceSO` | `TargetAccuracy*`, `GuessingSuccessRateCap` |
| Fejlesztői beállítások | `DeveloperSettingsSO` | teljes |
