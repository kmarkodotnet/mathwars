# Dinamikus nehézségi rendszer (DDA) — számszerű modell

> Státusz: ARCH fázis, v1.0 · Felelős: game-designer agent
> Forrás: spec 7. szakasz (570–727. sor), 8. szakasz (729–783), 9. szakasz (786–826).
> Ez a dokumentum **kiegészíti**, nem írja felül a `docs/contracts/scriptable-object-schema.md`-t.
> **Minden szám alapértelmezés, ScriptableObject-ből felülírható (spec 7.2: „A táblázat
> konfigurálható legyen, ne kerüljön beégetésre a kódba").**

---

## 1. Fogalmi szétválasztás

A projektben **négy, egymástól független** nehézségi fogalom létezik. Ezek összekeverése a
leggyakoribb tervezési hiba, ezért itt rögzítjük:

| Fogalom | Hatókör | Változik? | Tároló |
|---|---|---|---|
| `GameLevelDifficulty` (**játékszintű**) | az egész termék „alap-szigora", a cél-pontossági sáv | **Nem** változik játék közben; csak fejlesztői/szülői beállítás | `GameBalanceConfig` |
| `ChallengeLevel` (**globális, 1–10**) | pályák között adaptálódik, HUD-on látható | pályánként ±1 | `PlayerProfile.challengeLevel` |
| `In-level assist tier` (**rövid táv**) | 1 pályán belül, 3–5 válasz ablaka | gyorsan, de csak lefelé | futásidejű állapot |
| `SkillProfile` (**tudásprofil**) | készségenkénti tudás, 30–50 válasz ablaka | lassan, mindkét irányban | `PlayerProfile.skills` |

A `ChallengeLevel` és a `SkillProfile` **külön adat** (spec 7.4): a globális szint emelése
soha nem kényszeríthet a gyerekre még nem tanult műveletet.

### 1.1. A játékszintű cél-paraméter (spec 7.6, 718–719. sor)

```
targetAccuracyMin      = 0.75     // e fölött a gyerek jellemzően teljesíti a pályát
targetAccuracyMax      = 0.85     // e fölött a rendszer nehezít
targetSuccessRate      = 0.70     // a célsávban játszó gyerek pályáinak kb. ennyi %-a sikeres
guessingSuccessRate    ≤ 0.15     // a tisztán tippelő játékos pályateljesítési aránya (felső korlát)
```

Indoklás: a 75–85% a spec explicit célsávja. A 70%-os célzott pályasiker abból jön, hogy
10-ből 7 sikeres kísérlet még „rendszeres sikerélményt" ad (spec 23/7.), de a 3 kudarc
fenntartja a tétet és működteti a 7.4. szerinti szintcsökkentést. A tippelő felső korlát
a spec 9.1-ből ered: „ne legyen rendszeresen teljesíthető találgatással".

**ScriptableObject:** `GameBalanceConfig` (egyetlen példány, `Resources/Config/`).

---

## 2. Az 1–10 kihívásszint matematikai tartalma (MathComplexity)

A spec 7.2. táblázatának konkretizálása. `opCount` = operandusok száma.

| CL | Megnevezés | Megengedett operátorok | Operandus-tartomány | opCount | Zárójel | Ismeretlen operandus | Sávszám |
|---|---|---|---|---|---|---|---|
| 1 | Összeadás/kivonás 10-ig | `+`, `−` | 1–10, eredmény 0–10 | 2 | nem | nem | 2 |
| 2 | Összeadás/kivonás 20-ig | `+`, `−` | 1–20, tízesátlépés engedve | 2 | nem | nem | 2 |
| 3 | Szorzótábla-bevezetés | `+`, `−`, `×`, `÷` | `×`/`÷`: 2,3,5,10-es tábla, tényező ≤10; `+`/`−`: ≤20 | 2 | nem | nem | 3 |
| 4 | Teljes szorzótábla | `+`, `−`, `×`, `÷` | `×`: 1–10 × 1–10; `÷`: osztható, osztó ≤10; `+`/`−`: ≤50 | 2 | nem | nem | 3 |
| 5 | Nagyobb műveletek, maradék | `+`, `−`, `×`, `÷`, `mod`, `divrem` | `+`/`−`: ≤100 kölcsönzéssel; `mod`: osztandó ≤60, osztó ≤9 | 2 | nem | nem | 3 |
| 6 | Kétjegyű × egyjegyű | előzők + `×` kiterjesztve | `×`: 11–30 × 2–9; `÷`: osztandó ≤100 | 2 (ritkán 3) | nem | nem | 4 |
| 7 | Vegyes műveletek, egyszerű zárójel | mind | tagok ≤50, végeredmény ≤200 | 3 | **igen**, max 1 szint | nem | 4 |
| 8 | Hiányzó operandus, egyenletek | mind | tagok ≤60; `a + ? = b × c` alak | 3 | igen, max 1 szint | **igen**, 1 db | 4 |
| 9 | Kétjegyű szorzás, többlépéses | mind | `×`: 11–25 × 11–19; tagok ≤99 | 3–4 | igen, max 1 szint | igen, 1 db | 4 |
| 10 | Összetett, több operandusos | mind | tagok ≤99, végeredmény ≤999 | 4–5 | igen, **max 2 szint**, beágyazva | igen, 1 db **bármelyik oldalon** | 4 |

**A skála meredeksége (spec 8., 781–782. sor):** a nehézség nem exponenciálisan nő. A CL1→CL5
szakasz tartalmi bővülés (új operátorok), a CL6→CL10 szakasz elsősorban **szerkezeti**
bonyolódás (operandusszám, zárójel, ismeretlen) és időnyomás. A CL10 az, ahol „nagyon gyors
és ügyes számolás" kell — ott a `thinkTimeSec` 4,5 s egy 4 operandusú, zárójeles kifejezésre.

**Készség-előfeltétel-kapu (spec 7.2, 619–621. sor):** a generátor minden feladatjelölthöz
ellenőrzi, hogy annak `requiredSkills` listája szerepel-e a `SkillProfile`-ban legalább
`Introduced` állapotban. Ha nem, a feladat **elvetésre kerül és a CL ugyanazon szintjéről
másik, engedélyezett készségű feladat generálódik**. A CL soha nem „nyit ki" új készséget
automatikusan — az új készséget a világ-progresszió vezeti be (lásd `worlds.md`).

**ScriptableObject:** `ChallengeLevelTableConfig` → `ChallengeLevelDefinition[10]`, mezők:
`level`, `displayNameHu`, `allowedOperators[]`, `operandRanges[]` (operátoronként min/max),
`operandCountMin/Max`, `maxParenthesisDepth`, `unknownOperandCount`, `laneCount`,
`minGateGap`, `requiredSkillTags[]`.

---

## 3. TimePressure szintenként

| CL | `thinkTimeSec` (sáv-feladat) | `gateApproachSec` (kapu megjelenéstől a döntési vonalig) | `answerSpawnDelay` | Kapu-sebesség (egység/s) | `minGapBetweenProblemsSec` |
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
  döntés), kissé tömörítve, mert a 1,5 s-os `answerSpawnDelay` korábban adja a sávokat.
- A legalacsonyabb érték **4,5 s**, nem kevesebb: 7 éves gyereknél 4 s alatt a motoros
  sávváltás ideje (≈0,8 s) már a gondolkodási idő jelentős részét elvenné, és a feladat
  reakcióidő-teszté, nem számolássá válna (spec 12: „a gyorsaság csak a pontosság mellett").
- A `minGapBetweenProblemsSec` biztosítja a GAME_DESIGN.md 2.2. szabályát (min. 8 s tiszta harc).
- `timePressureScale` globális szorzó (default 1.0) minden fenti időre — ez a DDA
  TimePressure-dimenziójának külön állítható tengelye (spec 7.3, 649. sor: „Ezeket külön-külön
  lehessen állítani"). Ha a gyerek **jól számol, de lassan**, csak ez a szorzó nő 1.15 / 1.30 /
  1.50 lépcsőkben, a MathComplexity érintetlen marad (spec 7.3, 651. sor).

**ScriptableObject:** `TimePressureTableConfig` + futásidejű `timePressureScale` a
`AdaptiveDifficultyState`-ben.

---

## 4. CombatDifficulty szintenként

Referencia: 240 s-os pálya, 8 hullám, a fegyver alap-DPS-e 4 lövés/s × 1 sebzés.

| CL | Hullámok | Ellenség / pálya | `enemySpeedMult` | `enemyHpMult` | `enemyDamageMult` | Spawn-sűrűség (e/s csúcson) | Páncélos arány | Rajzó arány |
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

**ScriptableObject:** `CombatDifficultyTableConfig`; az ellenségek alapértékei külön
`EnemyDefinition` assetekben (lásd GAME_DESIGN.md 5. pont), a tábla csak szorzókat ad.

---

## 5. A lőszer-költségvetési egyenlet (spec 9.1)

Ez a balancing **központi invariánsa**. A `gameplay-dev` ebből származtassa a jutalmakat,
ne egymástól függetlenül állítsa be őket (spec 9.1 utolsó mondata: „Az ellenségek számát és a
jutalmakat ne véletlenszerűen, egymástól függetlenül állítsd be").

```
AmmoDemand      = Σ(enemyHP) × overkillFactor            // overkillFactor = 1.15
AmmoSupply(p)   = startingAmmo + killRefund×enemyCount + p × MathRewardPerfect
RequiredAccuracy = (AmmoDemand − startingAmmo − killRefund×enemyCount) / MathRewardPerfect
```

### 5.1. CL5 referencia-pálya levezetése

| Tétel | Számítás | Érték |
|---|---|---|
| Ellenségek összes HP-ja | 110 ellenség, átlag 4,0 HP | 440 |
| Boss | | 120 |
| Overkill (szórás, túlsebzés, elhibázott lövés) | (440+120) × 1,15 | **644 lövés = 644 lőszer** |
| Kezdő lőszer | | −60 |
| Kill-refund (+1 / kill) | 110 | −110 |
| **Mateknak fedeznie kell** | | **474** |
| Tökéletes matek-hozam (6 kapu × 60 + 8 sáv × 38) | 360 + 304 | **664** |
| **RequiredAccuracy** | 474 / 664 | **0,714** |

### 5.2. Ebből következő profil-kimenetek (a `SIM-plan.md` validálja)

| Pontosság | Matek-hozam | Egyenleg (±) | Várt kimenet |
|---|---|---|---|
| 50% | 332 | **−142** | Kifogy a lőszer a 6. hullámnál → pályavesztés tipikus. Megfelel a „ne legyen tippeléssel teljesíthető" elvnek. |
| 70% | 465 | −9 | Borotvaélen; a segítő mentőhullám (6.2) dönt. Pályasiker ≈ 45%. |
| 75% | 498 | +24 | Jellemzően sikeres, szűk tartalékkal. |
| 85% | 564 | +90 | Kényelmesen sikeres, de a boss még kihívás. |
| 95% | 631 | +157 | Sikeres + a felesleg pontszámmá válik, ★3 elérhető. |

**Ez teljesíti a spec 718. sorát:** a 75–85%-os sávban a játékos általában sikeres, de a kihívás
(boss-fázisok, páncélosok) megmarad. A `RequiredAccuracy = 0,714` szándékosan a célsáv alsó
pereme alatt van ~4 százalékponttal, hogy a hullám-RNG-ből adódó szórás ne buktasson el egy
75%-os gyereket.

**ScriptableObject:** `LevelAmmoBudgetConfig` (`overkillFactor`, `killRefund`, `startingAmmo`,
`ammoSoftCap`, `gateRewardBase`, `laneRewardBase`), `RewardTableConfig` (CL-enkénti jutalmak).

### 5.3. Jutalomértékek CL-enként

A jutalmak a CL-lel együtt skálázódnak, mert a `AmmoDemand` is nő. Az arány tartása a fontos,
nem az abszolút szám.

| CL | `gateRewardBase` | `laneRewardBase` | Matek-döntés / pálya | `MathRewardPerfect` | `RequiredAccuracy` (cél: 0,68–0,74) |
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

A próbálkozásszámot **pályánként** tartjuk nyilván (`LevelAttemptRecord`), és a számláló a
pálya sikeres teljesítésekor nullázódik.

### 6.2. Dimenzió-választó szabály (spec 7.3, 651–655. sor)

A `−1` és `+1` nem vakon a globális CL-t módosítja: a `AdaptiveDifficultyService` a kudarc
okából választja ki a **dimenziót**, amelyen a fő elmozdulás történik.

| Diagnózis (a hosszú távú ablak alapján) | Elsődleges beavatkozás | Másodlagos |
|---|---|---|
| Pontosság < 65%, átlagos válaszidő a keret 60%-a alatt (gyorsan hibázik) | `MathComplexity −1` | – |
| Pontosság ≥ 80%, de timeout-arány > 20% (jól számol, lassan) | `timePressureScale × 1.15` | MathComplexity változatlan |
| Pontosság ≥ 80%, vereség oka `HealthDepleted` vagy `EnemyBreakthrough` | `CombatDifficulty −1` | MathComplexity változatlan |
| Pontosság ≥ 80%, vereség oka `AmmoStarvation` | `gateReward/laneReward × 1.10` | jelzés a balance-logba (modellhiba gyanú) |
| Pontosság ≥ 88% **és** siker első próbálkozásra **és** timeout < 5% | `MathComplexity +1` | `CombatDifficulty +1` ha a HP > 60% volt a végén |

A HUD-on látható 1–10 szint a három dimenzió **súlyozott átlaga**:
`displayedCL = round(0.5×mathCL + 0.2×timeCL + 0.3×combatCL)`. A matek kapja a legnagyobb súlyt,
mert ez az oktatási termék gerince.

### 6.3. Hiszterézis és maximális változási sebesség (spec 7.6, 723. sor)

| Paraméter | Érték | Indoklás |
|---|---|---|
| `maxLevelChangePerLevel` | ±1 | a spec explicit szabálya |
| `minDecisionsBeforeChange` | 30 matek-válasz | a hosszú távú ablak alsó határa (spec 708. sor) |
| `directionChangeCooldownLevels` | 2 pálya | emelés után legalább 2 pálya, mielőtt csökkentés jöhetne (és fordítva) — ez a hiszterézis magja, megakadályozza az oszcillációt |
| `accuracyDeadbandLow` | 0.65 | ez alatt csökkentünk |
| `accuracyDeadbandHigh` | 0.88 | e fölött emelünk; a 0,65–0,88 **holtsáv**, benne nincs változás |
| `maxDimensionDeltaPerSession` | ±2 | egy ülésen (max. 4 pálya) belül egyetlen dimenzió sem mozdulhat 2-nél többet |
| `timePressureScaleRange` | 0.70 – 1.60 | a 10 fokozat időértékein felüli finomhangolás tartománya |

A 0,65 / 0,88 holtsáv szélesebb, mint a 0,75–0,85-ös célsáv: ez biztosítja, hogy a célsávban
**semmi nem történik**, és a rendszer csak akkor avatkozik be, ha a játékos tartósan kilóg.

### 6.4. Rövid (3–5 válasz) és hosszú (30–50 válasz) ablak (spec 7.6, 708–709. sor)

| Ablak | Méret | Mit vezérel | Irány |
|---|---|---|---|
| Rövid (`shortWindow`) | utolsó **5** válasz (min. 3 kell a kiértékeléshez) | **pályán belüli segítség** (6.5), és csak az új feladatokat/hullámokat érinti | **csak könnyítés**; legfeljebb a pálya alap-CL-jéhez képest **−1 szintnyi** eltérés |
| Hosszú (`longWindow`) | utolsó **40** válasz (tartomány 30–50) | a `ChallengeLevel` pályák közötti módosítása és a következő pálya kezdő szintje | mindkét irány, ±1 |

A rövid ablak **sosem nehezít** menet közben — a pályán belüli nehezítés azt az érzetet keltené,
hogy a játékot „büntetik" a jó teljesítményért. A hosszú ablak tartja fenn a valódi progressziót.

---

### 6.5. Pályán belüli segítség — küszöbök és enyhítések (spec 7.5)

A kiváltók folyamatosan monitorozódnak. A legmagasabb aktív tier érvényesül. Minden enyhítés
**kizárólag az ezután létrejövő** feladatokra és hullámokra hat (spec 698. sor), a már
képernyőn lévő feladvány megoldása nem változik.

| Tier | Kiváltó | Enyhítés | Időtartam |
|---|---|---|---|
| **T1 — Finom** | 2 egymást követő hibás válasz **vagy** 2 timeout az utolsó 5-ben | `thinkTimeSec × 1.25`; kapu-közeledési sebesség × 0,85 | 2 feladat |
| **T2 — Érezhető** | **3 egymást követő hibás válasz** (a spec explicit küszöbe, 677. sor) **vagy** T1 alatt újabb 2 hiba | T1 + egyszerűbb számértékek: a MathComplexity az új feladatokra `−1` szint; sávszám 4→3 | 3 feladat, vagy amíg 2 egymást követő helyes nem jön |
| **T3 — Mentőhullám** | lőszer < a `AmmoDemand` hátralévő részének 20%-a (**kritikus erőforráshiány**) | Rövid, könnyebb **mentő-matek-hullám**: 2 sáv-feladat `MathComplexity −2` szinten, 2 sávval, dupla jutalommal (`laneReward × 2`); ezzel egyidejűleg a spawn-sűrűség × 0,7 | 1 mentőhullám, pályánként max. **2** alkalommal |
| **T4 — Harci enyhítés** | HP a maximum 30%-a alá esik 20 s-on belül (**nagyon gyors életerővesztés**) | `enemySpeedMult × 0.85`, spawn-sűrűség × 0,75, `enemyDamageMult × 0.8` | 25 s |

**Visszaállás (fontos, nehogy a segítség beragadjon):** minden tier kikapcsol, ha a játékos
**2 egymást követő helyes választ** ad; a visszaállás fokozatos (T3 → T1 → ki), tier-enként
legalább 1 feladatnyi késleltetéssel, hogy ne legyen hirtelen nehezedés.

**Mit NEM csinál a segítség (spec 692–700. sor):**
- Nem ad ingyen lőszert és nem old meg feladatot a játékos helyett.
- Nem garantál pályateljesítést: a T3 mentőhullám jutalma csak **helyes** válasz esetén jár, és
  maximum 2×, ami a hiányzó lőszer ~35%-át fedezi — a maradékot a gyereknek kell megoldania.
- Nem állítja le a harcot és a matekot (spec 696. sor).
- Nem módosít visszamenőleg már megjelent feladványt.

**ScriptableObject:** `InLevelAssistConfig` → `AssistTierDefinition[4]`, mezők: `tier`,
`triggerType` (enum: `ConsecutiveErrors`, `TimeoutsInWindow`, `AmmoBelowRatio`, `HealthDropRate`),
`triggerThreshold`, `thinkTimeMultiplier`, `gateSpeedMultiplier`, `mathComplexityDelta`,
`laneCountOverride`, `spawnDensityMultiplier`, `enemySpeedMultiplier`, `enemyDamageMultiplier`,
`rescueWaveEnabled`, `rescueWaveRewardMultiplier`, `maxActivationsPerLevel`,
`durationProblems` / `durationSeconds`, `recoveryCorrectAnswers`.

---

## 7. Naplózás (spec 7.6, 725. sor: „Minden adaptációs döntés legyen belső naplóban visszakövethető")

Minden adaptációs döntés egy `DifficultyDecisionLogEntry` rekordot ír:
`timestamp`, `trigger`, `windowType` (short/long), `windowAccuracy`, `windowMedianResponseMs`,
`timeoutRatio`, `lossReason`, `dimensionChanged`, `oldValue`, `newValue`, `suppressedByHysteresis`
(bool + ok), `levelId`, `attemptIndex`. A napló lokálisan tárolódik és a szülői felületen
megtekinthető (spec 11.), külső szerverre nem megy (spec 12.).

---

## 8. ScriptableObject-térkép összefoglalva (a `gameplay-dev` / `math-engine-dev` számára)

| Config asset | Tartalom | Szakasz |
|---|---|---|
| `GameBalanceConfig` | `targetAccuracyMin/Max`, `targetSuccessRate`, `guessingSuccessRateCap` | 1.1 |
| `ChallengeLevelTableConfig` | 10 × `ChallengeLevelDefinition` (matematikai tartalom) | 2 |
| `TimePressureTableConfig` | 10 × `TimePressureDefinition` | 3 |
| `CombatDifficultyTableConfig` | 10 × `CombatDifficultyDefinition` | 4 |
| `LevelAmmoBudgetConfig` | költségvetési egyenlet együtthatói | 5 |
| `RewardTableConfig` | 10 × (`gateRewardBase`, `laneRewardBase`, `decisionCount`) | 5.3 |
| `AdaptationRulesConfig` | pályák közötti szabályok, dimenzió-választó mátrix, hiszterézis | 6.1–6.4 |
| `InLevelAssistConfig` | 4 × `AssistTierDefinition` | 6.5 |

Ezek pontos C# mezőnevei és típusai a `docs/contracts/scriptable-object-schema.md`-ben
rögzítendők — ott az architect agent a felelős, ez a dokumentum az **értékeket** adja hozzá.
