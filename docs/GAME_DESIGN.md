# Math Horde (Számharcosok) — Game Design Document

> Státusz: ARCH fázis, v1.3 · Felelős: game-designer agent
> Kapcsolódó dokumentumok: `docs/ARCHITECTURE.md` (architect agent),
> `docs/contracts/scriptable-object-schema.md` (architect agent),
> `docs/TESTING.md` (qa-unity agent),
> `docs/balance/difficulty-model.md`, `docs/balance/worlds.md`, `docs/balance/SIM-plan.md`.
> Ez a dokumentum a spec 20. szakasz 13. pontja által kötelezően előírt leszállítandó.
> Minden itt szereplő szám **ScriptableObject-alapértelmezés**, nem kódba égetett konstans.
>
> **v1.2 — ADR-017 (2026-11-19):** a felhasználó döntése alapján minden fegyver
> mindig végtelen lőszerrel működik. A matek nem használ lőszert; a választható típusok
> egyre erősebbek lehetnek. A végtelen tüzelés és ammo-kivezetés F1-20-ban elkészült;
> a tényleges HUD-fegyverválasztás F1-21-ben elkészült.
>
> **v1.3 — ADR-018 + ADR-019 (2026-11-22):** a felhasználó döntése alapján
> **egyetlen matematikai feladat sem használ élő játékos-értéket operandusként**.
> Az összehasonlító kapu saját, generált bázisértékkel dolgozik, és **mindkét
> kötelező mechanika jutalma fix, előre konfigurált életerő**. A Pajzs és a Lőszer
> semmilyen matematikai feladatban nem szerepelhet. Az erőforrás-snapshot
> mechanika (v1.1, ADR-015/016) ezzel **kivezetve**.
>
> **Dokumentum-hierarchia:** ütközés esetén a `docs/contracts/*` az irányadó; ez a
> dokumentum a *miért*-et és az értékeket adja, a kontrakt a *nevet és a típust* (ADR-009).

---

## 1. Játékkoncepció és célközönség

**Egy mondatban:** a játékos egy automatikusan tüzelő harcost irányít a képernyő alján,
balra-jobbra húzva sávot vált, és a felülről érkező hordát csak akkor tudja feltartóztatni,
ha a közeledő matematikai kapukon és válaszsávokon helyesen dönt — a helyes döntés
**életerőt** ad, a pajzs és a matematikai gyengepontok pedig tovább segítik a túlélést.
A fegyverek korlátlanul tüzelnek.

| Jellemző | Érték |
|---|---|
| Célközönség | 7–12 év (2.–6. osztály), magyar tanterv szerinti aritmetika |
| Platform | Android, portré mód, egy hüvelykujjal játszható |
| Session-hossz | 1 pálya ≈ 3–5 perc, tipikus ülés 2–4 pálya (8–20 perc) |
| Műfaj | 3D horde-survival shooter + beépített matematikai döntéshozatal |
| Input | Vízszintes drag/swipe a sávváltáshoz. A tüzelés automatikus, nincs külön lövés-gomb. |
| Monetizáció | Nincs gacha, loot box, energiarendszer vagy reklám (spec 10.2) |

**A nem módosítható alapelvekhez való viszony (spec 23):** a játék elsődlegesen lövöldözős
túlélőjáték (1.), a matek az erőforrást szabályozza (2.), mindkét feladattípus kötelező (3.),
és a matematikai döntés alatt a harc, a lövedékek és az ellenségmozgás **egyetlen frame-re sem
áll meg** (4.). A kvízablak, a `Time.timeScale = 0` és a „válaszolj, aztán folytasd" minta
tiltott minta a projektben.

> A 2. alapelv („a matematikai számítások a túléléshez szükséges erőforrások
> megszerzését szabályozzák”) az ADR-018 után is teljesül: a matek az **életerőt**
> szabályozza, ami a túlélés elsődleges erőforrása (a `Health <= 0` az egyetlen
> vereség-ok). A változás annyi, hogy a jutalom **összege** fix és előre látható,
> nem a feladat számaiból levezetett.

---

## 2. Alapvető játékhurok (core loop)

### 2.1. Mikro-loop (3–8 másodperc, folyamatosan ismétlődik)

```
ellenség közeledik → auto-tüzelés sebzi az ellenséget → matek-elem jelenik meg a pálya felső
harmadában → a gyerek fejben számol, miközben továbbra is lő és kerülget → sávot vált →
áthalad a kapun/sávon → azonnali életerő-visszajelzés → következő hullám
```

### 2.2. Makro-loop egy ~4 perces pályán (240 s, referencia CL5 nehézségen)

A spec 10.1. szakaszának 9 lépése konkrét idősávokra bontva. A „Matek-döntés" oszlop összege
**14 döntési helyzet**, ami teljesíti a 10.1. minimum 10-es követelményét tartalékkal, és
megegyezik a `difficulty-model.md` 5.3. táblázatának CL5-ös `MathDecisionCount` értékével.

| Szakasz | Idő (s) | Tartalom | Matek-döntés | Kumulált |
|---|---|---|---|---|
| S0 Bevezetés | 0–12 | Első, lassú alap-ellenségek, nincs matek — a gyerek megtanulja a sávváltást | 0 | 0 |
| S1 Első kapu | 12–35 | Összehasonlító kapu (2 kapu, `Maximize`), közben 1. hullám | 1 gate | 1 |
| S2 Harc | 35–60 | 2. hullám, gyors ellenségek bevezetése | 1 lane | 2 |
| S3 Sáv-blokk | 60–95 | 2 db eredményválasztó sáv-feladat, 3. hullám | 2 lane | 4 |
| S4 Vegyes | 95–135 | 1 kapu (`Minimize` — „válaszd a kisebbet!") + 2 sáv, páncélos ellenség | 1 gate + 2 lane | 7 |
| S5 Nyomás | 135–175 | Sűrű 5. hullám, 2 sáv rövidebb döntési ablakkal | 2 lane | 9 |
| S6 Felkészülés | 175–195 | 1 kapu (`Maximize`, nagyobb fix életerő-jutalom) + 1 sáv a boss előtt | 1 gate + 1 lane | 11 |
| S7 Boss | 195–235 | Főellenség, 3 gyengepont-feladat (lásd 5.4) | 3 lane | 14 |
| S8 Eredmény | 235–250 | Eredményképernyő, csillagok, hibajavító áttekintés | – | 14 |

> **Világ-korlát:** az S4 `Minimize` kapuja csak olyan pályán szerepelhet, amelynek világa
> `EnabledGateGoals`-ban tartalmazza a `Minimize`-t (W3-tól — `worlds.md` 3.). A W1–W2
> pályáin ez a szakasz `Maximize` kapura cserélődik. A `Minimize` az ADR-018 után
> **nem igényel csökkentendő erőforrást**: a jutalom akkor is fix életerő, a cél csak
> annyit jelent, hogy a *kisebb* eredményű sávot kell választani.
> Ez a táblázat **sablon**, nem konkrét pálya.

**Tervezési szabályok a loopra:**
- Két egymást követő matek-döntés között legyen **legalább 8 s** tiszta harc, különben a gyerek
  kognitívan túlterhelődik és a lövöldözős játékélmény eltűnik (spec 23/1.). Konfiguráció:
  `TimePressureTableSO.MinGapBetweenProblemsSeconds` (CL-enként 8–14 s).
- Egy pályán **legfeljebb 2 egymást követő** azonos típusú feladat, hogy a két kötelező mechanika
  váltakozzon (spec 23/3.). Konfiguráció: `MathEventConfigSO.MaxConsecutiveSameProblemType`.
- A pálya utolsó 15 másodperce matek-mentes „győzelmi" szakasz: a gyerek élvezheti a folyamatos
  tüzelést és a kiválasztott fegyvertípust.
- Pályahossz-paraméter: `LevelDefinitionSO.TargetDurationSeconds` (default 240, tartomány 180–300;
  az SO tartománya 60–600, mert a `Tutorial` sablon 150 s).
  Az MVP első világában 150 s is elfogadható (spec 10.1 utolsó bekezdése).

---

## 3. Mechanika A — Összehasonlító kapuk (spec 5.1, ADR-018)

### 3.1. Működés

A pálya felső részén 2 (ritkábban 3–4) kapu ereszkedik lefelé. A kapuk felett a HUD
mutatja a feladat **bázisértékét** (pl. `6`), a **célikont** („válaszd a nagyobbat!"),
és a **fix életerő-jutalmat** (pl. `+30 ♥`). Minden kapun egy **művelet** van kiírva
(pl. `×2`, `+25`), tehát a gyerek a `6 × 2` és a `6 + 25` eredményét hasonlítja össze.
A kapuk **eredménye** a döntés előtt **nem látható** (spec 5.1), csak az áthaladás utáni
visszajelzésben.

A feladat teljes tartalma (bázisérték, műveletek, melyik a helyes) **a feladat
létrehozásakor, egyszer, önállóan generálódik** — nem a játékos erőforrásállásából.
Ezért a helyes válasz a kiírástól a döntésig nem változhat. A Preview szakaszban csak a
cél és a fix jutalom látszik; a bázisérték és a műveletek a kapuk megjelenésekor
(`Approaching`) válnak láthatóvá. A válaszsávos feladatok teljes, korai Preview-je
változatlan (4.1.).

### 3.2. Fix életerő-jutalom — a mechanika lelke (ADR-018)

```
Feladat létrehozásakor:   GateBaseValue és a műveletek generálódnak   (pl. 6 ; ×2 ; +25)
Áthaladáskor (helyes):    RewardDelta = RewardSpec.FlatAmount          (pl. +30 életerő)
Áthaladáskor (hibás):     RewardDelta = 0
Új erőforrás:             Health = min(Health + RewardDelta, MaxHealth)
```

**Kidolgozott példa (a spec 21/B elfogadási kritériuma):**

| Lépés | Érték |
|---|---|
| A kapu bázisértéke (generált, nem erőforrásállás) | 6 |
| Bal kapu `×2` → eredmény | 12 |
| Jobb kapu `+25` → eredmény | 31 ✅ optimális (`Maximize`) |
| Életerő a döntés pillanatában (a harc 12 sebzést okozott) | 88 |
| A jobb kapun áthalad, fix jutalom `+30` | 88 + 30 = **100** (`Max` 100 ⇒ `WasClamped`) |
| Ha a bal kaput választja | 88 + 0 = **88** (hibás választás → nincs matematikai jutalom) |

A kapu soha nem állít be abszolút értéket, mindig **deltát** ad, és a delta soha nem
függ a feladat számaitól. Két haszna van:
1. **A helyes válasz nem csúszhat el**: nincs olyan élő érték, amely a kiírás és a
   döntés között megváltozhatna (ADR-018 2. probléma).
2. **Minden nehézségi szint játszható**: a feladat aritmetikája és a jutalom mérete
   független, ezért a CL1-es `eredmény ≤ 10` korlát nem korlátozza a jutalmat
   (ADR-018 1. probléma).

### 3.3. Hibás választás következménye

`RewardDelta = 0`, azaz **az elmaradt jutalom a büntetés**, nem közvetlen HP- vagy pajzslevonás
(spec 9. szakasz és 12. szakasz: „a rossz válasz ne legyen megalázó"). Ez alól egyetlen kivétel
nincs az MVP-ben. Konfiguráció: `MathEventConfigSO.IncorrectRewardAmount = 0`.
Negatív jutalom az MVP-ben **nem létezik**: a `Minimize` cél sem von le semmit, csak
a *kisebb eredmény* a helyes válasz.

### 3.4. Kapu-célok és ikonjaik (spec 5.1 „a cél ne mindig a nagyobb legyen")

> **Kontrakt-megfeleltetés (ADR-009, ADR-019):** a kapucél a kódban **egyetlen
> adat**: `GateGoal` (az optimalizálás iránya). A jutalom erőforrása és összege
> ettől ortogonális, és mindig fix `Health` (`RewardSpec.Resource/FlatAmount`).
> Az alábbi címkék **prezentációs** elemek (ikon + string-tábla kulcs), nem enum-értékek.

| Prezentációs címke | Kontrakt (`Goal`) | String-kulcs | Ikon + felirat | Optimális | Példa | Mikor |
|---|---|---|---|---|---|---|
| „Válaszd a nagyobbat!" | `Maximize` | `ui.gate.goal_max` | 🟢 felfelé nyíl + szív a jutalomnál | nagyobb eredmény | `6: ×2 vs +25` → 12 vs 31 → `+25` nyer | W1-től |
| „Válaszd a kisebbet!" | `Minimize` | `ui.gate.goal_min` | 🔵 lefelé nyíl + szív a jutalomnál | **kisebb** eredmény | `48: ÷4 vs −30` → 12 vs 18 → `÷4` nyer | W3-tól |
| „Érd el a 60-at!" | `ReachTarget` | `ui.gate.goal_target` | 🎯 céltábla + célszám | célhoz legközelebbi | `cél 60, 15: ×4 vs +40` → 60 vs 55 → `×4` nyer | W2 boss, majd W5-től általánosan |

A célikon **mindig** megjelenik a kapupár felett, legalább 2 másodperccel a döntési pont előtt
(`MathEventConfigSO.GoalIconLeadSeconds = 2.0`), és soha nem csak színnel kódolt
(spec 12: ikon + szám + felirat együtt). A jutalom-kijelzés a fix összeg és a szív-ikon
(`+30 ♥`); **jutalom-TÍPUS választó ikon nincs** — az ADR-018 6. pontja ezt az F5-13
hatókörébe tolta (a jutalom jelenleg mindig életerő).

A korábbi „Több pajzsot!" (`Maximize × Shield`) és „Kevesebb páncél!"
(`Minimize × EnemyArmor`) **jutalom-párosok megszűntek** (ADR-018 3–4. pont): a pajzs
és a páncél matematikai feladatban nem szerepelhet. A `Minimize` cél megmaradt,
csak fix életerő-jutalommal.

### 3.5. Generátor-invariánsok (a `math-engine-dev` számára kötelező validáció)

1. `|eredményA − eredményB| >= MinGateGap` — `MathDifficultyTableSO.MinGateGap`, default **4**,
   CL1–3-on **8** — ne legyen „majdnem döntetlen", amit a gyerek nem tud biztonsággal eldönteni.
   Validátor-kód: `GATE_GAP_TOO_SMALL`. Ez minden opciópárra érvényes (3–4 kapu esetén is).
2. Döntetlen (`A == B`) tiltott (`GATE_TIE`); a `Goal` szerinti optimum pontosan egy opció.
3. **Minden kapu-szám generált operandus**: a bázisérték és a műveleti operandusok a
   `[MinOperandValue, MaxOperandValue]` ∩ `[GateOperandMin, GateOperandMax]` metszetből
   jönnek, és minden opció eredménye `<= MaxResultValue`, `>= 0`. Nincs cél-, profil- vagy
   bázisértékcsere a generálási kudarc elfedésére; kimerülés esetén őszinte
   `GENERATION_EXHAUSTED` (ADR-019).
4. A „nagyobb szám mindig nyer" heurisztika ne legyen eredményes: pályánként legalább **30%**
   olyan kapupár, ahol a nagyobb *műveleti operandus* nem a jobb választás (pl. `×2` vs `+25`
   6-nál) — `MathEventConfigSO.NonObviousGatePairRatioMin = 0.30` —, és legalább 1 nem-`Maximize`
   célú kapu a 3. világtól.
5. A kapupozíció (bal/jobb) egyenletesen random, max 2 azonos oldal egymás után
   (`MathEventConfigSO.MaxConsecutiveSameGateSide = 2`).
6. Két kapu `(művelet, operandus)` párja nem lehet azonos (`GATE_DUPLICATE_OPERATION`);
   ugyanaz az **operátor** eltérő operandussal megengedett (pl. `−1` vs `−9`), mert CL1-en
   ez gyakran az egyetlen érvényes megoldás.

---

## 4. Mechanika B — Eredményválasztó sávok (spec 5.2)

### 4.1. Működés és időzítés

A pálya 2–4 sávra oszlik. A kifejezés a képernyő **felső sávjában** jelenik meg nagy
betűkkel; a válaszokat ellenséges egységek vagy kapuk hordozzák, sávonként egy-egy.
Pontosan egy válasz helyes. A választás a karakter sávváltásával történik, **nincs kvízablak**
(spec 5.2).

Referencia-ütemezés CL5-ön (a spec 5.2 példája alapján, konfigurálható):

| t (s) | Esemény | Paraméter (`TimePressureTableSO` / `MathEventConfigSO`) |
|---|---|---|
| 0.0 | A kifejezés megjelenik a felső HUD-sávban | `MathEventScheduleEntry.PreviewDurationSeconds` |
| 1.5 | A válaszhordozó egységek belépnek a képernyő tetején | `AnswerSpawnDelaySeconds` = 1.5 |
| 1.5–8.0 | Harc folyik, a gyerek számol és sávot vált, **a döntés visszavonható** | `ThinkTimeSeconds` = 8.0 |
| 8.0 | Döntési vonal: amelyik sávban a karakter áll, az a válasz | `DecisionZoneDepth` |
| 8.0–9.5 | Visszajelzés: a helyes érték felvillan, a jutalom beszáll | `FeedbackDisplaySeconds` = 1.5 |

**Timeout-kezelés:** ha a karakter a döntési vonalnál „senkiföldjén" (két sáv között) áll, az
`Timeout`-nak számít — nincs jutalom, de nincs extra büntetés sem, és a timeout külön
statisztikaként megy a DDA-ba (időnyomás-probléma, nem tudásprobléma — spec 7.3).

### 4.2. Kidolgozott példa (spec 21/C elfogadási kritérium)

```
Feladat:  7 × 8 = ?
1. sáv: 54   2. sáv: 63   3. sáv: 56 ✅   4. sáv: 64
Helyes sáv jutalma: +15 életerő (RewardTableSO.HealthRewardBase, fix összeg)
Hibás sáv: 0 életerőjutalom + a helyes érték 1,5 s-ig zölden felvillan a 3. sáv felett
```

A hibás válaszok a spec 6.4. hibamodelljeit követik: `54` = szorzótábla-szomszéd (`6×9`),
`63` = `7×9`, `64` = `8×8` — mind „hihető" tévedés, nem véletlen szám.

**A két mechanika jutalma ugyanazzal a mechanizmussal működik** (fix életerő-összeg);
csak a konfigurált érték tér el: a kapu nagyobb tét
(`HealthRewardBase × GateRewardHealthMultiplier`, CL1-en 30), a sáv a `HealthRewardBase`
(CL1-en 15) — scriptable-object-schema.md 6.1.

### 4.3. Sávszám és nehézség

| Sávszám | Találgatási esély | Használat (`MathDifficultyTableSO.AnswerOptionCount`) |
|---|---|---|
| 2 | 50% | CL1–2; az ammo-mentő T3 kivezetve (ADR-017) |
| 3 | 33% | CL3–5, és T2 segítség-állapotban (`LaneCountOverride = 3`) |
| 4 | 25% | CL6–10 alapértelmezés |

A sávszám **nem** mehet 4 fölé: a mobil portré képernyőn 4 sávnál szélesebb elrendezésben a
célpontok 60 dp alá csökkennének (érinthetőségi minimum). A kontrakt ezt `2 – 4`
tartománnyal kényszeríti (`LevelDefinition.LaneCount`, `DifficultyProfile.AnswerOptionCount`).

---

## 5. Ellenségek

Vizuális megkülönböztethetőség szabálya: minden ellenségtípus **sziluettben, méretben és
mozgásritmusban** is eltér, nem csak színben (spec 12: ne csak színnel közvetíts információt).

A kontrakt `EnemyType` enumja **öt** archetípust tartalmaz (`Basic`, `Fast`, `Armored`, `Boss`,
`Swarmlet`) — ADR-008. A spec 4.1 „legalább négy típust" ír elő, az ötödik a horde-élmény
feltétele (lásd a táblázat alatti indoklást).

| Típus (`EnemyType`) | Sziluett / méret | Viselkedés | HP | Sebesség | Átjutási seb. (`ContactDamage`) | Lövés/kill | Fázis |
|---|---|---|---|---|---|---|---|
| `Basic` (Számfaló) | zömök kocka, 1.0× | egyenes, állandó tempó | 3 | 1.0× | 8 | 3 | F1 |
| `Fast` (Villanó) | keskeny, nyúlánk, 0.8× | 2× sebesség, cikcakkban sávot vált | 2 | 2.0× | 5 | 2 | F1 |
| `Armored` (Páncélos) | széles, bordázott pajzs, 1.4× | lassú; `FrontalDamageReduction = 0.6`, `ArmorPoints = 6`; a páncél **nem** matematikai úton gyengíthető (ADR-018) | 8 | 0.6× | 15 | 8–14 | F1 (HP/sebesség), **F5** (páncél-mezők) |
| `Swarmlet` (Rajzó) | apró, 0.5×, `SpawnGroupSize = 4` | kötelékben mozog, 1 HP, de sok | 1 | 1.3× | 3 | 1 | F1 |
| `Boss` (világonként egyedi) | 3.0×, egyedi modell | 3–5 fázis, lásd 5.4 | 120 (W1) … 250 (W7) | 0.3× | azonnali pályavesztés-kockázat | ~120 | F5 |

**Miért kell az ötödik típus (ADR-008 indoklása):** a horde-survival műfaj
élményének kulcsa a „tömeg lekaszálása". Egy 3 HP-s `Basic` ellenséget 3 lövés öl meg, így
4 lövés/s tűzgyorsaságnál másodpercenként legfeljebb ~1,3 ellenség hal meg — ez nem „horda",
hanem sorbanállás. Az 1 HP-s, kötelékben érkező `Swarmlet` adja a másodpercenkénti 4 kill
élményét; a lövések ideje és tényleges sebzése a kiválasztott fegyvertípustól függ. A `difficulty-model.md` 4. táblázata **CL2-től** számol
„rajzó aránnyal", tehát a típus nélkül a harci balance-tábla sem lenne megvalósítható.
A páncél-mezők (`ArmorPoints`, `FrontalDamageReduction`) F5-ben kerülnek be: F1–F4-ben az
`Armored` a megnövelt HP-val (8) és a lassú sebességgel (0.6×) differenciált.
**Nyitott F5-ös tétel:** mivel a páncél matematikai gyengítése (korábbi
`Minimize × EnemyArmor` kapu) az ADR-018 óta nem létezik, a páncél ellensúlyozása
F5-ben vagy erősebb fegyvertípussal (ADR-017), vagy új, jóváhagyott mechanikával
történik — ez új termékdöntést igényel.

**Az `EnemyType` enum bővítésének szabálya:** a `worlds.md` további archetípusokat vezet be
(`Splitter` W4, `Shifter` W5, `Mirror` W6, `Prime` W7, `Cipher` W8). Ezek **F5-ös** bővítések,
lefoglalt enum-számokkal — ld. `contracts/core-domain.md` 7. „EnemyType bővítési útiterv".
F1–F4-ben design-dokumentum sem tekintheti őket létező típusnak.

### 5.4. Boss — matematikai gyengepontok

A boss **nem sebezhető hagyományos lövéssel** 90%-ban: a sebzés 90%-át az adja, ha a gyerek a
3 gyengepont-feladatot megoldja. Ez biztosítja a spec 23/7. alapelvét („a győzelemhez valódi
matematikai teljesítmény szükséges"), és megakadályozza, hogy a folyamatos tüzelés önmagában
helyettesítse a boss matematikai gyengepontjainak megoldását.

| Fázis | HP-sáv (W1 boss) | Mechanika | Matek |
|---|---|---|---|
| 1 | 120 → 80 | Pajzs nyitva, 3 sávon Swarmlet-eső | 1 sáv-feladat, helyes → 40 sebzés |
| 2 | 80 → 40 | Pajzs zárva, csak a gyengepont sebezhető | 1 sáv-feladat, helyes → 40 sebzés; hibás → a pajzs 12 s-ig zárva marad |
| 3 | 40 → 0 | „Düh": 1.5× sebesség, rövidebb döntési ablak (−25%) | 1 sáv-feladat, helyes → 40 sebzés |

Konfiguráció: `EnemyDefinitionSO` (`Type = Boss`) + `BossPhaseEntry[]` ugyanazon az asseten
(`HpThreshold`, `ProblemType`, `DamageOnCorrect`, `PenaltyOnWrongSeconds`) — **nincs külön
`BossDefinition` asset** (scriptable-object-schema.md 1.1).

Hibás gyengepont-válasz esetén a feladat **újragenerálódik** (más számokkal, azonos készségre),
tehát a gyerek nem reked meg — csak időt és védelmi tartalékot veszít. Ez a „fokozatos segítség" pedagógiai
elv gyakorlati alkalmazása (spec 9.1).

> A boss-sebzés **nem** erőforrás-jutalom: a `DamageOnCorrect` közvetlen harci hatás,
> nem `RewardSpec`. Ezért nem sérti az ADR-018 „jutalom = fix életerő" szabályát.

---

## 6. Erőforrások

### 6.1. MVP erőforrások (Fázis 1)

| Erőforrás (`ResourceType`) | Kezdő (`Starting*`) | Max (`Max*`) | Fogyás | Forrás | Kifogyás következménye |
|---|---|---|---|---|---|
| `Health` (életerő) | 100 | 100 | átjutott ellenségenként 3–15 | **minden matematikai jutalom** (fix összeg), pálya végén teljes feltöltés, boss-fázisok közt +15 | pályavesztés (`HealthDepleted`) |
| `Shield` (pajzs) | 0 | 50 | a HP **előtt** fogy, 1:1 (`CombatBalanceSO.ShieldAbsorbsBeforeHealth`) | `LevelDefinition.StartingShield`, boss legyőzése (F5) — **matematikai feladat nem töltheti** (ADR-018) | – (csak puffer) |

Mindkét MVP-erőforrásnak saját Starting/Max mezője van: StartingHealth/MaxHealth és
StartingShield/MaxShield. MaxShield==0 esetén nincs pajzs-pool. A ResourcePool Max kemény
korlát; a levágott többlet elveszik (a fix jutalom tele életerőnél `WasClamped`, ami normál
működés). Nincs ammo pool, kill-refund, lőszerbudget vagy overflow-pontozás (ADR-017).

> **Nyitott balance-tétel (ADR-019 4.1.):** az ADR-018 után a pajzsnak az MVP-ben
> nincs matematikai forrása. A `LevelW1L01` jelenlegi `StartingShield = 0` /
> `MaxShield = 50` párosa ezért holt mechanika: a pajzs sosem lesz 0-nál nagyobb.
> Két lehetséges rendezés — **balance-döntés, nem kontraktkérdés**, és az F3-12
> nem igényli: (a) `StartingShield = 15..20` kezdő puffer a W1 pályákon, vagy
> (b) `MaxShield = 0` a W1-en, és a pajzs csak a boss-pályától (F5) jelenik meg.
> Döntésig a pajzs-HUD-réteg a `MaxShield > 0` feltétel miatt látszik, de mindig 0 —
> ezt a `LevelDefinitionSO.Validate()` legfeljebb `Warning`-ként jelzi.

### 6.2. Javasolt további erőforrások (a spec kéri a bővítést)

A `ResourceType` enum már most tartalmazza az `Energy` értéket a későbbi bővítésekhez;
új érték csak a lista végére kerülhet (core-domain.md 0.).

> **ADR-018 4. pont:** matematikai jutalom céljára **nem** vezetünk be új
> erőforrás-dimenziót. Az alábbi javaslatok ezért vagy harci/progressziós
> mechanikaként valósulhatnak meg, vagy új, jóváhagyott termékdöntést igényelnek.

| Javaslat | Mit csinál | Miért jó pedagógiailag | Fázis |
|---|---|---|---|
| `FocusCharge` (Fókusz-töltet) | 3 egymást követő **helyes** válasz tölti fel; aktiválva 5 s-ig lassított ellenségek | A *pontossági sorozatot* jutalmazza, nem a gyorsaságot (spec 12.). Nem matematikai feladat jutalma, hanem sorozat-mechanika — ezért nem sérti az ADR-018-at. | F5+ (`ResourceType.Energy`), új termékdöntéssel |
| `RepairKit` (Javítócsomag) | Legfeljebb 2 db hordozható, +30 HP, a gyerek dönti el, mikor használja | Erőforrás-tervezést vezet be a számolás mellé. **Az ADR-018 miatt átdolgozandó:** nem lehet matematikai feladat jutalma; pl. pályateljesítésért vagy csillagért adható. | F5+, új termékdöntéssel |
| `StarShards` (Csillagszilánk) | Meta-valuta pályateljesítésért és új készségek első elsajátításáért; fegyverekre és kinézetekre költhető | Teljesen determinisztikus bolt, nincs véletlen. A *rendszeres gyakorlást* jutalmazza, nem a hibátlanságot (spec 10.2). | 1 (`StarRatingSO.ShardsPerStar`), 2 (bolt) |

A `StarShards` **nem** `ResourceType`: nem session-erőforrás, hanem a `PlayerProgress`
meta-valutája (progresszió, nem harc).

Kifejezetten **elvetett** ötletek: energiarendszer / élet-limit (játékidő-korlátozás csak a
szülői felületről jöhet — spec 11.), véletlenszerű loot, időzített napi láda (spec 10.2 tiltja).

---

## 7. Jutalmazás és progresszió

### 7.1. Csillagok (pályánként 0–3)

Konfiguráció: `StarRatingSO` (scriptable-object-schema.md 14.); implementáció:
`ILevelProgressionService.CalculateStars` — **a küszöbök nincsenek kódba égetve**.

| Csillag | Feltétel | `StarRatingSO` mező | Indoklás |
|---|---|---|---|
| ★1 | A pálya teljesítése (`SessionResult.Victory`) | — | Minden gyerek elérje, aki végigcsinálta — ez a „rendszeres sikerélmény" (spec 23/7.) |
| ★2 | A matek-döntések **>= 70%-a** helyes | `Star2MinAccuracy = 0.70` | A célsáv (75–85%) alsó pereme alatt van, hogy a célsávban játszó gyerek biztosan megkapja |
| ★3 | ★2 **és** a döntések **>= 90%-a** helyes **és** 0 timeout | `Star3MinAccuracy = 0.90`, `Star3MaxTimeouts = 0` | Mesterfokú teljesítmény; nem követel hibátlanságot (10%-ig hibázhat) |

A csillagok **sebességtől függetlenek** — nincs „gyorsasági csillag", mert az a tippelést
jutalmazná (spec 12.). A kontrakt ezt kikényszeríti: `StarRatingSO`-ba idő-alapú mező
felvétele kontraktsértés.

### 7.2. Progressziós sávok

| Sáv | Mivel nyílik | Tartalom |
|---|---|---|
| Világok | az előző világ pályáinak >= 60%-a ★1-gyel teljesítve (`WorldUnlockRequirement.RequiredStarRatio = 0.60`), plusz világonként opcionális készség-előfeltétel | lásd `docs/balance/worlds.md` |
| Fegyverek | MVP: a pálya AvailableWeapons listájából, kezdettől elérhető; további progresszió F5-13 | eltérő konfigurált erősség, növekvő DPS is megengedett; két valós HUD-gombbal választható típus (ADR-017), új vásárlási/matek→fegyver szabály nélkül |
| Karakterkinézet | csillag-mérföldkövek (`StarRatingSO.SkinStarMilestones` = 10/25/50/100 ★) | tisztán kozmetikai |
| Képességek | új készség első `Mastered` fokozata (`SkillProfile.GetMasteryLevel`) | pl. „Kivonás mestere" → +1 másodperc gondolkodási idő minden kivonásos feladatnál |
| Vizuális fejlesztések | világonként | a bázis díszítése, pusztán jutalomérzet |

A „Képességek" sáv a legfontosabb pedagógiai hurok: **a gyerek a matematikai fejlődéséért kap
játékmechanikai erőt**, és pont abból a készségből, amelyben fejlődött.

### 7.3. Mi NINCS a játékban

Loot box, gacha, kártyacsomag, véletlen jutalom-pörgetés, energiarendszer, streak-vesztési
fenyegetés („elveszíted a 7 napos sorozatod!"), fizetős nehézségcsökkentés, külső analitika,
reklám. (Spec 10.2 és 12.)

---

## 8. Gyermekbarát UX és pedagógiai szabályok

| Szabály (spec 12.) | Konkrét design-megvalósítás |
|---|---|
| A rossz válasz ne legyen megalázó | Nincs piros X, nincs „HIBÁS!" felirat, nincs csalódott hangeffekt. A helyes érték zölden felvillan, a jutalom egyszerűen nem száll be. Szövegezés: *„56 volt. Jön a következő!"* (`ui.feedback.correct_was`) |
| Értelmes visszajelzés | Minden döntés után 1,5 s-os, a harcot nem megállító overlay: mindkét kapueredmény / a helyes érték + a jutalom. Részletes magyarázat a pálya végi eredményképernyőn, kérésre (spec 6.5). |
| Kiszámítható tét | A fix életerő-jutalom **a feladat előtt** látszik (`+30 ♥`), így a gyerek tudja, mi a tét, és a jutalom nem a számok véletlenjén múlik (ADR-018). |
| Rendszeres sikerélmény | DDA célja ~75–85% pontosság mellett a pályateljesítés (`GameBalanceSO.TargetAccuracyMin/Max`); lásd `docs/balance/difficulty-model.md` |
| Ne lehessen tippeléssel nyerni | 4 sáv (25%) + a tippelő profil szimulált pályateljesítési aránya cél szerint < 15% (`GameBalanceSO.GuessingSuccessRateCap`, lásd `SIM-plan.md`) |
| A válaszok ne árulják el magukat | A helyes válasz sávpozíciója egyenletes eloszlású; a distraktorok a 6.4. hibamodellekből jönnek, így nagyságrendben nem lógnak ki (`GUESSABLE_PATTERN` validátor) |
| Ne legyen rendszeresen túl bonyolult | Készség-előfeltétel-kapu: a generátor csak olyan készséget használ, amelyre `SkillProfile.GetMasteryLevel(tag) >= Introduced` (spec 7.2 utolsó bekezdése, ADR-010) |
| Lehetőség a javításra | A rontott feladattípus a következő 2 pályán emelt (de nem kizárólagos) gyakorisággal tér vissza, egy szinttel könnyebb számokkal |
| A gyorsaság csak a pontosság mellett számít | Nincs idő-alapú pontszorzó |
| Szünetjavaslat | 25 perc folyamatos játék után barátságos, elutasítható ajánlat (`ParentSettingsDefaultsSO.BreakSuggestionMinutes = 25`); 45 perc után a szülői limit érvényesül (`HardStopMinutes = 45`) |
| Nem csak szín | Minden HUD-elem: ikon + szám + rövid szöveg. A nehézségi sáv 1–10 számmal, a kapucél ikonnal és felirattal. |

---

## 9. HUD-elemek (design-szintű lista)

| Elem | Pozíció | Tartalom |
|---|---|---|
| Kihívásszint-sáv | felül középen | 10 fokozat, zöld→piros, **számmal** (spec 7.1); a szám a három dimenzió súlyozott átlaga (`AdaptiveDifficultyConfigSO.DisplayWeight*`) |
| Feladatsáv | felül, a szint alatt | a kifejezés nagy betűvel (sáv-feladat), vagy a kapucél ikon + a kapu **bázisértéke** (`GateBaseValue`) + a fix jutalom (`+30 ♥`) |
| Fegyverválasztó | bal alul | típusonként magyar gomb és aktív jelölés, legalább 48 dp; nincs ammo szám/csík |
| Élet / pajzs | jobb alul | szív-ikon + szám; a pajzs külön kék réteg (csak ha `MaxShield > 0`) |
| Hullámjelző | jobb felül | „3/8 hullám" (`WaveDirector.CurrentWaveIndex` / `TotalWaves`) |
| Visszajelzés-buborék | a döntési vonal felett | 1,5 s, nem blokkoló; mindkét kapueredmény és a jóváírt életerő |

---

## 10. Nyitott kérdések a további agenteknek

1. `math-engine-dev`: a 3.5. pont generátor-invariánsait unit tesztként is kérjük (property-based
   teszt: 10 000 generált kapupár, egyik sem sérti az 1–6. szabályt), **és** szintenként
   igazolt `GENERATION_EXHAUSTED`-mentesség a szállított kapu-korlátokkal (ADR-019).
2. `gameplay-dev`: a fix jutalom és a `MaxHealth`-vágás kölcsönhatását integrációs teszt
   fedje (spec 21/B): tele életerőnél a helyes válasz `WasClamped = true`-val is
   `Applied` státuszú.
3. ~~`architect`: a `FocusCharge` és `RepairKit` Fázis 2-es…~~ **Lezárva:** a
   `ResourceType` explicit numerikus értékekkel bővíthető; az `Energy` érték az
   `FocusCharge` számára lefoglalva, de az ADR-018 óta **nem** lehet matematikai jutalom.
4. `balance-designer` (**új, ADR-019 4.2.**): a fix életerő-jutalom (CL1: kapu 30, sáv 15)
   újramérése a `SIM-plan.md` szerint, mert az életerő lett minden matematikai jutalom
   egyetlen célja. A 6.1. pajzs-nyitott tételre is itt kell javaslat.

## 11. Első tényleges fegyverkészlet és a matek-integráció állapota

Az ADR-017 szerinti starter_blaster Damage5/FireRate3 (15 DPS) és heavy_blaster
Damage10/FireRate3 (30 DPS) a LevelW1L01 pályán kezdettől választható. A váltás
sessionparancs, nem jutalom. Gyors oda-vissza váltás nem ad extra lövést; a régi
lövedék változatlan. A HUD érintése nem választ matematikai sávot; pause alatt tiltott.
A két SO és a scene tényleges bekötése F1-21-ben, a végtelen tüzelés F1-20-ban elkészült.

**Matek-tartalom az ADR-018 után:** a `LevelW1L01` tíz eseménye öt összehasonlító kaput
és öt eredményválasztó sávot tartalmaz, **mindkettő `Health` jutalommal** (kapu 30, sáv 15 HP
CL1-en). A kapu-feladat operandusai önállóan generálódnak, ezért a korábbi
CL1/Shield50 lefedettségi akadály **szerkezetileg megszűnt** (ADR-018 1. probléma).
A tényleges scene-integráció, a tíz valódi feloldás és a teljes minőségi kapu
továbbra is **F3-12** feladata; ez a dokumentum nem állít kész funkciót.
