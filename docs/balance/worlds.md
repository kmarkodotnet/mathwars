# Világok és pályák — tartalmi terv

> Státusz: ARCH fázis, v1.0 · Felelős: game-designer agent
> Forrás: spec 10. szakasz (828–885. sor), 7.2. (matematikai tartalom), 8. (készségprofil).
> A pálya tartalma **konfigurációból épül fel** (spec 848. sor) — lásd 6. pont.

---

## 1. A legfontosabb szabály: világ ≠ kihívásszint

> „A világok és a dinamikus nehézségi szint két külön fogalom. Egy gyermek lehet a harmadik
> világban 4-es kihívásszinten, egy másik ugyanott 7-esen." (spec 844–846. sor)

| | Világ (`WorldId`) | Kihívásszint (`ChallengeLevel` 1–10) |
|---|---|---|
| Mit határoz meg? | **Milyen** matematikai készségek és ellenségtípusok jelennek meg egyáltalán (tartalmi kapu) | **Milyen nehezen** jelennek meg: számtartomány, operandusszám, idő, harci intenzitás |
| Mi változtatja? | Pályateljesítés → feloldási feltétel | `AdaptiveDifficultyService` (lásd `difficulty-model.md` 6.) |
| Irány | csak előre | fel és le |
| Látható? | világtérképen | HUD-sáv felül, számmal |

**Gyakorlati következmény a `gameplay-dev` számára:** a pálya generálásakor a
`WorldDefinition.enabledSkills` és a `ChallengeLevelDefinition` **metszete** adja a megengedett
feladathalmazt. Ha a metszet üres (pl. CL8 „ismeretlen operandus"-t kérne, de a W2 azt még nem
engedélyezi), akkor a **világ szűkebb halmaza nyer**, és a CL a többi tengelyen (idő, harc,
számtartomány, operandusszám) érvényesül. Ez a spec 621. sorának közvetlen megvalósítása.

**Példa (a spec idézett esete):** W3 „Szorzók barlangja" mindkét gyereknél szorzótáblát ad.
A 4-es szintű gyerek 2,3,5,10-es táblát kap 11,5 s gondolkodási idővel és 78 ellenséggel;
a 7-es szintű gyerek vegyes, zárójeles szorzásos kifejezést 7,0 s-mal és 142 ellenséggel.
**Ugyanaz a pálya, ugyanaz a téma, teljesen eltérő kihívás.**

---

## 2. Világok áttekintése

A spec 7 javasolt világát megtartom, két ponton javítva (lásd 4. pont), és egy 8. világot
javaslok Fázis 2-re.

| # | Világ | Fő matematikai tartalom | Új ellenség | Pályák | Tipikus CL-sáv* |
|---|---|---|---|---|---|
| 1 | Kezdők erdeje | `+`, `−` 20-ig | `Basic`, `Fast` | 8 | 1–3 |
| 2 | Százas erőd | `+`, `−` 100-ig, tízesátlépés, kölcsönzés | `Armored` | 10 | 2–5 |
| 3 | Szorzók barlangja | szorzótábla 10×10-ig | `Swarmlet` | 10 | 3–6 |
| 4 | Osztók tornya | osztás, maradék, `mod` | `Splitter` (osztódó) | 10 | 4–7 |
| 5 | Négy művelet birodalma | vegyes műveletek, műveleti sorrend | `Shifter` (sávváltó) | 12 | 5–8 |
| 6 | Logika kastélya | zárójel, hiányzó operandus, egyenlet | `Mirror` (tükör) | 12 | 6–9 |
| 7 | Mesterliga | minden, kétjegyű szorzás, többlépéses | `Prime` (prím-őr) | 10 + végtelen | 7–10 |
| 8* | Kódok obszervatóriuma (Fázis 2) | sorozatok, bináris/hexa, geometria | `Cipher` | 10 | 5–10 |

\* A „tipikus CL-sáv" csak **statisztikai várakozás**, nem korlát. Egy gyors fejlődésű gyerek
a W2-ben is lehet CL7-en.

---

## 3. Világok részletesen

### W1 — Kezdők erdeje
| | |
|---|---|
| Hangulat | napfényes erdei tisztás, fa-ellenségek, barátságos pasztell paletta |
| Bevezetett matek | `Addition`, `Subtraction` 20-ig; tízesátlépés a 6. pályától |
| Bevezetett kapucél | `MoreAmmo` (az egyetlen cél ebben a világban — a mechanika megtanulása) |
| Új ellenség | `Basic` (1. pálya), `Fast` (4. pálya) |
| Pályák | 8 (az 1–2. tanítópálya: 150 s, 8 matek-döntés; a 3–8.: 180–210 s) |
| Boss | **Makkfejű** (120 HP): 3 fázis, mindegyikben egy `+`/`−` sáv-feladat. 1. világ lévén 2 sávval, bőséges (+50%) döntési idővel. |
| Feloldás | – (kezdő világ) |
| Jutalom | ★-onként 5 `StarShards`; a világ 60%-os teljesítése → W2 + „Erdei" karakterkinézet |

### W2 — Százas erőd
| | |
|---|---|
| Hangulat | kőfal, bástyák, középkori erőd; a sávok a kapuboltozatok |
| Bevezetett matek | `+`/`−` 100-ig, tízesátlépés, kivonás kölcsönzéssel; **`MoreShield` kapucél** |
| Új ellenség | `Armored` (3. pálya) — bevezeti a „nem elég sokat lőni, tervezni is kell" élményt |
| Pályák | 10 (200–240 s) |
| Boss | **Falmester** (140 HP): a pajzsa csak akkor nyílik, ha a gyerek egy `ReachTarget` típusú feladatot pontosan eltalál (pl. „Érd el a 100-at: 68 + ?"). Hibás válasz → a pajzs 12 s-ig zárva. |
| Feloldás | W1 pályák ≥ 60%-a ★1-gyel |
| Jutalom | „Pajzstörő" fegyver (szórt lövés), `MoreShield` kapu állandósul a későbbi világokban |

### W3 — Szorzók barlangja
| | |
|---|---|
| Hangulat | kristálybarlang, visszhang, sötét-fluoreszkáló paletta |
| Bevezetett matek | `Multiplication` 2,3,5,10-es tábla → teljes 10×10; a sorozatos összeadás és a szorzás kapcsolata vizuálisan is megjelenik (a `Swarmlet` kötelékek 3×4 alakzatban érkeznek) |
| Új kapucél | **`LessEnemyArmor`** — itt tanulja meg a gyerek, hogy a **kisebb** eredmény is lehet a jó (spec 5.1, 305–307. sor) |
| Új ellenség | `Swarmlet` (rajzó kötelékek, 4–6 fő) |
| Pályák | 10 (220–250 s) |
| Boss | **Kristálypók** (160 HP), 8 lábbal: minden fázisban annyi lábat tör le, amennyi a szorzat egyik tényezője — a boss egy `a × b` feladatot mutat, és a helyes sáv `a×b` sebzést ad |
| Feloldás | W2 ≥ 60% ★1 **és** a `Multiplication` készség `Introduced` szinten |
| Jutalom | „Szorzó" fegyver (2 lövedék egyszerre), barlangi kinézet |

### W4 — Osztók tornya
| | |
|---|---|
| Hangulat | spirális torony, emeletenként emelkedő kamera |
| Bevezetett matek | `Division` (maradék nélkül) → `IntegerDivisionWithRemainder` → `Modulo` |
| Új ellenség | **`Splitter` (Osztódó)**: lelövéskor 2 db `Swarmlet`-re esik szét — a „maradék" fogalmának játékmechanikai metaforája. Ha a gyerek a maradékos feladatot helyesen oldja meg, a darabok nem jönnek létre. |
| Pályák | 10 (240 s) |
| Boss | **Maradékkirály** (170 HP): a sávokon `29 ÷ 4 = 7 maradék ?` típusú feladatok; a hibás sáv a maradéknak megfelelő számú `Splitter`-t szabadít el |
| Feloldás | W3 ≥ 60% ★1 **és** `Multiplication` készség `Practiced` szinten (az osztás előfeltétele — spec 7.2, 619. sor) |
| Jutalom | „Felező" fegyver (átütő lövedék), `RepairKit` erőforrás feloldása (Fázis 2) |

### W5 — Négy művelet birodalma
| | |
|---|---|
| Hangulat | nyitott mező négy égtájra osztott biómmal, a hullámok az égtájak szerint váltanak |
| Bevezetett matek | `MixedOperations`, **műveleti sorrend** (`OperatorPrecedence`) zárójel nélkül: `8 + 4 × 6 − 7`; **`ReachTarget` kapucél** |
| Új ellenség | **`Shifter` (Sávváltó)**: 2 másodpercenként átlép a szomszédos sávba — kényszeríti a gyerekre, hogy a matek-döntés mellett a harci pozíciót is tervezze |
| Pályák | 12 (240–270 s) |
| Boss | **Négyarcú** (190 HP): 4 fázis, fázisonként más operátor dominál; a gyerek az éppen aktív operátorhoz tartozó készségéből kap feladatot |
| Feloldás | W4 ≥ 60% ★1 |
| Jutalom | „Négyes" fegyver (4 irányú szórás), birodalmi kinézet, a `FocusCharge` erőforrás feloldása (Fázis 2) |

### W6 — Logika kastélya
| | |
|---|---|
| Hangulat | ködös kastély, tükörtermek, lila-arany |
| Bevezetett matek | `Parentheses` (`(6+4)×3`), `MissingOperand` (`7 + ? = 3 × 8`), `EquationSolving` (`(?+17)×3 = 6×(14−3)`) |
| Új ellenség | **`Mirror` (Tükör)**: csak akkor sebezhető, ha a gyerek a **szomszédos** sávból lő rá — a „mindkét oldal egyenlő" gondolat térbeli megfelelője |
| Pályák | 12 (250–280 s) |
| Boss | **Kulcsmester** (210 HP): a boss egy egyenlet bal oldalát tartja, a sávokon a jobb oldal lehetséges értékei érkeznek; a helyes egyenlőség nyitja a páncélt |
| Feloldás | W5 ≥ 60% ★1 **és** `OperatorPrecedence` készség `Practiced` szinten |
| Jutalom | „Kulcs" fegyver (lassító lövedék), kastély-kinézet |

### W7 — Mesterliga
| | |
|---|---|
| Hangulat | aréna, lelátó, reflektorok; a HUD „verseny" stílusú |
| Bevezetett matek | kétjegyű × kétjegyű szorzás, 4–5 operandusos, kétszintű zárójeles kifejezések, kétoldalas ismeretlen |
| Új ellenség | **`Prime` (Prím-őr)**: csak prímszám-válasszal sebezhető gyengepontja van; ritka, ünnepi ellenfél |
| Pályák | 10 kampánypálya + **Végtelen aréna** (nem progressziós, hetente frissülő seed) |
| Boss | **Nagymester** (250 HP), 5 fázis; az utolsó fázisban a gondolkodási idő a CL-érték 75%-a |
| Feloldás | W6 ≥ 60% ★1 **és** `ChallengeLevel ≥ 6` (az egyetlen hely, ahol a CL feloldási feltétel — itt a tartalom valóban csak magas szinten értelmes) |
| Jutalom | „Mester" kinézet, aranysáv a profilon; a Végtelen aréna **nem** ad kizárólagos jutalmat, csak pontszámot (nincs FOMO-nyomás — spec 10.2) |

### W8 — Kódok obszervatóriuma *(Fázis 2, javaslat)*
A spec 6.0.1–6.0.4 „későbbi fejlesztés" pontjait (bináris/hexa számrendszer, sorozatok,
geometria) egy önálló világba gyűjti, hogy ne hígítsák fel az aritmetikai világokat.
Új ellenség: `Cipher` (kódolt, a HP-ja binárisan jelenik meg). Pályák: 10.
Feloldás: W5 ≥ 60% ★1 (nem igényli a W6/W7-et, így párhuzamos ág, ami változatosságot ad).

---

## 4. Amin a specen javítottam — és miért

1. **`Splitter` ellenség a W4-ben** a spec eredeti „Osztók tornya" leírásához: a maradékos
   osztás absztrakt fogalma így **látható játékmechanikává** válik (a maradék = a szétesett
   darabok). Pedagógiailag ez többet ér, mint egy újabb sáv-feladat.
2. **A `LessEnemyArmor` kapucél a W3-ban, nem később.** A spec kéri, hogy a cél ne mindig a
   nagyobb érték legyen (305–307. sor). Minél tovább tanítjuk a „nagyobb mindig jobb"
   heurisztikát, annál nehezebb lesz leszoktatni róla — ezért már a 3. világban megtörjük.
3. **A W7 „Végtelen aréna" nem ad exkluzív jutalmat.** Egy végtelen mód ranglistával és
   exkluzív jutalommal kényszeres játékra ösztönözne; a spec 10.2 és 12. szakasza ezt tiltja.
4. **Világonkénti készség-előfeltétel a feloldásban** (pl. W4-hez `Multiplication: Practiced`).
   A spec csak a feladatgenerálásnál követeli meg az előfeltételeket; a világkapukra kiterjesztve
   megelőzzük, hogy egy gyerek szorzás nélkül érjen az osztáshoz.
5. **W8 mint párhuzamos ág**, nem a sor végén: így a 6.0.x „későbbi" tartalmak nem csak a
   legjobbaknak elérhetők.

---

## 5. Pályastruktúra-sablonok

Minden pálya a GAME_DESIGN.md 2.2. szakaszának S0–S8 sablonjából épül, három változatban:

| Sablon | Hossz | Matek-döntés | Boss | Használat |
|---|---|---|---|---|
| `Tutorial` | 150 s | 8 | nincs | világonként az első 1–2 pálya |
| `Standard` | 210–250 s | 12–16 (CL-függő, lásd `difficulty-model.md` 5.3) | nincs vagy mini | a pályák ~80%-a |
| `BossLevel` | 260–300 s | 14–18 | igen, 3–5 fázis | világonként az utolsó pálya |

Minden `Standard` és `BossLevel` pálya teljesíti a spec 866. sorát (≥ 10 matek-döntés).
A `Tutorial` sablon 8 döntése a spec 868. sora alapján elfogadható („az első játszható
verzióban rövidebb pályák is elfogadhatók").

---

## 6. ScriptableObject-séma (kiegészítés a `docs/contracts/scriptable-object-schema.md`-hez)

| Asset | Kulcsmezők |
|---|---|
| `WorldDefinition` | `worldId`, `displayNameHu`, `themeId`, `enabledSkills[]`, `enabledGateGoals[]`, `introducedEnemyTypes[]`, `levels[]` (`LevelDefinition` referenciák), `bossLevelRef`, `unlockRequirement` (`UnlockRequirement`), `rewardsOnCompletion[]` |
| `UnlockRequirement` | `requiredWorldId`, `requiredStarRatio` (0.60), `requiredSkills[]` (`skillTag` + `minMasteryLevel`), `requiredChallengeLevel` (0 = nincs) |
| `LevelDefinition` | `levelId`, `worldId`, `templateType` (Tutorial/Standard/BossLevel), `targetDurationSec`, `mathDecisionCount`, `sectionTimeline[]` (S0–S8), `waveRefs[]`, `bossRef`, `allowedProblemTypes[]`, `environmentRef` |
| `WaveDefinition` | `waveId`, `startTimeSec`, `durationSec`, `enemyComposition[]` (`enemyType` + `weight`), `spawnDensityBase`, `laneDistribution` |
| `BossDefinition` | `bossId`, `baseHp`, `phases[]` (`hpThreshold`, `mathProblemType`, `damageOnCorrect`, `penaltyOnWrong`, `behaviourModifiers`) |
| `EnemyDefinition` | `enemyType`, `baseHp`, `baseSpeed`, `breakthroughDamage`, `armorReduction`, `splitOnDeath`, `silhouetteId`, `prefabRef` |
| `RewardDefinition` | `rewardType` (Stars/Shards/Weapon/Skin/Ability/World), `amount`, `unlockRef` |

A `WorldDefinition.enabledSkills` × `ChallengeLevelDefinition.allowedOperators` metszetképzése
a `ProblemGenerationContext` felelőssége (`math-engine-dev`) — lásd 1. pont.
