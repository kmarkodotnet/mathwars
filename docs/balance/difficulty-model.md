# Dinamikus nehézségi rendszer (DDA) — számszerű modell

> Státusz: ARCH fázis, v1.1 · Felelős: game-designer agent
> Forrás: spec 7. szakasz (570–727. sor), 8. szakasz (729–783), 9. szakasz (786–826).
> Ez a dokumentum **kiegészíti**, nem írja felül a `docs/contracts/scriptable-object-schema.md`-t.
> **Minden szám alapértelmezés, ScriptableObject-ből felülírható (spec 7.2: „A táblázat
> konfigurálható legyen, ne kerüljön beégetésre a kódba").**
>
> **v1.1 (2026-11-16, architect-konszolidáció, ADR-009):** minden ScriptableObject- és
> típusnév a kontrakt kanonikus nevére cserélve (`scriptable-object-schema.md` 0.1
> névmegfeleltetés); `displayNameHu` → `DescriptionKey` (ADR-007); a vereség-okok a
> kontrakt `FailureReason` / `LossDiagnosis` felbontására hozva; az `Introduced`/`Practiced`
> fokozatok a `SkillMasteryLevel` enumra hivatkoznak (ADR-010).

---

## 1. Fogalmi szétválasztás

A projektben **négy, egymástól független** nehézségi fogalom létezik. Ezek összekeverése a
leggyakoribb tervezési hiba, ezért itt rögzítjük:

| Fogalom | Hatókör | Változik? | Tároló |
|---|---|---|---|
| **Játékszintű alap-szigor** | az egész termék „alap-szigora", a cél-pontossági sáv | **Nem** változik játék közben; csak fejlesztői/szülői beállítás | `GameBalanceSO` |
| `ChallengeLevel` (**globális, 1–10**) | pályák között adaptálódik, HUD-on látható | pályánként ±1 | `PlayerProfile.CurrentChallengeLevel` |
| **In-level assist tier** (**rövid táv**) | 1 pályán belül, 3–5 válasz ablaka | gyorsan, de csak lefelé | futásidejű `InLevelAssistance` (nem perzisztált) |
| `SkillProfile` (**tudásprofil**) | készségenkénti tudás, 30–50 válasz ablaka | lassan, mindkét irányban | `PlayerProgress.Skills` |

A `ChallengeLevel` és a `SkillProfile` **külön adat** (spec 7.4): a globális szint emelése
soha nem kényszeríthet a gyerekre még nem tanult műveletet.

### 1.1. A játékszintű cél-paraméter (spec 7.6, 718–719. sor)

```
TargetAccuracyMin      = 0.75     // e fölött a gyerek jellemzően teljesíti a pályát
TargetAccuracyMax      = 0.85     // e fölött a rendszer nehezít
TargetSuccessRate      = 0.70     // a célsávban játszó gyerek pályáinak kb. ennyi %-a sikeres
GuessingSuccessRateCap = 0.15     // a tisztán tippelő játékos pályateljesítési aránya (felső korlát)
```

Indoklás: a 75–85% a spec explicit célsávja. A 70%-os célzott pályasiker abból jön, hogy
10-ből 7 sikeres kísérlet még „rendszeres sikerélményt" ad (spec 23/7.), de a 3 kudarc
fenntartja a tétet és működteti a 7.4. szerinti szintcsökkentést. A tippelő felső korlát
a spec 9.1-ből ered: „ne legyen rendszeresen teljesíthető találgatással".

**ScriptableObject:** `GameBalanceSO` (egyetlen példány, `Resources/Config/`) —
scriptable-object-schema.md 10.

---

## 2. Az 1–10 kihívásszint matematikai tartalma (MathComplexity)

A spec 7.2. táblázatának konkretizálása. `opCount` = operandusok száma.

| CL | Megnevezés (`DescriptionKey`) | Megengedett operátorok | Operandus-tartomány | opCount | Zárójel (`MaxParenthesisDepth`) | Ismeretlen operandus | Sávszám (`AnswerOptionCount`) |
|---|---|---|---|---|---|---|---|
| 1 | `diff.l1` — Összeadás/kivonás 10-ig | `+`, `−` | 1–10, eredmény 0–10 | 2 | 0 | nem | 2 |
| 2 | `diff.l2` — Összeadás/kivonás 20-ig | `+`, `−` | 1–20, tízesátlépés engedve | 2 | 0 | nem | 2 |
| 3 | `diff.l3` — Szorzótábla-bevezetés | `+`, `−`, `×`, `÷` | `×`/`÷`: 2,3,5,10-es tábla, tényező ≤10; `+`/`−`: ≤20 | 2 | 0 | nem | 3 |
| 4 | `diff.l4` — Teljes szorzótábla | `+`, `−`, `×`, `÷` | `×`: 1–10 × 1–10; `÷`: osztható, osztó ≤10; `+`/`−`: ≤50 | 2 | 0 | nem | 3 |
| 5 | `diff.l5` — Nagyobb műveletek, maradék | `+`, `−`, `×`, `÷`, `mod`, `divrem` | `+`/`−`: ≤100 kölcsönzéssel; `mod`: osztandó ≤60, osztó ≤9 | 2 | 0 | nem | 3 |
| 6 | `diff.l6` — Kétjegyű × egyjegyű | előzők + `×` kiterjesztve | `×`: 11–30 × 2–9; `÷`: osztandó ≤100 | 2 (ritkán 3) | 0 | nem | 4 |
| 7 | `diff.l7` — Vegyes műveletek, egyszerű zárójel | mind | tagok ≤50, végeredmény ≤200 | 3 | **1** | nem | 4 |
| 8 | `diff.l8` — Hiányzó operandus, egyenletek | mind | tagok ≤60; `a + ? = b × c` alak | 3 | 1 | **igen**, 1 db (`RightOperand`) | 4 |
| 9 | `diff.l9` — Kétjegyű szorzás, többlépéses | mind | `×`: 11–25 × 11–19; tagok ≤99 | 3–4 | 1 | igen, 1 db | 4 |
| 10 | `diff.l10` — Összetett, több operandusos | mind | tagok ≤99, végeredmény ≤999 | 4–5 | **2**, beágyazva | igen, 1 db `EitherSide` | 4 |

> A „Megnevezés" oszlop **string-tábla-kulcs**, nem magyar szöveg az assetben (ADR-007);
> a megjelenő magyar címke a `hu.json`-ban él.

**A skála meredeksége (spec 8., 781–782. sor):** a nehézség nem exponenciálisan nő. A CL1→CL5
szakasz tartalmi bővülés (új operátorok), a CL6→CL10 szakasz elsősorban **szerkezeti**
bonyolódás (operandusszám, zárójel, ismeretlen) és időnyomás. A CL10 az, ahol „nagyon gyors
és ügyes számolás" kell — ott a gondolkodási idő 4,5 s egy 4 operandusú, zárójeles kifejezésre.

**Készség-előfeltétel-kapu (spec 7.2, 619–621. sor):** a generátor minden feladatjelölthöz
ellenőrzi, hogy annak `RequiredSkills` listájára
`SkillProfile.GetMasteryLevel(tag) >= SkillMasteryLevel.Introduced` teljesül-e (ADR-010).
Ha nem, a feladat **elvetésre kerül és a CL ugyanazon szintjéről
másik, engedélyezett készségű feladat generálódik** (`SKILL_NOT_UNLOCKED` validátor-kód).
A CL soha nem „nyit ki" új készséget automatikusan — az új készséget a világ-progresszió
vezeti be (`WorldDefinition.IntroducedSkills`, lásd `worlds.md`).

**ScriptableObject:** `MathDifficultyTableSO` → `MathDifficultyLevelEntry` × 10
(scriptable-object-schema.md 5.). Mezők: `Level`, `DescriptionKey`, `AllowedOperators[]`,
`MinOperandValue`/`MaxOperandValue`, `MaxResultValue`, `MinOperandCount`/`MaxOperandCount`,
`AllowParentheses`, `MaxParenthesisDepth`, `AllowUnknownOperand`, `UnknownOperandCount`,
`UnknownPlacement`, `AnswerOptionCount`, `LaneCount`, `MinGateGap`, `RequiredSkills[]`.

A `MinGateGap` szintenként: **CL1–3: 8**, **CL4–10: 4** (GAME_DESIGN.md 3.5/1.).

---

## 3. TimePressure szintenként

| CL | `ThinkTimeSeconds` (sáv-feladat) | `GateApproachSeconds` (kapu megjelenéstől a döntési vonalig) | `AnswerSpawnDelaySeconds` | `GateApproachSpeed` (egység/s) | `MinGapBetweenProblemsSeconds` |
|---|---|---|---|---|---|
| 1 | 14.0 | 12.0 | 2.5 | 1.2 | 14 |
| 2 | 13.0 | 11.0 | 2.5 | 1.3 | 13 |
| 3 | 11.5 | 10.0 | 2.0 | 1.5 | 12 |
| 4 | 10.0 | 9.0 | 2.0 | 1.7 | 11 |
| 5 | 8.0 | 8.0 | 1.5 | 1.9 | 10 |
| 6 | 7.5 | 7.5 | 1.5 | 2.1 | 10 |
| 7 | 7.0 | 7.0 | 1.5 | 2.3 | 9 |
| 8 | 6.5 | 6.5 | 1.2 | 2.6 | 9 |
| 9 | 5.5 | 6.0 | 1.0 | 2.9 | 8 |
| 10 | 4.5 | 5.0 | 1.0 | 3.3 | 8 |

**Indoklás:**
- A CL5-ös 8,0 s pontosan a spec 5.2. példájának idővonala (0 s feladat → 2 s válaszok → 10 s
  döntés), kissé tömörítve, mert a 1,5 s-os `AnswerSpawnDelaySeconds` korábban adja a sávokat.
- A legalacsonyabb érték **4,5 s**, nem kevesebb: 7 éves gyereknél 4 s alatt a motoros
  sávváltás ideje (≈0,8 s) már a gondolkodási idő jelentős részét elvenné, és a feladat
  reakcióidő-teszté, nem számolássá válna (spec 12: „a gyorsaság csak a pontosság mellett").
  A kontrakt ezt `[Range(4.0, 30.0)]`-tal kényszeríti.
- A `MinGapBetweenProblemsSeconds` biztosítja a GAME_DESIGN.md 2.2. szabályát (min. 8 s tiszta
  harc); a kontrakt alsó korlátja 8,0.
- `timePressureScale` globális szorzó (default 1.0, tartomány 0,70–1,60) minden fenti időre —
  ez a DDA TimePressure-dimenziójának külön állítható tengelye (spec 7.3, 649. sor: „Ezeket
  külön-külön lehessen állítani"). Ha a gyerek **jól számol, de lassan**, csak ez a szorzó nő
  1.15 lépcsőkben (`AdaptiveDifficultyConfigSO.TimePressureScaleStep`), a MathComplexity
  érintetlen marad (spec 7.3, 651. sor). A szorzó **futásidejű állapot**, nem SO-mező.

**ScriptableObject:** `TimePressureTableSO` → `TimePressureLevelEntry` × 10
(scriptable-object-schema.md 5.1). A `MathDifficultyTableSO.BaseThinkTimeSeconds` ennek a
táblának a tükörértéke; eltérés esetén a `Validate()` hibát jelez.

---

## 4. CombatDifficulty szintenként

Referencia: 240 s-os pálya, 8 hullám, a fegyver alap-DPS-e 4 lövés/s × 1 sebzés.

| CL | Hullámok | Ellenség / pálya | `EnemySpeedMultiplier` | `EnemyHealthMultiplier` | `EnemyDamageMultiplier` | `PeakSpawnRatePerSecond` | `ArmoredRatio` | `SwarmletRatio` |
|---|---|---|---|---|---|---|---|---|
| 1 | 5 | 55 | 0.70 | 0.70 | 0.60 | 0.6 | 0% | 0% |
| 2 | 6 | 65 | 0.78 | 0.78 | 0.68 | 0.7 | 0% | 10% |
| 3 | 6 | 78 | 0.85 | 0.85 | 0.76 | 0.8 | 5% | 15% |
| 4 | 7 | 92 | 0.92 | 0.92 | 0.84 | 0.9 | 8% | 18% |
| 5 | 8 | 110 | 1.00 | 1.00 | 1.00 | 1.0 | 12% | 20% |
| 6 | 8 | 126 | 1.08 | 1.10 | 1.08 | 1.15 | 15% | 22% |
| 7 | 9 | 142 | 1.16 | 1.20 | 1.16 | 1.30 | 18% | 24% |
| 8 | 9 | 160 | 1.24 | 1.32 | 1.24 | 1.45 | 22% | 25% |
| 9 | 10 | 180 | 1.33 | 1.45 | 1.33 | 1.60 | 25% | 26% |
| 10 | 10 | 205 | 1.42 | 1.60 | 1.42 | 1.80 | 28% | 28% |

**Indoklás:** az ellenségszám CL1→CL10 között **3,7×**, a HP-szorzó 2,3×, tehát a teljes
„lelőnivaló HP" kb. 8,5×-ére nő — ez a lineárisnál meredekebb, de messze nem exponenciális
(spec 8., 782. sor). A sebességszorzó szándékosan a legszelídebb tengely (max 1,42×), mert a
túl gyors ellenség elveszi a gondolkodási időt, és ezzel a MathComplexity helyett a reakcióidőt
tesztelné — a két dimenziót tisztán kell tartani (spec 7.3).

**`SwarmletRatio` és a világok (fontos tartalmi kapu):** a `Swarmlet` archetípus a `worlds.md`
szerint a **W3**-ban jelenik meg, miközben ez a tábla már CL2-től számol rajzó aránnyal.
A feloldás: a tábla a **kihívásszint** tengelye, a világ a **tartalom** tengelye, és ütközés
esetén a **világ szűkebb halmaza nyer** (worlds.md 1.). Ha a pálya világa
`IntroducedEnemyTypes`-ban nem tartalmazza a `Swarmlet`-et, az arány `Basic`/`Fast` ellenségre
esik vissza (scriptable-object-schema.md 7.1 validáció). Ugyanez áll az `ArmoredRatio`-ra
(`Armored` a W2-ben jelenik meg).

**ScriptableObject:** `CombatDifficultyTableSO` (scriptable-object-schema.md 7.1); az
ellenségek alapértékei külön `EnemyDefinitionSO` assetekben (lásd GAME_DESIGN.md 5. pont),
a tábla csak szorzókat és arányokat ad. A `CombatBalanceSO.PerChallengeLevel*Step` mezői
ezért alapértelmezésben **0** — két párhuzamos CL-skálázás duplán nehezítene.

---

## 5. A lőszer-költségvetési egyenlet (spec 9.1)

Ez a balancing **központi invariánsa**. A `gameplay-dev` ebből származtassa a jutalmakat,
ne egymástól függetlenül állítsa be őket (spec 9.1 utolsó mondata: „Az ellenségek számát és a
jutalmakat ne véletlenszerűen, egymástól függetlenül állítsd be").

```
AmmoDemand      = Σ(enemyHP) × OverkillFactor            // OverkillFactor = 1.15
AmmoSupply(p)   = StartingAmmo + KillAmmoRefund×enemyCount + p × MathRewardPerfect
RequiredAccuracy = (AmmoDemand − StartingAmmo − KillAmmoRefund×enemyCount) / MathRewardPerfect
```

### 5.1. CL5 referencia-pálya levezetése

| Tétel | Számítás | Érték |
|---|---|---|
| Ellenségek összes HP-ja | 110 ellenség, átlag 4,0 HP | 440 |
| Boss | | 120 |
| Overkill (szórás, túlsebzés, elhibázott lövés) | (440+120) × 1,15 | **644 lövés = 644 lőszer** |
| Kezdő lőszer (`LevelDefinitionSO.StartingAmmo`) | | −60 |
| Kill-refund (`EnemyDefinitionSO.RewardValue` = +1 lőszer / kill) | 110 | −110 |
| **Mateknak fedeznie kell** | | **474** |
| Tökéletes matek-hozam (6 kapu × 60 + 8 sáv × 38) | 360 + 304 | **664** |
| **RequiredAccuracy** | 474 / 664 | **0,714** |

> A kill-refund a kontraktban `EnemyDefinitionSO.RewardValue` (**lőszer**, nem pontszám) ×
> `AmmoBudgetSO.KillAmmoRefund`. A harci (nem matek) hozam felső korlátja a tökéletes
> matek-hozam 15%-a (`AmmoBudgetSO.CombatRewardShareMax`) — itt 110/664 = 16,6%, ami
> **a határon van**: a `SIM-plan.md` futásánál vagy a `RewardValue` csökken 0,9-re
> (effektíve 99 lőszer), vagy a `MathRewardPerfect` emelkedik. A szabály iránya kötött:
> **a matek-hozam nem csökkenhet** a harci hozam javára (spec 23/2.).

### 5.2. Ebből következő profil-kimenetek (a `SIM-plan.md` validálja)

| Pontosság | Matek-hozam | Egyenleg (±) | Várt kimenet |
|---|---|---|---|
| 50% | 332 | **−142** | Kifogy a lőszer a 6. hullámnál → pályavesztés tipikus (`LossDiagnosis = AmmoStarvation` → `FailureReason = HealthDepleted`). Megfelel a „ne legyen tippeléssel teljesíthető" elvnek. |
| 70% | 465 | −9 | Borotvaélen; a segítő mentőhullám (6.5/T3) dönt. Pályasiker ≈ 45%. |
| 75% | 498 | +24 | Jellemzően sikeres, szűk tartalékkal. |
| 85% | 564 | +90 | Kényelmesen sikeres, de a boss még kihívás. |
| 95% | 631 | +157 | Sikeres + a felesleg pontszámmá válik (F5), ★3 elérhető. |

**Ez teljesíti a spec 718. sorát:** a 75–85%-os sávban a játékos általában sikeres, de a kihívás
(boss-fázisok, páncélosok) megmarad. A `RequiredAccuracy = 0,714` szándékosan a célsáv alsó
pereme alatt van ~4 százalékponttal, hogy a hullám-RNG-ből adódó szórás ne buktasson el egy
75%-os gyereket.

> **Figyelem a `LevelDefinition.RequiredMathAccuracy`-val való összekeverésre:** az itteni
> `RequiredAccuracy` egy **számított balance-metrika** (mennyi pontosság kell a lőszer
> kijöveteléhez), a `LevelDefinition.RequiredMathAccuracy` ezzel szemben egy **győzelmi
> feltétel** (spec 1.2), alapértéke 0.0 = nincs matek-minimum. A kettő nem azonos és nem
> egymásból származik.

**ScriptableObject:** `AmmoBudgetSO` (`OverkillFactor`, `KillAmmoRefund`,
`CombatRewardShareMax`, `TargetRequiredAccuracyMin/Max`, `OverflowScorePerAmmo`) +
`RewardTableSO` (CL-enkénti jutalmak) — scriptable-object-schema.md 6.1, 6.2.
A `StartingAmmo` és a `MaxAmmo` (= `ammoSoftCap`) **pályánkénti** adat:
`LevelDefinitionSO`.

### 5.3. Jutalomértékek CL-enként

A jutalmak a CL-lel együtt skálázódnak, mert a `AmmoDemand` is nő. Az arány tartása a fontos,
nem az abszolút szám.

| CL | `GateRewardBase` | `LaneRewardBase` | `MathDecisionCount` / pálya | `MathRewardPerfect` | `RequiredAccuracy` (cél: 0,68–0,74) |
|---|---|---|---|---|---|
| 1 | 30 | 20 | 10 (4 kapu + 6 sáv) | 240 | 0,69 |
| 2 | 34 | 22 | 11 | 282 | 0,70 |
| 3 | 40 | 26 | 12 | 350 | 0,71 |
| 4 | 50 | 32 | 13 | 474 | 0,71 |
| 5 | 60 | 38 | 14 | 664 | 0,714 |
| 6 | 70 | 44 | 14 | 772 | 0,72 |
| 7 | 82 | 52 | 15 | 904 | 0,72 |
| 8 | 96 | 60 | 15 | 1056 | 0,73 |
| 9 | 112 | 70 | 16 | 1232 | 0,735 |
| 10 | 130 | 82 | 16 | 1420 | 0,74 |

A `RequiredAccuracy` CL10-ig csak 0,69→0,74-re nő: a magasabb szinten **nem a lőszerhiánnyal**
nehezítünk (az frusztráló tehetetlenséget okoz), hanem a matematikai és időbeli komplexitással.

**ScriptableObject:** `RewardTableSO` (scriptable-object-schema.md 6.1).
A `MathDecisionCount` oszlop a `LevelDefinitionSO.MathEvents` elemszámának **célértéke**;
a kemény alsó korlát a spec 10.1 szerinti 10 (`Tutorial` sablonnál 8).

---

## 6. Adaptáció

### 6.1. Pályák közötti (spec 7.4, kötelező szabályok)

| Esemény | `ChallengeLevel` változás |
|---|---|
| Pálya teljesítve **első** próbálkozásra | **+1** |
| Pálya teljesítve **második** próbálkozásra | **0** |
| Pálya teljesítve harmadik vagy későbbi próbálkozásra | 0 |
| Ugyanazon pálya **kétszer egymás után** sikertelen | **−1** |
| További **két** egymást követő sikertelenség | ismét **−1** |
| Bármikor | clamp(1, 10) |

A próbálkozásszámot **pályánként** tartjuk nyilván a kontrakt
`LevelCompletionRecord.Attempts` / `.ConsecutiveFailures` mezőiben (core-domain.md 6.;
korábbi, hibás munkanév: `LevelAttemptRecord`), és a számláló a pálya sikeres
teljesítésekor nullázódik.

### 6.2. Dimenzió-választó szabály (spec 7.3, 651–655. sor)

A `−1` és `+1` nem vakon a globális CL-t módosítja: a `AdaptiveDifficultyService` a kudarc
okából választja ki a **dimenziót**, amelyen a fő elmozdulás történik.

A kudarc oka két, egymástól független adat (core-domain.md 6.):
`FailureReason` = a **játékszabály** szerinti ok (zárt lista: `HealthDepleted`, `TimeLimit`,
`PlayerQuit`, `MathMinimumNotMet`), `LossDiagnosis` = a **mért** diagnózis
(`AmmoStarvation`, `EnemyBreakthrough`, `MathAccuracyShortfall`, `BossPhaseStall`).
A lőszerhiány és az ellenség-áttörés **nem** `FailureReason`, mert a lőszer kifogyása
önmagában nem vereség (spec 1.3) — a vereség oka ilyenkor is `HealthDepleted`.

| Diagnózis (a hosszú távú ablak alapján) | Elsődleges beavatkozás | Másodlagos |
|---|---|---|
| Pontosság < 65%, átlagos válaszidő a keret 60%-a alatt (gyorsan hibázik) | `MathComplexity −1` | – |
| Pontosság ≥ 80%, de timeout-arány > 20% (jól számol, lassan) | `timePressureScale × 1.15` | MathComplexity változatlan |
| Pontosság ≥ 80%, `LossDiagnosis == EnemyBreakthrough` | `CombatDifficulty −1` | MathComplexity változatlan |
| Pontosság ≥ 80%, `LossDiagnosis == AmmoStarvation` | `GateRewardBase`/`LaneRewardBase` × 1.10 | jelzés a balance-logba (modellhiba gyanú) |
| Pontosság ≥ 80%, `LossDiagnosis == BossPhaseStall` | `CombatDifficulty −1` és a boss-fázis gondolkodási ideje × 1.25 | – |
| Pontosság ≥ 88% **és** siker első próbálkozásra **és** timeout < 5% | `MathComplexity +1` | `CombatDifficulty +1` ha a HP > 60% volt a végén |

A HUD-on látható 1–10 szint a három dimenzió **súlyozott átlaga**:
`displayedCL = round(0.5×mathCL + 0.2×timeCL + 0.3×combatCL)`
(`AdaptiveDifficultyConfigSO.DisplayWeightMath/Time/Combat`, összegük pontosan 1.0).
A matek kapja a legnagyobb súlyt, mert ez az oktatási termék gerince.

### 6.3. Hiszterézis és maximális változási sebesség (spec 7.6, 723. sor)

| Paraméter (`AdaptiveDifficultyConfigSO`) | Érték | Indoklás |
|---|---|---|
| `MaxLevelChangePerSession` | ±1 | a spec explicit szabálya |
| `MinDecisionsBeforeChange` | 30 matek-válasz | a hosszú távú ablak alsó határa (spec 708. sor) |
| `DirectionChangeCooldownLevels` | 2 pálya | emelés után legalább 2 pálya, mielőtt csökkentés jöhetne (és fordítva) — ez a hiszterézis magja, megakadályozza az oszcillációt |
| `AccuracyDeadbandLow` | 0.65 | ez alatt csökkentünk |
| `AccuracyDeadbandHigh` | 0.88 | e fölött emelünk; a 0,65–0,88 **holtsáv**, benne nincs változás |
| `MaxDimensionDeltaPerSession` | ±2 | egy ülésen (max. 4 pálya) belül egyetlen dimenzió sem mozdulhat 2-nél többet |
| `TimePressureScaleMin/Max` | 0.70 – 1.60 | a 10 fokozat időértékein felüli finomhangolás tartománya |

A 0,65 / 0,88 holtsáv szélesebb, mint a 0,75–0,85-ös célsáv: ez biztosítja, hogy a célsávban
**semmi nem történik**, és a rendszer csak akkor avatkozik be, ha a játékos tartósan kilóg.
A kontrakt invariánsa:
`AccuracyDeadbandLow < TargetAccuracyMin <= TargetAccuracyMax < AccuracyDeadbandHigh`.

### 6.4. Rövid (3–5 válasz) és hosszú (30–50 válasz) ablak (spec 7.6, 708–709. sor)

| Ablak | Méret | Mit vezérel | Irány |
|---|---|---|---|
| Rövid (`ShortWindowSize`) | utolsó **5** válasz (min. 3 kell a kiértékeléshez) | **pályán belüli segítség** (6.5), és csak az új feladatokat/hullámokat érinti | **csak könnyítés**; legfeljebb a pálya alap-CL-jéhez képest **−1 szintnyi** eltérés (T3-nál −2) |
| Hosszú (`LongWindowSize`) | utolsó **40** válasz (tartomány 30–50) | a `ChallengeLevel` pályák közötti módosítása és a következő pálya kezdő szintje | mindkét irány, ±1 |

A rövid ablak **sosem nehezít** menet közben — a pályán belüli nehezítés azt az érzetet keltené,
hogy a játékot „büntetik" a jó teljesítményért. A hosszú ablak tartja fenn a valódi progressziót.
A kontrakt ezt kikényszeríti: az `InLevelAssistSO` minden szorzó-mezője `<= 1.0`
(a `ThinkTimeMultiplier` kivételével, ami `>= 1.0`), nehezítő érték validációs hiba.

---

### 6.5. Pályán belüli segítség — küszöbök és enyhítések (spec 7.5)

A kiváltók folyamatosan monitorozódnak. A legmagasabb aktív tier érvényesül. Minden enyhítés
**kizárólag az ezután létrejövő** feladatokra és hullámokra hat (spec 698. sor), a már
képernyőn lévő feladvány megoldása nem változik (`IMathEventDirector.ApplyTimePressure`
szerződése: „a MÁR AKTÍV események paraméterei nem változnak").

| Tier | Kiváltó (`TriggerType` / `TriggerThreshold`) | Enyhítés | Időtartam |
|---|---|---|---|
| **T1 — Finom** | `ConsecutiveErrors` = 2 (vagy 2 timeout az utolsó 5-ben) | `ThinkTimeMultiplier` 1.25; `GateSpeedMultiplier` 0.85 | 2 feladat |
| **T2 — Érezhető** | `ConsecutiveErrors` = **3** (a spec explicit küszöbe, 677. sor) | T1 + `MathComplexityDelta = −1`; `LaneCountOverride = 3` | 3 feladat, vagy amíg 2 egymást követő helyes nem jön |
| **T3 — Mentőhullám** | `AmmoBelowRatio` = 0.20 (a hátralévő `AmmoDemand` 20%-a — **kritikus erőforráshiány**) | Rövid, könnyebb **mentő-matek-hullám**: 2 sáv-feladat `MathComplexityDelta = −2` szinten, `LaneCountOverride = 2`, dupla jutalommal (`RewardTableSO.RescueWaveRewardMultiplier = 2.0`); egyidejűleg `SpawnDensityMultiplier = 0.70` | 1 mentőhullám, pályánként max. **2** (`MaxActivationsPerLevel = 2`) |
| **T4 — Harci enyhítés** | `HealthDropRate` = 0.30 (HP a max 30%-a alá esik 20 s-on belül) | `EnemySpeedMultiplier` 0.85, `SpawnDensityMultiplier` 0.75, `EnemyDamageMultiplier` 0.80 | 25 s (`DurationSeconds`) |

**Visszaállás (fontos, nehogy a segítség beragadjon):** minden tier kikapcsol, ha a játékos
**2 egymást követő helyes választ** ad (`RecoveryCorrectAnswers = 2`); a visszaállás fokozatos
(T3 → T1 → ki), tier-enként legalább 1 feladatnyi késleltetéssel, hogy ne legyen hirtelen
nehezedés.

**Mit NEM csinál a segítség (spec 692–700. sor):**
- Nem ad ingyen lőszert és nem old meg feladatot a játékos helyett.
- Nem garantál pályateljesítést: a T3 mentőhullám jutalma csak **helyes** válasz esetén jár, és
  maximum 2×, ami a hiányzó lőszer ~35%-át fedezi — a maradékot a gyereknek kell megoldania.
- Nem állítja le a harcot és a matekot (spec 696. sor).
- Nem módosít visszamenőleg már megjelent feladványt.

**ScriptableObject:** `InLevelAssistSO` → `AssistTierEntry` × 4
(scriptable-object-schema.md 8.1).

---

## 7. Naplózás (spec 7.6, 725. sor: „Minden adaptációs döntés legyen belső naplóban visszakövethető")

Minden adaptációs döntés egy `DifficultyAdjustment` rekordot ír (core-domain.md 6.):
`PreviousLevel`, `NewLevel`, `Reason` (gépi kód, **angol** — ADR-007), `Timestamp`, és a
`TriggerMetrics` szótárban: `windowType` (short/long), `windowAccuracy`,
`windowMedianResponseMs`, `timeoutRatio`, `failureReason`, `lossDiagnosis`,
`dimensionChanged`, `suppressedByHysteresis`, `levelId`, `attemptIndex`.
A napló lokálisan tárolódik és a szülői felületen megtekinthető (spec 11.), külső szerverre
nem megy (spec 12.). Kapcsoló: `DeveloperSettingsSO.LogAdaptationDecisions`.

---

## 8. ScriptableObject-térkép összefoglalva (a `gameplay-dev` / `math-engine-dev` számára)

> **Kanonikus asset-nevek** (scriptable-object-schema.md 0.1). A régebbi `*Config` végű
> munkanevek (`ChallengeLevelTableConfig`, `TimePressureTableConfig`, …) elavultak.

| Config asset | Tartalom | Szakasz | Fázis |
|---|---|---|---|
| `GameBalanceSO` | `TargetAccuracyMin/Max`, `TargetSuccessRate`, `GuessingSuccessRateCap`, `ReferenceDps` | 1.1 | F1 |
| `MathDifficultyTableSO` | 10 × `MathDifficultyLevelEntry` (matematikai tartalom) | 2 | F2 |
| `TimePressureTableSO` | 10 × `TimePressureLevelEntry` | 3 | F2 |
| `CombatDifficultyTableSO` | 10 × `CombatDifficultyLevelEntry` | 4 | F1 |
| `AmmoBudgetSO` | költségvetési egyenlet együtthatói | 5 | F3 |
| `RewardTableSO` | 10 × (`GateRewardBase`, `LaneRewardBase`, `MathDecisionCount`) | 5.3 | F3 |
| `AdaptiveDifficultyConfigSO` | pályák közötti szabályok, dimenzió-választó mátrix, hiszterézis | 6.1–6.4 | F4 |
| `InLevelAssistSO` | 4 × `AssistTierEntry` | 6.5 | F4 |
| `StarRatingSO` | csillag- és feloldási küszöbök | GAME_DESIGN 7.1 | F5 |

Ezek pontos C# mezőnevei és típusai a `docs/contracts/scriptable-object-schema.md`-ben
rögzítettek — ott az architect agent a felelős, ez a dokumentum az **értékeket** adja hozzá.
Ütközés esetén a kontrakt az irányadó (ADR-009).
