# Kontrakt — ScriptableObject konfigurációs séma

> Helye: `Assets/_Project/Configuration/` · Assembly: `MathHorde.Infrastructure`
> (a `ScriptableObject` osztályok Unity-függők, ezért **nem** a Core-ban élnek)
> A `GameConfigurationRepository` minden SO-t **Core DTO-ra fordít**, mielőtt a GameLogic
> megkapja — a GameLogic soha nem lát `ScriptableObject`-et (ARCHITECTURE 8.).
>
> Ez a dokumentum a spec **16. szakaszának MINDEN** kódváltoztatás nélkül módosítható
> paraméterét lefedi. Ami itt nincs, az a kódban sem lehet beégetett „mágikus szám”.

## 0. Konvenciók

| Szabály | Részlet |
|---|---|
| Osztálynév | `<Téma>SO`, fájlnév = osztálynév, `[CreateAssetMenu(menuName = "MathHorde/...")]` |
| Validáció | minden SO implementálja a `IValidatableConfig` interfészt: `IReadOnlyList<string> Validate()`; az `OnValidate()` ezt hívja és Editor-konzolra ír |
| Tartomány | numerikus mezőn `[Range(min,max)]` vagy `[Min(x)]` attribútum — a lenti tartomány KÖTELEZŐ |
| Szöveg | magyar szöveg **soha** nincs SO-ban; csak `...NameKey` string-tábla-kulcs (ADR-007) |
| Azonosító | `string Id` mező, kisbetűs snake_case, assetnévvel egyező |
| Hivatkozás | SO→SO hivatkozás direkt referenciával (nem string-gel), kivéve a runtime-feloldott `PrefabKey` |

**Fázis-jelölés:** F1 = Fázis 1 … F7 = Fázis 7 (spec 19.).

---

## 1. `EnemyDefinitionSO` — **F1** (spec 4.1)

`menuName: "MathHorde/Enemy Definition"`

| Mező | Típus | Tartomány | Default | Megjegyzés |
|---|---|---|---|---|
| `Id` | `string` | nem üres | `"basic_bot"` | egyedi |
| `DisplayNameKey` | `string` | nem üres | `"enemy.basic"` | string-tábla kulcs |
| `Type` | `EnemyType` | enum | `Basic` | Basic/Fast/Armored/Boss |
| `MaxHealth` | `int` | 1 – 10000 | 10 | |
| `MovementSpeed` | `float` | 0.1 – 20.0 | 2.0 | világegység/mp |
| `ContactDamage` | `int` | 0 – 1000 | 10 | |
| `AttackCooldownSeconds` | `float` | 0.0 – 10.0 | 1.0 | |
| `RewardValue` | `int` | 0 – 1000 | 1 | pontszám/drop-súly |
| `Prefab` | `GameObject` | nem null | — | Presentation |
| `TintColor` | `Color` | — | fehér | gyors vizuális megkülönböztetés |
| `ScaleMultiplier` | `float` | 0.2 – 5.0 | 1.0 | |

Validáció: `Type == Boss` ⇒ `MaxHealth >= 100`.

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
| `AmmoPerShot` | `int` | 1 – 20 | 1 |
| `ProjectileSpeed` | `float` | 0 – 200 (0 = hitscan) | 40.0 |
| `TargetingMode` | `TargetingMode` | enum | `OwnLaneThenAdjacent` |
| `AdjacentLanePenalty` | `float` | 0.0 – 1.0 | 0.0 | sebzés-szorzó szomszédos sávra |
| `ProjectilePrefab` | `GameObject` | nem null, ha `ProjectileSpeed > 0` | — |
| `MuzzleEffectKey` | `string` | — | `"vfx.muzzle.default"` |
| `HitEffectKey` | `string` | — | `"vfx.hit.default"` |

---

## 3. `LevelDefinitionSO` — **F1** (bővül F3, F5) (spec 10., 16. „Pályahossz”)

`menuName: "MathHorde/Level Definition"`

| Mező | Típus | Tartomány | Default | Fázis |
|---|---|---|---|---|
| `Id` | `string` | `"w1-l01"` alak | — | F1 |
| `World` | `WorldDefinitionSO` | nem null | — | F5 |
| `DisplayNameKey` | `string` | nem üres | — | F1 |
| `LaneCount` | `int` | **2 – 4** | 3 | F1 |
| `TargetDurationSeconds` | `float` | 60 – 600 | 240 | F1 |
| `Waves` | `List<WaveDefinitionEntry>` | 1 – 50 elem | — | F1 |
| `MathEvents` | `List<MathEventScheduleEntry>` | 0 – 40 (F3-tól **>= 10**) | — | F3 |
| `BossEnemy` | `EnemyDefinitionSO` | lehet null | null | F5 |
| `BossTriggerGameTime` | `float` | 0 – 600 | 200 | F5 |
| `StartingAmmo` | `int` | 0 – 999 | 20 | F1 |
| `MaxAmmo` | `int` | 1 – 999 | 200 | F1 |
| `StartingHealth` | `int` | 1 – 999 | 100 | F1 |
| `MaxHealth` | `int` | 1 – 999 | 100 | F1 |
| `StartingShield` | `int` | 0 – 999 | 0 | F1 |
| `MaxShield` | `int` | 0 – 999 | 50 | F1 |
| `RequiredMathAccuracy` | `float` | 0.0 – 1.0 (0 = nincs) | 0.0 | F3 |
| `Weapon` | `WeaponConfigSO` | nem null | — | F1 |
| `EnvironmentPrefab` | `GameObject` | nem null | — | F1 |

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
| `RewardResource` | `ResourceType` | enum | `Ammo` |
| `Goal` | `GateGoal` | enum | `Maximize` |
| `RewardAmountOverride` | `int` | 0 – 500 (0 = balance-ből) | 0 |

**Validáció:** `MathEvents` időpontjai között legalább `MathEventCooldownSeconds` (ld. 6.)
legyen; `LaneCount` 2 ⇒ `ProblemType` lehet `ResourceOperationComparison`.

---

## 4. `WorldDefinitionSO` — **F5** (spec 10.)

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `Id` | `string` | `"w1"` alak | — |
| `DisplayNameKey` | `string` | nem üres | — |
| `OrderIndex` | `int` | 0 – 50 | 0 |
| `Levels` | `List<LevelDefinitionSO>` | 1 – 30 | — |
| `IntroducedSkills` | `List<SkillTag>` | 0 – 10 | üres |
| `UnlockRequirementLevel` | `LevelDefinitionSO` | lehet null | null |
| `ThemeColor` | `Color` | — | — |

---

## 5. `MathDifficultyTableSO` — **F2** (spec 7.2, 16. „Matematikai szintek”, „Engedélyezett operátorok”, „Operandustartományok”, „Feladatösszetettség”, „Válaszsávok száma”, „Gondolkodási idő”)

> **Kötelezően 10 sor** — a spec 7.2 táblázata nem égethető a kódba.

`menuName: "MathHorde/Math Difficulty Table"` · mező: `List<MathDifficultyLevelEntry> Levels` (pontosan 10 elem)

### `MathDifficultyLevelEntry`

| Mező | Típus | Tartomány | Default (szint 1 → 10) |
|---|---|---|---|
| `Level` | `int` | 1 – 10 | 1..10 |
| `DescriptionKey` | `string` | nem üres | `"diff.l1"` … |
| `AllowedOperators` | `List<BinaryOperator>` (flags-szerű lista) | nem üres | L1: {Add, Sub} → L10: mind |
| `MinOperandValue` | `int` | 0 – 999 | 0 |
| `MaxOperandValue` | `int` | 1 – 999 | L1: 10 → L10: 99 |
| `MinOperandCount` | `int` | 2 – 5 | 2 |
| `MaxOperandCount` | `int` | 2 – 5 | L1: 2 → L10: 5 |
| `AllowParentheses` | `bool` | — | L1–L6: false, L7+: true |
| `AllowUnknownOperand` | `bool` | — | L1–L7: false, L8+: true |
| `AllowNegativeResults` | `bool` | — | false |
| `AnswerOptionCount` | `int` | **2 – 4** | L1–L2: 2, L3–L5: 3, L6+: 4 |
| `BaseThinkTimeSeconds` | `float` | 2.0 – 30.0 | L1: 12 → L10: 6 |
| `RequiredSkills` | `List<SkillTag>` | — | a szint által bevezetett készségek |
| `MathComplexity` | `int` | 1 – 10 | = Level |
| `TimePressure` | `int` | 1 – 10 | = Level |
| `CombatDifficulty` | `int` | 1 – 10 | = Level |

**Validáció:** `MaxOperandValue > MinOperandValue`; `MaxOperandCount >= MinOperandCount`;
`AllowUnknownOperand == true` ⇒ `AllowedOperators` tartalmaz `Addition`-t vagy
`Multiplication`-t; a `Levels` szigorúan monoton `Level` szerint.
A konkrét értékek a `docs/balance/` (balance-designer agent) felelőssége.

---

## 6. `MathEventConfigSO` — **F3** (spec 16. „Matematikai feladatok gyakorisága”, „Jutalmak”)

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `MathEventIntervalSeconds` | `float` | 5 – 120 | 20 |
| `MathEventIntervalJitter` | `float` | 0 – 0.5 | 0.15 |
| `MathEventCooldownSeconds` | `float` | 2 – 60 | 6 |
| `MaxConcurrentMathEvents` | `int` | 1 – 3 | 1 |
| `GateApproachSpeed` | `float` | 0.5 – 20.0 | 4.0 |
| `GateSpawnDepth` | `float` | 5 – 60 | 30 |
| `DecisionZoneDepth` | `float` | 0.5 – 10 | 3.0 |
| `FeedbackDisplaySeconds` | `float` | 0.5 – 5.0 | 1.5 |
| `AmmoRewardBase` | `int` | 1 – 500 | 30 |
| `HealthRewardBase` | `int` | 0 – 200 | 15 |
| `ShieldRewardBase` | `int` | 0 – 200 | 10 |
| `IncorrectRewardAmount` | `int` | 0 – 100 | 0 |
| `RewardScalePerChallengeLevel` | `float` | 0.0 – 0.3 | 0.05 |
| `ResourceGateOperandMin` | `int` | 1 – 100 | 2 |
| `ResourceGateOperandMax` | `int` | 2 – 200 | 50 |
| `ResourceGateMultiplierMin` | `int` | 2 – 5 | 2 |
| `ResourceGateMultiplierMax` | `int` | 2 – 10 | 4 |

---

## 7. `CombatBalanceSO` — **F1** (spec 16. „Ellenségek sebessége/életereje”, „Ellenséghullámok mérete”, „Lőszerfogyás”)

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `EnemySpeedMultiplier` | `float` | 0.5 – 2.0 | 1.0 |
| `EnemyHealthMultiplier` | `float` | 0.5 – 2.0 | 1.0 |
| `EnemyDamageMultiplier` | `float` | 0.5 – 2.0 | 1.0 |
| `WaveSizeMultiplier` | `float` | 0.5 – 3.0 | 1.0 |
| `SpawnRateMultiplier` | `float` | 0.5 – 3.0 | 1.0 |
| `AmmoConsumptionMultiplier` | `float` | 0.5 – 2.0 | 1.0 |
| `MaxWaveGapSeconds` | `float` | 0 – 10 | 2.0 |
| `PlayerLaneChangeSpeed` | `float` | 1 – 30 | 10.0 |
| `EnemyDespawnDepth` | `float` | -20 – 0 | -2.0 |
| `MaxActiveEnemies` | `int` | 10 – 300 | 60 |
| `EnemyPoolSize` | `int` | 10 – 400 | 80 |
| `ProjectilePoolSize` | `int` | 20 – 500 | 120 |
| `PerChallengeLevelSpeedStep` | `float` | 0.0 – 0.2 | 0.05 |
| `PerChallengeLevelHealthStep` | `float` | 0.0 – 0.3 | 0.08 |
| `PerChallengeLevelWaveSizeStep` | `float` | 0.0 – 0.3 | 0.07 |

---

## 8. `AdaptiveDifficultyConfigSO` — **F4** (spec 7.4–7.6, 16. „Adaptációs küszöbök”)

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `TargetAccuracyMin` | `float` | 0.5 – 0.95 | 0.75 |
| `TargetAccuracyMax` | `float` | 0.5 – 0.99 | 0.85 |
| `ShortWindowSize` | `int` | 3 – 10 | 5 |
| `LongWindowSize` | `int` | 10 – 100 | 40 |
| `ConsecutiveErrorsForAssistance` | `int` | 2 – 6 | 3 |
| `ConsecutiveTimeoutsForMoreTime` | `int` | 2 – 6 | 2 |
| `MaxLevelChangePerSession` | `int` | 1 – 2 | 1 |
| `HysteresisBandAccuracy` | `float` | 0.0 – 0.2 | 0.05 |
| `MinSessionsBeforeAdjust` | `int` | 1 – 5 | 1 |
| `ConsecutiveFailuresForDecrease` | `int` | 2 – 5 | 2 |
| `AssistThinkTimeMultiplier` | `float` | 1.0 – 2.0 | 1.4 |
| `AssistGateSpeedMultiplier` | `float` | 0.4 – 1.0 | 0.75 |
| `AssistCombatMultiplier` | `float` | 0.4 – 1.0 | 0.8 |
| `AssistDurationSeconds` | `float` | 5 – 120 | 30 |
| `RescueWaveEnabled` | `bool` | — | true |
| `LowResourceThresholdRatio` | `float` | 0.0 – 0.5 | 0.1 |
| `MinChallengeLevel` | `int` | 1 – 10 | 1 |
| `MaxChallengeLevel` | `int` | 1 – 10 | 10 |

---

## 9. `SkillPrerequisiteSO` — **F4** (spec 7.2, 7.4)

Mező: `List<SkillPrerequisiteEntry> Entries`

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `Skill` | `SkillTag` | enum | — |
| `Prerequisites` | `List<SkillTag>` | 0 – 5 | üres |
| `MasteryThreshold` | `float` | 0.5 – 1.0 | 0.8 |
| `MinAttemptsForMastery` | `int` | 3 – 50 | 8 |
| `ReviewIntervalDays` | `float` | 0.5 – 30 | 3 |

**Validáció:** az előfeltétel-gráf körmentes (DAG).

---

## 10. `ParentSettingsDefaultsSO` — **F6** (spec 11.5)

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
| `BreakSuggestionMinutes` | `int` | 10 – 120 | 30 |
| `ParentPinLength` | `int` | 4 – 8 | 4 |

---

## 11. `DeveloperSettingsSO` — **F1** (spec 16. utolsó bekezdés)

> Build-ben kikapcsolt (`EnableDevTools = false` release-ben, `#if DEVELOPMENT_BUILD` kapu).

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `EnableDevTools` | `bool` | — | true (Editor) |
| `ForceChallengeLevel` | `int` | 0 – 10 (0 = nincs kényszerítés) | 0 |
| `ForceAmmo` | `int` | -1 – 999 (-1 = nincs) | -1 |
| `ForceHealth` | `int` | -1 – 999 | -1 |
| `ForceNextProblemType` | `ProblemType?` | enum / null | null |
| `ForceSeed` | `int` | 0 = véletlen | 0 |
| `GodMode` | `bool` | — | false |
| `InfiniteAmmo` | `bool` | — | false |
| `TimeScale` | `float` | 0.1 – 5.0 | 1.0 |
| `SkipToWaveIndex` | `int` | 0 – 49 | 0 |
| `ShowDebugOverlay` | `bool` | — | false |
| `LogAdaptationDecisions` | `bool` | — | true |

---

## 12. `AudioConfigSO` — **F7**

| Mező | Típus | Tartomány | Default |
|---|---|---|---|
| `MasterVolume` / `MusicVolume` / `SfxVolume` | `float` | 0.0 – 1.0 | 1.0 / 0.6 / 0.9 |
| `Clips` | `List<AudioClipEntry{ Key:string, Clip:AudioClip }>` | — | — |
| `HapticsIntensity` | `float` | 0.0 – 1.0 | 0.5 |

---

## 13. Spec 16. ↔ SO megfeleltetés (teljességi ellenőrzés)

| Spec 16. paraméter | SO | Mező |
|---|---|---|
| Matematikai szintek | `MathDifficultyTableSO` | `Levels[]` |
| Engedélyezett operátorok | `MathDifficultyTableSO`, `ParentSettingsDefaultsSO` | `AllowedOperators` |
| Operandustartományok | `MathDifficultyTableSO` | `Min/MaxOperandValue` |
| Feladatösszetettség | `MathDifficultyTableSO` | `Min/MaxOperandCount`, `AllowParentheses`, `AllowUnknownOperand` |
| Válaszsávok száma | `MathDifficultyTableSO`, `LevelDefinitionSO` | `AnswerOptionCount`, `LaneCount` |
| Gondolkodási idő | `MathDifficultyTableSO`, `MathEventScheduleEntry` | `BaseThinkTimeSeconds`, `ThinkTimeSecondsOverride` |
| Ellenségek sebessége | `EnemyDefinitionSO`, `CombatBalanceSO` | `MovementSpeed`, `EnemySpeedMultiplier` |
| Ellenségek életereje | `EnemyDefinitionSO`, `CombatBalanceSO` | `MaxHealth`, `EnemyHealthMultiplier` |
| Ellenséghullámok mérete | `LevelDefinitionSO`, `CombatBalanceSO` | `Spawns[].Count`, `WaveSizeMultiplier` |
| Lőszerfogyás | `WeaponConfigSO`, `CombatBalanceSO` | `AmmoPerShot`, `AmmoConsumptionMultiplier` |
| Jutalmak | `MathEventConfigSO` | `*RewardBase`, `RewardScalePerChallengeLevel` |
| Adaptációs küszöbök | `AdaptiveDifficultyConfigSO` | teljes |
| Matematikai feladatok gyakorisága | `MathEventConfigSO` | `MathEventInterval*`, `MaxConcurrentMathEvents` |
| Pályahossz | `LevelDefinitionSO` | `TargetDurationSeconds`, `Waves[]` |
| Főellenségparaméterek | `EnemyDefinitionSO` (Type=Boss), `LevelDefinitionSO` | `BossEnemy`, `BossTriggerGameTime` |
| Fejlesztői beállítások | `DeveloperSettingsSO` | teljes |
