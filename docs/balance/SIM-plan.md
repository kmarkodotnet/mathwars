# Balancing-szimuláció — terv (Fázis 4 bemenete)

> Státusz: ARCH fázis, v1.0 · Felelős: game-designer agent
> Forrás: spec 9.1 (804–825. sor: „Készíts szimulációt eltérő matematikai pontosságú virtuális
> játékosokkal"), 7.6, 10.1.
> **Ez a terv. A futtatás és az eredmények dokumentálása a Fázis 4 feladata**, kimenet:
> `docs/balance/SIM-<pálya>.md` fájlonként egy pálya-konfigurációra.

---

## 1. A szimuláció célja

Validálni a `difficulty-model.md` 5. pontjában felírt lőszer-költségvetési egyenletet **valós
időbeli lefutáson**, nem csak zárt képlettel. A zárt képlet átlagot ad; a szimuláció a
**szórást** és az időzítést méri — azaz azt, hogy a lőszerhiány *mikor* lép fel, és kimenthető-e.

Három kérdésre kell választ adnia:
1. A 75–85%-os pontosságú játékos ténylegesen teljesíti-e a pályák ~70%-át? (spec 718. sor)
2. A tippelő (25%) játékos ténylegesen elbukik-e? (spec 806. sor)
3. Elég-e, de nem túl sok-e a pályán belüli segítség? (spec 700. sor: „ne jelentse azt, hogy a
   játékos végülis bármilyen gyenge is, végig tudja vinni a pályát")

---

## 2. Architektúra-követelmények (a `math-engine-dev` és `gameplay-dev` számára)

A szimulátor **Unity-mentes, headless .NET konzolprojekt** (`MathHorde.Simulation`), amely a
Core/Domain rétegre hivatkozik. Ez a spec 13. és 23/9. alapelvéből következik (a motor legyen
független a megjelenítéstől, és más játékmódokhoz is felhasználható).

| Követelmény | Indok |
|---|---|
| Fix időlépés, `dt = 0.05 s` (20 Hz) | 240 s = 4800 lépés/futás; elég finom a spawn- és lövésütemhez, gyors |
| Determinisztikus, seed-elt RNG; a seed logolva | spec 6.4: reprodukálhatóság |
| Nincs Unity API-hívás a szimulált kódúton | headless CI-futtathatóság |
| Futásidő | 10 000 futás < 60 s egy fejlesztői gépen |
| Kimenet | CSV (nyers futások) + a generált `SIM-<pálya>.md` összefoglaló |

---

## 3. Virtuális játékos-profilok

Minden profil = (matematikai pontosság, válaszidő-eloszlás, viselkedési modell).

| Profil | `mathAccuracy` | Válaszidő (a `thinkTimeSec` arányában) | Timeout-hajlam | Mozgáspontosság* | Leírás |
|---|---|---|---|---|---|
| `P-Guess` | 1/laneCount (0,25–0,50) | egyenletes 0,2–0,9 | 2% | 0,95 | Tippel; a kapuknál véletlen oldal. Kontroll-profil. |
| `P-Struggling` | 0,50 | N(0,75; 0,15) | 12% | 0,85 | Nehezen boldogul, gyakran kifut az időből |
| `P-Developing` | 0,70 | N(0,65; 0,15) | 7% | 0,90 | A célsáv alatt |
| `P-Target-Low` | 0,75 | N(0,60; 0,12) | 5% | 0,92 | **A célsáv alsó pereme** |
| `P-Target-High` | 0,85 | N(0,50; 0,12) | 3% | 0,94 | **A célsáv felső pereme** |
| `P-Strong` | 0,95 | N(0,35; 0,10) | 1% | 0,96 | Mesterszint |
| `P-SlowButAccurate` | 0,90 | N(0,98; 0,10) | **25%** | 0,90 | Jól számol, **lassan** — a spec 651. sorának tesztesete |
| `P-FastButSloppy` | 0,55 | N(0,30; 0,10) | 1% | 0,96 | Gyorsan hibázik — a 7.3. diagnózis másik ága |
| `P-CombatWeak` | 0,85 | N(0,50; 0,12) | 3% | **0,70** | Jól számol, de rosszul kerülget — `CombatDifficulty` tesztesete |

\* `Mozgáspontosság` = annak valószínűsége, hogy a szimulált játékos a megfelelő sávban áll a
döntési vonalnál, feltéve hogy tudja a helyes választ; a maradék esetekben „elkési" a sávváltást.
A harci modellben ugyanez a paraméter szabályozza az átjutás-elkerülést.

**A pontosság nem konstans:** a profil `mathAccuracy`-ja a feladat MathComplexity-jéhez képest
korrigálódik: `p_eff = clamp(mathAccuracy − 0.04 × (problemCL − playerCL), 0.10, 0.99)`.
Így a szimuláció a DDA-t is teszteli, nem csak egy fix pályát.

---

## 4. Mért metrikák

### 4.1. Futásonként (CSV-sor)

| Metrika | Definíció |
|---|---|
| `completed` | bool — a boss legyőzve, HP > 0 |
| `failReason` | `HealthDepleted` / `AmmoStarvation` / `TimeExpired` / `None` |
| `ammoSpentTotal`, `ammoSpentPerWave[]` | **várható lőszerfogyás hullámonként** (spec 812. sor) |
| `mathRewardTotal`, `mathRewardMissed` | **várható matematikai jutalom** (spec 813. sor) |
| `dryTimeSec`, `dryEpisodes` | mennyi ideig / hányszor volt a lőszer 0 — **lőszerhiány** (spec 814. sor) |
| `minAmmo`, `ammoAtWaveStart[]` | tartaléksáv alakulása |
| `damageTaken`, `breakthroughCount` | **várható sebzés** (spec 815. sor) |
| `accuracyActual`, `timeoutCount` | tényleges teljesítmény |
| `assistActivations[tier]` | **mentőfeladatok rendelkezésre állása/használata** (spec 818. sor) |
| `challengeLevelStart/End` | DDA-elmozdulás több pályás futásnál |
| `durationSec` | **pályahossz** (spec 817. sor) |

### 4.2. Profilonként aggregálva

`completionRate`, `medianDryTime`, `P(dryTime > 10 s)`, `medianMinAmmo`,
`requiredAccuracyEmpirical` (a `completionRate = 0,5`-hez tartozó pontosság, binárisan keresve),
`assistRate`, `medianDamageTaken`.

---

## 5. Futtatási mátrix

| Dimenzió | Értékek | Darab |
|---|---|---|
| Profil | a 9 profil | 9 |
| ChallengeLevel | 1, 3, 5, 7, 10 | 5 |
| Pályasablon | `Standard`, `BossLevel` | 2 |
| Segítség | `assistEnabled = true` / `false` | 2 |
| Seed | 500 ismétlés | 500 |

**Összesen 9 × 5 × 2 × 2 × 500 = 90 000 futás.** A `assistEnabled = false` ág méri a „nyers"
egyensúlyt, a `true` ág a segítség hatását — a kettő különbsége a segítség **ereje**, amit a
7. pont korlátoz.

---

## 6. Elfogadási kritériumok (mikor jó az egyensúly)

A Fázis 4 szimuláció akkor zöld, ha **minden** alábbi feltétel teljesül a `assistEnabled = true`,
`Standard` sablon, CL3/5/7 átlagán:

| # | Kritérium | Küszöb | Forrás |
|---|---|---|---|
| B1 | `P-Target-Low` (75%) `completionRate` | **0,60 – 0,80** | spec 718. sor |
| B2 | `P-Target-High` (85%) `completionRate` | **0,80 – 0,95** | spec 718. sor |
| B3 | `P-Guess` `completionRate` | **≤ 0,15** | spec 806. sor („ne legyen teljesíthető találgatással") |
| B4 | `P-Strong` (95%) `completionRate` | **≥ 0,95**, de `medianMinAmmo` **> 0** csak 0,90 arányban — azaz még neki is legyen szűk pillanata | spec 808. sor |
| B5 | `P-Struggling` (50%) `completionRate` | **0,15 – 0,35** | spec 700. sor: a segítség ne tegye mindenki számára teljesíthetővé |
| B6 | `requiredAccuracyEmpirical` | **0,66 – 0,76** | `difficulty-model.md` 5.2 (elméleti 0,714) |
| B7 | A segítség hatása: `completionRate(assist=true) − completionRate(assist=false)` a `P-Struggling`-nál | **+0,08 – +0,20** | érezhető, de nem dönti el a pályát |
| B8 | `P-SlowButAccurate` fő `failReason`-je | `AmmoStarvation` **timeout miatt**, és a `timePressureScale × 1,3` mellett a `completionRate` **≥ +0,25**-tel javul | spec 651. sor: a lassúságra idő a válasz |
| B9 | `P-CombatWeak` fő `failReason`-je | `HealthDepleted`, és `CombatDifficulty −1` mellett `completionRate` **≥ +0,20** | spec 655. sor |
| B10 | Hullámonkénti lőszerfogyás monotonitása | egyetlen hullám se fogyasszon többet, mint a pálya teljes fogyásának **22%-a** | nehogy egy hullám önmagában eldöntse a pályát |
| B11 | `P(dryTime > 10 s)` a `P-Target-High`-nál | **≤ 0,10** | a jól számoló gyerek ne álljon tehetetlenül |
| B12 | `assistActivations[T3]` a `P-Target-High`-nál | átlag **≤ 0,5 / pálya** | a célsávban a mentőhullám kivétel legyen, ne rutin |

**Ha egy kritérium bukik, a javítás sorrendje** (hogy ne egymást rontó paraméterek mozogjanak):
1. `RewardTableConfig` jutalomértékei (a `RequiredAccuracy`-t közvetlenül hangolják)
2. `LevelAmmoBudgetConfig.overkillFactor` / `killRefund`
3. `CombatDifficultyTableConfig` ellenségszám és HP
4. `InLevelAssistConfig` küszöbök
5. `TimePressureTableConfig` — **utolsóként**, mert ez befolyásolja a mért pontosságot is,
   így körkörös visszacsatolást okoz.

---

## 7. Többpályás (progressziós) szimuláció

A fenti egypályás futások mellett egy **10 pályás sorozatot** is futtatunk profilonként, a
DDA-val együtt, 200 seeddel. Ez validálja a `difficulty-model.md` 6. pontját:

| # | Kritérium | Küszöb |
|---|---|---|
| D1 | `P-Target-High` végső `ChallengeLevel`-je 10 pálya után | emelkedik, **+2 … +4** |
| D2 | `P-Struggling` végső `ChallengeLevel`-je | csökken, de **≥ 1** és nem ragad be 1-en (stabilizálódjon 1–3 között) |
| D3 | Oszcilláció: irányváltások száma 10 pálya alatt bármely profilnál | **≤ 2** (hiszterézis működik) |
| D4 | Bármely profil pályánkénti `completionRate` szórása a 10 pálya alatt | csökkenő trend (a DDA konvergál) |
| D5 | `P-SlowButAccurate`-nél a `MathComplexity` | **nem csökken** (csak a `timePressureScale` nő) — a dimenzió-szétválasztás bizonyítéka |

---

## 8. Kimeneti formátum: `docs/balance/SIM-<pálya>.md`

Minden szimulált pálya-konfigurációra (pl. `SIM-w1-l05.md`, `SIM-w5-boss.md`) egy fájl,
kötelező szakaszokkal:

1. **Bemenet** — a használt config-assetek és verziójuk, a seed-tartomány, futásszám, dátum.
2. **Profil-összefoglaló táblázat** — profilonként: `completionRate`, `failReason` megoszlás,
   `medianMinAmmo`, `dryTime`, `damageTaken`, `assistActivations`.
3. **Hullámonkénti lőszermérleg** — táblázat: hullám, várható fogyás, várható matek-jutalom,
   záró lőszer mediánja és 10/90 percentilise. (spec 812–814. sor)
4. **Lőszerhiány-elemzés** — `P(dryTime > 0)` és `P(dryTime > 10 s)` profilonként, és a
   leggyakoribb kiváltó hullám.
5. **`requiredAccuracyEmpirical`** — mért érték vs. az elméleti 0,714, és az eltérés magyarázata.
6. **Elfogadási kritériumok kiértékelése** — B1–B12 és D1–D5 pipa/bukás táblázatban.
7. **Javasolt paraméter-módosítások** — konkrét asset + mező + régi → új érték, a 6. pont
   javítási sorrendjét követve.
8. **Nyers adat** — a CSV relatív elérési útja.

---

## 9. Nem célja a szimulációnak

- Nem helyettesíti a valódi gyerek-playtestet. A szimulált `mathAccuracy` fix paraméter;
  valós gyereknél a pontosság a fáradtságtól, a feladattípustól és a stressztől is függ.
- Nem hangolja a játékot „manipulatívan" nyerésre: a cél az egyensúly feltérképezése, nem egy
  rejtett „gumifal", amely garantálja a sikert (spec 721. sor: „Ez tervezési cél, nem garantált
  vagy manipulált eredmény").
- Nem méri a tanulási hatékonyságot — azt a `SkillProfile` valós adatai és a szülői kimutatás
  mutatják meg (spec 8., 11.).

---

## 10. Tennivalók a Fázis 4-hez (checklist)

- [ ] `MathHorde.Simulation` headless projekt felállítása a Core/Domain referenciájával
- [ ] `VirtualPlayerProfile` ScriptableObject/POCO + a 9 profil `SimulationProfileSet`-ként
- [ ] 20 Hz-es fix lépésű harci és matek-óra, seed-elt RNG
- [ ] CSV-író + `SIM-<pálya>.md` generátor
- [ ] A 90 000 futásos mátrix lefuttatása CL1/3/5/7/10-re
- [ ] A 10 pályás progressziós futás profilonként
- [ ] B1–B12 és D1–D5 kiértékelése, paraméterjavítás, újrafuttatás a konvergenciáig
- [ ] A végleges értékek visszaírása a `*Config` assetekbe és a `difficulty-model.md` 3–5.
      táblázataiba (a dokumentum és az asset nem térhet el egymástól)
