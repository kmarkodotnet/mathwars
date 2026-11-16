# Világok és pályák — tartalmi terv

> Státusz: ARCH fázis, v1.1 · Felelős: game-designer agent
> Forrás: spec 10. szakasz (828–885. sor), 7.2. (matematikai tartalom), 8. (készségprofil).
> A pálya tartalma **konfigurációból épül fel** (spec 848. sor) — lásd 6. pont.
>
> **v1.1 (2026-11-16, architect-konszolidáció, ADR-009):** minden típus- és mezőnév a
> kontrakt kanonikus nevére cserélve; a `SkillTag`-ek a valóban létező enum-értékekre
> javítva (`MixedOperations` → `MultiStepExpressions`, `EquationSolving` → feladattípus,
> nem készség); a készség-fokozatok a `SkillMasteryLevel` enumra hivatkoznak (ADR-010);
> az új ellenség-archetípusok F5-ös, lefoglalt enum-értékekként jelölve (ADR-008).

---

## 1. A legfontosabb szabály: világ ≠ kihívásszint

> „A világok és a dinamikus nehézségi szint két külön fogalom. Egy gyermek lehet a harmadik
> világban 4-es kihívásszinten, egy másik ugyanott 7-esen." (spec 844–846. sor)

| | Világ (`WorldId`) | Kihívásszint (`ChallengeLevel` 1–10) |
|---|---|---|
| Mit határoz meg? | **Milyen** matematikai készségek, kapucélok és ellenségtípusok jelennek meg egyáltalán (tartalmi kapu) | **Milyen nehezen** jelennek meg: számtartomány, operandusszám, idő, harci intenzitás |
| Mi változtatja? | Pályateljesítés → `WorldUnlockRequirement` | `AdaptiveDifficultyService` (lásd `difficulty-model.md` 6.) |
| Irány | csak előre | fel és le |
| Látható? | világtérképen | HUD-sáv felül, számmal |

**Gyakorlati következmény a `gameplay-dev` számára:** a pálya generálásakor a
`WorldDefinition.IntroducedSkills` és a `DifficultyProfile.UnlockedSkills` **metszete** adja a
megengedett feladathalmazt. Ha a metszet üres (pl. CL8 „ismeretlen operandus"-t kérne, de a W2
azt még nem engedélyezi), akkor a **világ szűkebb halmaza nyer**, és a CL a többi tengelyen
(idő, harc, számtartomány, operandusszám) érvényesül. Ez a spec 621. sorának közvetlen
megvalósítása. Ugyanez a szabály érvényes a `EnabledGateGoals` és az `IntroducedEnemyTypes`
halmazokra is (ld. `difficulty-model.md` 4. `SwarmletRatio`-megjegyzés).

**Példa (a spec idézett esete):** W3 „Szorzók barlangja" mindkét gyereknél szorzótáblát ad.
A 4-es szintű gyerek 2,3,5,10-es táblát kap 10,0 s gondolkodási idővel és 92 ellenséggel;
a 7-es szintű gyerek vegyes, zárójeles szorzásos kifejezést 7,0 s-mal és 142 ellenséggel.
**Ugyanaz a pálya, ugyanaz a téma, teljesen eltérő kihívás.**

---

## 2. Világok áttekintése

A spec 7 javasolt világát megtartom, két ponton javítva (lásd 4. pont), és egy 8. világot
javaslok Fázis 2-re.

| # | Világ | Fő matematikai tartalom | Új ellenség (`EnemyType`) | Pályák | Tipikus CL-sáv* |
|---|---|---|---|---|---|
| 1 | Kezdők erdeje | `+`, `−` 20-ig | `Basic`, `Fast` | 8 | 1–3 |
| 2 | Százas erőd | `+`, `−` 100-ig, tízesátlépés, kölcsönzés | `Armored` | 10 | 2–5 |
| 3 | Szorzók barlangja | szorzótábla 10×10-ig | `Swarmlet` | 10 | 3–6 |
| 4 | Osztók tornya | osztás, maradék, `mod` | `Splitter` (osztódó) **[F5]** | 10 | 4–7 |
| 5 | Négy művelet birodalma | vegyes műveletek, műveleti sorrend | `Shifter` (sávváltó) **[F5]** | 12 | 5–8 |
| 6 | Logika kastélya | zárójel, hiányzó operandus, egyenlet | `Mirror` (tükör) **[F5]** | 12 | 6–9 |
| 7 | Mesterliga | minden, kétjegyű szorzás, többlépéses | `Prime` (prím-őr) **[F5]** | 10 + végtelen | 7–10 |
| 8* | Kódok obszervatóriuma (Fázis 2) | sorozatok, bináris/hexa, geometria | `Cipher` **[F6+]** | 10 | 5–10 |

\* A „tipikus CL-sáv" csak **statisztikai várakozás**, nem korlát. Egy gyors fejlődésű gyerek
a W2-ben is lehet CL7-en.

**[F5] / [F6+] jelölés:** ezek az `EnemyType` enum-értékek **még nem léteznek**; a kontrakt
(`core-domain.md` 7.) lefoglalt számokkal rögzíti őket, és csak a Fázis 5-ben kerülnek be.
A W4–W7 pályái F5 előtt a már létező öt archetípusból építkeznek.

---

## 3. Világok részletesen

### W1 — Kezdők erdeje
| | |
|---|---|
| Hangulat | napfényes erdei tisztás, fa-ellenségek, barátságos pasztell paletta |
| `IntroducedSkills` | `Addition`, `Subtraction`, `SingleDigitAddition`, `TwoDigitAddition`, `CrossingTen` (a 6. pályától), `ResourceComparison` |
| `EnabledGateGoals` | `{ Maximize }` (az egyetlen cél ebben a világban — a mechanika megtanulása) |
| `IntroducedEnemyTypes` | `{ Basic, Fast }` — `Basic` az 1., `Fast` a 4. pályától |
| Pályák | 8 (`TemplateType = Tutorial` az 1–2.: 150 s, 8 matek-döntés; a 3–8.: `Standard`, 180–210 s) |
| Boss | **Makkfejű** (`EnemyDefinitionSO`, `Type = Boss`, `MaxHealth = 120`): 3 fázis, mindegyikben egy `+`/`−` sáv-feladat. 1. világ lévén 2 sávval, bőséges (+50%) döntési idővel. |
| `Unlock` | null (kezdő világ) |
| Jutalom | ★-onként 5 `StarShards` (`StarRatingSO.ShardsPerStar`); a világ 60%-os teljesítése → W2 + „Erdei" karakterkinézet |

### W2 — Százas erőd
| | |
|---|---|
| Hangulat | kőfal, bástyák, középkori erőd; a sávok a kapuboltozatok |
| `IntroducedSkills` | `TwoDigitAddition`, `CrossingTen`, `SubtractionWithBorrowing` |
| `EnabledGateGoals` | `{ Maximize, ReachTarget }` — a `Maximize × Shield` („Több pajzsot!") kapu itt jelenik meg, és a boss `ReachTarget` feladatot ad |
| `IntroducedEnemyTypes` | `+ Armored` (3. pálya) — bevezeti a „nem elég sokat lőni, tervezni is kell" élményt |
| Pályák | 10 (200–240 s) |
| Boss | **Falmester** (140 HP): a pajzsa csak akkor nyílik, ha a gyerek egy `TargetValueSelection` feladatot (`Goal = ReachTarget`) pontosan eltalál (pl. „Érd el a 100-at: 68 + ?"). Hibás válasz → a pajzs 12 s-ig zárva (`BossPhaseEntry.PenaltyOnWrongSeconds = 12`). |
| `Unlock` | `RequiredWorld = w1`, `RequiredStarRatio = 0.60` |
| Jutalom | „Pajzstörő" fegyver (szórt lövés, azonos DPS); a `Maximize × Shield` kapu állandósul a későbbi világokban |

> **Megjegyzés a `ReachTarget` időzítéséhez:** a `GAME_DESIGN.md` 3.4 a `ReachTarget`-et
> „W5-től" általános kapucélként vezeti be; a W2 boss **egyetlen, scriptelt** feladatként
> használja. Ezért a W2 `EnabledGateGoals`-ja tartalmazza, de a `Standard` pályák
> `MathEventScheduleEntry`-jei nem generálnak `ReachTarget` kaput W5 előtt. A két állítás
> így nem ütközik.

### W3 — Szorzók barlangja
| | |
|---|---|
| Hangulat | kristálybarlang, visszhang, sötét-fluoreszkáló paletta |
| `IntroducedSkills` | `Multiplication`, `TimesTable2`, `TimesTable3`, `TimesTable5`, `TimesTable10` → majd `TimesTable4/6/7/8/9`; a sorozatos összeadás és a szorzás kapcsolata vizuálisan is megjelenik (a `Swarmlet` kötelékek 3×4 alakzatban érkeznek) |
| `EnabledGateGoals` | `+ Minimize` — itt tanulja meg a gyerek, hogy a **kisebb** eredmény is lehet a jó (`Minimize × EnemyArmor`, spec 5.1, 305–307. sor) |
| `IntroducedEnemyTypes` | `+ Swarmlet` (rajzó kötelékek, `SpawnGroupSize = 4`) |
| Pályák | 10 (220–250 s) |
| Boss | **Kristálypók** (160 HP), 8 lábbal: minden fázisban annyi lábat tör le, amennyi a szorzat egyik tényezője — a boss egy `a × b` feladatot mutat, és a helyes sáv `a×b` sebzést ad |
| `Unlock` | `RequiredWorld = w2`, `RequiredStarRatio = 0.60`, `RequiredSkills = { Multiplication: Introduced }` |
| Jutalom | „Szorzó" fegyver (2 lövedék egyszerre), barlangi kinézet |

### W4 — Osztók tornya
| | |
|---|---|
| Hangulat | spirális torony, emeletenként emelkedő kamera |
| `IntroducedSkills` | `Division`, `DivisionWithRemainder`, `Remainder` (az operátorok: `Division` → `IntegerDivisionWithRemainder` → `Modulo`) |
| `IntroducedEnemyTypes` | `+ Splitter` **[F5]**: lelövéskor 2 db `Swarmlet`-re esik szét (`SplitOnDeathEnemy`, `SplitOnDeathCount = 2`) — a „maradék" fogalmának játékmechanikai metaforája. Ha a gyerek a maradékos feladatot helyesen oldja meg, a darabok nem jönnek létre. F5 előtt a W4 `Armored` + `Swarmlet` mixet használ. |
| Pályák | 10 (240 s) |
| Boss | **Maradékkirály** (170 HP): a sávokon `29 ÷ 4 = 7 maradék ?` típusú feladatok; a hibás sáv a maradéknak megfelelő számú `Splitter`-t szabadít el |
| `Unlock` | `RequiredWorld = w3`, `RequiredStarRatio = 0.60`, `RequiredSkills = { Multiplication: Practiced }` (az osztás előfeltétele — spec 7.2, 619. sor) |
| Jutalom | „Felező" fegyver (átütő lövedék), `RepairKit` erőforrás feloldása (Fázis 2) |

### W5 — Négy művelet birodalma
| | |
|---|---|
| Hangulat | nyitott mező négy égtájra osztott biómmal, a hullámok az égtájak szerint váltanak |
| `IntroducedSkills` | `MultiStepExpressions`, `OperatorPrecedence` zárójel nélkül: `8 + 4 × 6 − 7` |
| `EnabledGateGoals` | mind (`Maximize`, `Minimize`, `ReachTarget`) — a `ReachTarget` itt lesz általános kapucél |
| `IntroducedEnemyTypes` | `+ Shifter` **[F5]**: 2 másodpercenként átlép a szomszédos sávba — kényszeríti a gyerekre, hogy a matek-döntés mellett a harci pozíciót is tervezze |
| Pályák | 12 (240–270 s) |
| Boss | **Négyarcú** (190 HP): 4 fázis, fázisonként más operátor dominál; a gyerek az éppen aktív operátorhoz tartozó készségéből kap feladatot |
| `Unlock` | `RequiredWorld = w4`, `RequiredStarRatio = 0.60` |
| Jutalom | „Négyes" fegyver (4 irányú szórás), birodalmi kinézet, a `FocusCharge` erőforrás feloldása (Fázis 2) |

### W6 — Logika kastélya
| | |
|---|---|
| Hangulat | ködös kastély, tükörtermek, lila-arany |
| `IntroducedSkills` | `Parentheses` (`(6+4)×3`), `MissingOperand` (`7 + ? = 3 × 8`) |
| Feladattípusok | `ProblemType.MissingOperand` és `ProblemType.EquationSolving` (`(?+17)×3 = 6×(14−3)`). **Nincs `SkillTag.EquationSolving`** — az egyenletes feladat készségei `MissingOperand` + `MultiStepExpressions` (+ `Parentheses`), ld. `core-domain.md` 3. |
| `IntroducedEnemyTypes` | `+ Mirror` **[F5]**: csak akkor sebezhető, ha a gyerek a **szomszédos** sávból lő rá — a „mindkét oldal egyenlő" gondolat térbeli megfelelője |
| Pályák | 12 (250–280 s) |
| Boss | **Kulcsmester** (210 HP): a boss egy egyenlet bal oldalát tartja, a sávokon a jobb oldal lehetséges értékei érkeznek; a helyes egyenlőség nyitja a páncélt |
| `Unlock` | `RequiredWorld = w5`, `RequiredStarRatio = 0.60`, `RequiredSkills = { OperatorPrecedence: Practiced }` |
| Jutalom | „Kulcs" fegyver (lassító lövedék), kastély-kinézet |

### W7 — Mesterliga
| | |
|---|---|
| Hangulat | aréna, lelátó, reflektorok; a HUD „verseny" stílusú |
| `IntroducedSkills` | `TwoDigitMultiplication`; 4–5 operandusos, kétszintű zárójeles kifejezések, kétoldalas ismeretlen (`UnknownPlacement.EitherSide`) |
| `IntroducedEnemyTypes` | `+ Prime` **[F5]**: csak prímszám-válasszal sebezhető gyengepontja van; ritka, ünnepi ellenfél |
| Pályák | 10 kampánypálya + **Végtelen aréna** (nem progressziós, hetente frissülő seed) |
| Boss | **Nagymester** (250 HP), 5 fázis; az utolsó fázisban a gondolkodási idő a CL-érték 75%-a |
| `Unlock` | `RequiredWorld = w6`, `RequiredStarRatio = 0.60`, `RequiredSkills = { OperatorPrecedence: Mastered }`, `RequiredChallengeLevel = 6` (az **egyetlen** hely, ahol a CL feloldási feltétel — itt a tartalom valóban csak magas szinten értelmes; a kontrakt validációja pontosan egy ilyen világot engedélyez) |
| Jutalom | „Mester" kinézet, aranysáv a profilon; a Végtelen aréna **nem** ad kizárólagos jutalmat, csak pontszámot (nincs FOMO-nyomás — spec 10.2) |

### W8 — Kódok obszervatóriuma *(Fázis 2, javaslat)*
A spec 6.0.1–6.0.4 „későbbi fejlesztés" pontjait (bináris/hexa számrendszer, sorozatok,
geometria) egy önálló világba gyűjti, hogy ne hígítsák fel az aritmetikai világokat.
Új ellenség: `Cipher` **[F6+]** (kódolt, a HP-ja binárisan jelenik meg). Pályák: 10.
`Unlock`: `RequiredWorld = w5`, `RequiredStarRatio = 0.60` (nem igényli a W6/W7-et, így
párhuzamos ág, ami változatosságot ad).

---

## 4. Amin a specen javítottam — és miért

1. **`Splitter` ellenség a W4-ben** a spec eredeti „Osztók tornya" leírásához: a maradékos
   osztás absztrakt fogalma így **látható játékmechanikává** válik (a maradék = a szétesett
   darabok). Pedagógiailag ez többet ér, mint egy újabb sáv-feladat. **Fázis 5.**
2. **A `Minimize` kapucél a W3-ban, nem később.** A spec kéri, hogy a cél ne mindig a
   nagyobb érték legyen (305–307. sor). Minél tovább tanítjuk a „nagyobb mindig jobb"
   heurisztikát, annál nehezebb lesz leszoktatni róla — ezért már a 3. világban megtörjük.
3. **A W7 „Végtelen aréna" nem ad exkluzív jutalmat.** Egy végtelen mód ranglistával és
   exkluzív jutalommal kényszeres játékra ösztönözne; a spec 10.2 és 12. szakasza ezt tiltja.
4. **Világonkénti készség-előfeltétel a feloldásban** (pl. W4-hez `Multiplication: Practiced`).
   A spec csak a feladatgenerálásnál követeli meg az előfeltételeket; a világkapukra kiterjesztve
   megelőzzük, hogy egy gyerek szorzás nélkül érjen az osztáshoz. Ehhez a kontrakt
   `WorldUnlockRequirement.RequiredSkills` szótára kellett (korábban csak egyetlen
   pálya-referencia volt — ADR-009 által javított kontrakt-hiányosság).
5. **W8 mint párhuzamos ág**, nem a sor végén: így a 6.0.x „későbbi" tartalmak nem csak a
   legjobbaknak elérhetők.
6. **Az ötödik ellenség-archetípus (`Swarmlet`) a W3-ban** — a horda-élmény feltétele,
   a kontraktban F1-től létezik (ADR-008). Indoklás: `GAME_DESIGN.md` 5.

---

## 5. Pályastruktúra-sablonok

Minden pálya a GAME_DESIGN.md 2.2. szakaszának S0–S8 sablonjából épül, három változatban
(`LevelDefinitionSO.TemplateType`):

| `TemplateType` | Hossz | Matek-döntés | Boss | Használat |
|---|---|---|---|---|
| `Tutorial` | 150 s | 8 | nincs | világonként az első 1–2 pálya |
| `Standard` | 210–250 s | 12–16 (CL-függő, lásd `difficulty-model.md` 5.3) | nincs vagy mini | a pályák ~80%-a |
| `BossLevel` | 260–300 s | 14–18 | igen, 3–5 fázis | világonként az utolsó pálya |

Minden `Standard` és `BossLevel` pálya teljesíti a spec 866. sorát (>= 10 matek-döntés).
A `Tutorial` sablon 8 döntése a spec 868. sora alapján elfogadható („az első játszható
verzióban rövidebb pályák is elfogadhatók") — a kontrakt validációja ezért a
`TemplateType == Tutorial` esetre engedi a >= 8 értéket.

---

## 6. ScriptableObject-megfeleltetés

> **A kanonikus mezőnevek a `docs/contracts/scriptable-object-schema.md`-ben vannak** —
> ez a táblázat csak azt mutatja, melyik világ-tartalmi döntés hová kerül. A korábbi
> revízió saját mezőneveket (`worldId`, `displayNameHu`, `enabledSkills[]`, `bossRef`, …)
> használt; ezek **elavultak** (ADR-007, ADR-009).

| Asset (kanonikus) | Mely tartalmi döntést hordozza | Séma |
|---|---|---|
| `WorldDefinitionSO` | `Id`, `DisplayNameKey`, `OrderIndex`, `Levels[]`, `IntroducedSkills[]`, `EnabledGateGoals[]`, `IntroducedEnemyTypes[]`, `Unlock`, `ThemeColor` | SO-séma 4. |
| `WorldUnlockRequirementEntry` | `RequiredWorld`, `RequiredStarRatio` (0.60), `RequiredSkills[]` (`SkillTag` + `SkillMasteryLevel`), `RequiredChallengeLevel` (0 = nincs) | SO-séma 4.1 |
| `LevelDefinitionSO` | `Id`, `World`, `TemplateType`, `TargetDurationSeconds`, `LaneCount`, `Waves[]`, `MathEvents[]`, `BossEnemy`, `EnvironmentPrefab` | SO-séma 3. |
| `WaveDefinitionEntry` + `WaveSpawnEntry` | `WaveIndex`, `StartGameTime`, `DurationSeconds`, `Spawns[]` (`Enemy`, `Count`, `SpawnIntervalSeconds`, `AllowedLanes[]`) | SO-séma 3.1–3.2 |
| `EnemyDefinitionSO` (`Type = Boss`) + `BossPhaseEntry[]` | `MaxHealth`, fázisonként `HpThreshold`, `ProblemType`, `DamageOnCorrect`, `PenaltyOnWrongSeconds` — **nincs külön `BossDefinition` asset** | SO-séma 1., 1.1 |
| `EnemyDefinitionSO` | `Type`, `MaxHealth`, `MovementSpeed`, `ContactDamage`, `RewardValue` (= lőszer-refund), `ArmorPoints`/`FrontalDamageReduction` [F5], `SplitOnDeathEnemy`/`SplitOnDeathCount` [F5], `SpawnGroupSize`, `Prefab` | SO-séma 1. |
| `StarRatingSO` | `WorldUnlockStarRatio`, `ShardsPerStar`, `SkinStarMilestones[]` | SO-séma 14. |

A `WorldDefinition.IntroducedSkills` × `DifficultyProfile.UnlockedSkills` metszetképzése
a `ProblemGenerationRequest` összeállításának felelőssége (`gameplay-dev` állítja elő,
`math-engine-dev` fogyasztja) — lásd 1. pont és `math-engine.md` 7.
