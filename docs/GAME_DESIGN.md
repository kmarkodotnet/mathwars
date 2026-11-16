# Math Horde (Számharcosok) — Game Design Document

> Státusz: ARCH fázis, v1.1 · Felelős: game-designer agent
> Kapcsolódó dokumentumok: `docs/ARCHITECTURE.md` (architect agent),
> `docs/contracts/scriptable-object-schema.md` (architect agent),
> `docs/TESTING.md` (qa-unity agent),
> `docs/balance/difficulty-model.md`, `docs/balance/worlds.md`, `docs/balance/SIM-plan.md`.
> Ez a dokumentum a spec 20. szakasz 13. pontja által kötelezően előírt leszállítandó.
> Minden itt szereplő szám **ScriptableObject-alapértelmezés**, nem kódba égetett konstans.
>
> **v1.1 (2026-11-16, architect-konszolidáció, ADR-009):** a kapucélok a kontrakt
> `GateGoal` × `ResourceType` felbontására hozva (3.4); a `Swarmlet` ellenségtípus
> **elfogadva** és a kontraktba felvéve (ADR-008); a „soft cap” fogalom tisztázva (6.1);
> kezdő lőszer 60-ra egységesítve a lőszer-költségvetéssel; a csillagküszöbök
> `StarRatingSO`-ra hivatkoznak.
>
> **Dokumentum-hierarchia:** ütközés esetén a `docs/contracts/*` az irányadó; ez a
> dokumentum a *miért*-et és az értékeket adja, a kontrakt a *nevet és a típust* (ADR-009).

---

## 1. Játékkoncepció és célközönség

**Egy mondatban:** a játékos egy automatikusan tüzelő harcost irányít a képernyő alján,
balra-jobbra húzva sávot vált, és a felülről érkező hordát csak akkor tudja feltartóztatni,
ha a közeledő matematikai kapukon és válaszsávokon helyesen dönt — a lőszer a fejszámolás
közvetlen jutalma.

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

---

## 2. Alapvető játékhurok (core loop)

### 2.1. Mikro-loop (3–8 másodperc, folyamatosan ismétlődik)

```
ellenség közeledik → auto-tüzelés fogyasztja a lőszert → matek-elem jelenik meg a pálya felső
harmadában → a gyerek fejben számol, miközben továbbra is lő és kerülget → sávot vált →
áthalad a kapun/sávon → azonnali erőforrás-visszajelzés → következő hullám
```

### 2.2. Makro-loop egy ~4 perces pályán (240 s, referencia CL5 nehézségen)

A spec 10.1. szakaszának 9 lépése konkrét idősávokra bontva. A „Matek-döntés" oszlop összege
**14 döntési helyzet**, ami teljesíti a 10.1. minimum 10-es követelményét tartalékkal, és
megegyezik a `difficulty-model.md` 5.3. táblázatának CL5-ös `MathDecisionCount` értékével.

| Szakasz | Idő (s) | Tartalom | Matek-döntés | Kumulált |
|---|---|---|---|---|
| S0 Bevezetés | 0–12 | Első, lassú alap-ellenségek, nincs matek — a gyerek megtanulja a sávváltást | 0 | 0 |
| S1 Első kapu | 12–35 | Erőforrás-módosító kapu (2 kapu), közben 1. hullám | 1 gate | 1 |
| S2 Harc | 35–60 | 2. hullám, gyors ellenségek bevezetése | 1 lane | 2 |
| S3 Sáv-blokk | 60–95 | 2 db eredményválasztó sáv-feladat, 3. hullám | 2 lane | 4 |
| S4 Vegyes | 95–135 | 1 kapu (`Minimize` × `EnemyArmor`) + 2 sáv, páncélos ellenség | 1 gate + 2 lane | 7 |
| S5 Nyomás | 135–175 | Sűrű 5. hullám, 2 sáv rövidebb döntési ablakkal | 2 lane | 9 |
| S6 Felkészülés | 175–195 | „Tankoló" kapu (`Maximize` × `Ammo`) + 1 sáv a boss előtt | 1 gate + 1 lane | 11 |
| S7 Boss | 195–235 | Főellenség, 3 gyengepont-feladat (lásd 5.4) | 3 lane | 14 |
| S8 Eredmény | 235–250 | Eredményképernyő, csillagok, hibajavító áttekintés | – | 14 |

> **Világ-korlát:** az S4 páncél-kapuja csak olyan pályán szerepelhet, amelynek világa
> `EnabledGateGoals`-ban tartalmazza a `Minimize`-t (W3-tól — `worlds.md` 3.). A W1–W2
> pályáin ez a szakasz `Maximize × Ammo` vagy (W2-től) `Maximize × Shield` kapura cserélődik.
> Ez a táblázat **sablon**, nem konkrét pálya.

**Tervezési szabályok a loopra:**
- Két egymást követő matek-döntés között legyen **legalább 8 s** tiszta harc, különben a gyerek
  kognitívan túlterhelődik és a lövöldözős játékélmény eltűnik (spec 23/1.). Konfiguráció:
  `TimePressureTableSO.MinGapBetweenProblemsSeconds` (CL-enként 8–14 s).
- Egy pályán **legfeljebb 2 egymást követő** azonos típusú feladat, hogy a két kötelező mechanika
  váltakozzon (spec 23/3.). Konfiguráció: `MathEventConfigSO.MaxConsecutiveSameProblemType`.
- A pálya utolsó 15 másodperce matek-mentes „győzelmi" szakasz: a gyerek élvezheti a felhalmozott
  lőszer elköltését.
- Pályahossz-paraméter: `LevelDefinitionSO.TargetDurationSeconds` (default 240, tartomány 180–300;
  az SO tartománya 60–600, mert a `Tutorial` sablon 150 s).
  Az MVP első világában 150 s is elfogadható (spec 10.1 utolsó bekezdése).

---

## 3. Mechanika A — Erőforrás-módosító kapuk (spec 5.1)

### 3.1. Működés

A pálya felső részén 2 (ritkábban 3) kapu ereszkedik lefelé, mindegyiken egy **művelet** van
kiírva (pl. `×2`, `+25`). A kapu felett a HUD mutatja a **snapshot-értéket** és a **célikont**.
A kapuk eredménye a döntés előtt **nem látható** (spec 5.1), csak az áthaladás utáni
visszajelzésben.

### 3.2. Erőforrás-snapshot — a mechanika lelke

```
Feladat létrehozásakor:      SnapshotValue = CurrentAmmo          (pl. 20)
Áthaladáskor:                RewardDelta   = OperationResult − SnapshotValue
Új erőforrás:                CurrentAmmo   = CurrentAmmo + RewardDelta
```

**Kidolgozott példa (a spec 21/B elfogadási kritériuma):**

| Lépés | Érték |
|---|---|
| Lőszer a kapu megjelenésekor (snapshot) | 20 |
| Bal kapu `×2` → eredmény | 40 → delta **+20** |
| Jobb kapu `+25` → eredmény | 45 → delta **+25** ✅ optimális |
| A gyerek közben 12 lövést ad le, tényleges lőszer | 8 |
| Jobb kapun áthalad | 8 + 25 = **33** |
| Ha a bal kaput választja | 8 + 0 = **8** (hibás választás → nincs matematikai jutalom) |

A snapshot azért kell, mert enélkül a kapu „felülírná" a harc közben elfogyasztott lőszert, és
a gyerek megtanulná, hogy érdemes a kapu előtt üresre lőni a tárat. **A kapu soha nem állít be
abszolút értéket, mindig deltát ad.**

### 3.3. Hibás választás következménye

`RewardDelta = 0`, azaz **az elmaradt jutalom a büntetés**, nem közvetlen HP- vagy lőszerlevonás
(spec 9. szakasz és 12. szakasz: „a rossz válasz ne legyen megalázó"). Ez alól egyetlen kivétel
nincs az MVP-ben. Konfiguráció: `MathEventConfigSO.IncorrectRewardAmount = 0`.
Negatív delta csak akkor fordulhat elő, ha maga a feladat célja a csökkentés
(`Minimize × EnemyArmor` kapu), és ott a *jutalom* a nagyobb csökkenés.

### 3.4. Kapu-célok és ikonjaik (spec 5.1 „a cél ne mindig a nagyobb legyen")

> **Kontrakt-megfeleltetés (ADR-009):** a kapucél a kódban **két ortogonális adat**:
> `GateGoal` (az optimalizálás iránya) × `ResourceType` (melyik erőforrásról van szó).
> Az alábbi `MoreAmmo` / `LessEnemyArmor` / `MoreShield` nevek **prezentációs címkék**
> (ikon + string-tábla kulcs), **nem enum-értékek**.

| Prezentációs címke | Kontrakt-pár (`Goal` × `Resource`) | String-kulcs | Ikon + felirat | Optimális | Példa | Mikor |
|---|---|---|---|---|---|---|
| „Több lőszert!" | `Maximize` × `Ammo` | `ui.gate.more_ammo` | 🟡 felfelé nyíl + töltény | nagyobb eredmény | `20: ×2 vs +25` | minden világ |
| „Kevesebb páncél!" | `Minimize` × `EnemyArmor` | `ui.gate.less_armor` | 🔵 lefelé nyíl + pajzs | **kisebb** eredmény | `48: ÷4 vs −30` → 12 vs 18 → `÷4` nyer | W3-tól |
| „Érd el a 60-at!" | `ReachTarget` × (bármi) | `ui.gate.reach_target` | 🎯 céltábla + szám | célhoz legközelebbi | `cél 60, 15: ×4 vs +40` → 60 vs 55 | W2 boss, majd W5-től általánosan |
| „Több pajzsot!" | `Maximize` × `Shield` | `ui.gate.more_shield` | 🟢 pajzs + plusz | nagyobb eredmény | `10: +12 vs ×2` | W2-től |

A célikon **mindig** megjelenik a kapupár felett, legalább 2 másodperccel a döntési pont előtt
(`MathEventConfigSO.GoalIconLeadSeconds = 2.0`), és soha nem csak színnel kódolt
(spec 12: ikon + szám + felirat együtt).

### 3.5. Generátor-invariánsok (a `math-engine-dev` számára kötelező validáció)

1. `|eredményA − eredményB| >= MinGateGap` — `MathDifficultyTableSO.MinGateGap`, default **4**,
   CL1–3-on **8** — ne legyen „majdnem döntetlen", amit a gyerek nem tud biztonsággal eldönteni.
   Validátor-kód: `GATE_GAP_TOO_SMALL`.
2. Döntetlen (`A == B`) tiltott (`GATE_TIE`).
3. Mindkét eredmény legyen `>= 0` és `<= LevelDefinition.MaxAmmo` (200), különben a kapacitás-vágás
   megfordítaná a tényleges előnyt (spec 5.1 utolsó bekezdés, `GATE_ORDER_INVERTED_BY_CAP`).
   Ha a snapshot magas, a generátor kisebb műveleteket választ, vagy `ReachTarget` célra vált.
4. A „nagyobb szám mindig nyer" heurisztika ne legyen eredményes: pályánként legalább **30%**
   olyan kapupár, ahol a nagyobb *operandus* nem a jobb választás (pl. `×2` vs `+25` 20-nál) —
   `MathEventConfigSO.NonObviousGatePairRatioMin = 0.30` —, és legalább 1 nem-`Maximize` célú
   kapu a 3. világtól.
5. A kapupozíció (bal/jobb) egyenletesen random, max 2 azonos oldal egymás után
   (`MathEventConfigSO.MaxConsecutiveSameGateSide = 2`).

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
Helyes sáv jutalma: +32 lőszer (RewardTableSO.LaneRewardBase, CL4)
Hibás sáv: 0 lőszer + a helyes érték 1,5 s-ig zölden felvillan a 3. sáv felett
```

A hibás válaszok a spec 6.4. hibamodelljeit követik: `54` = szorzótábla-szomszéd (`6×9`),
`63` = `7×9`, `64` = `8×8` — mind „hihető" tévedés, nem véletlen szám.

### 4.3. Sávszám és nehézség

| Sávszám | Találgatási esély | Használat (`MathDifficultyTableSO.AnswerOptionCount`) |
|---|---|---|
| 2 | 50% | CL1–2, és `InLevelAssistSO` T3 (`LaneCountOverride = 2`) |
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
| `Armored` (Páncélos) | széles, bordázott pajzs, 1.4× | lassú; `FrontalDamageReduction = 0.6`, `ArmorPoints = 6`; a páncél a `Minimize × EnemyArmor` kapukkal gyengíthető | 8 | 0.6× | 15 | 8–14 | F1 (HP/sebesség), **F5** (páncél-mezők) |
| `Swarmlet` (Rajzó) | apró, 0.5×, `SpawnGroupSize = 4` | kötelékben mozog, 1 HP, de sok | 1 | 1.3× | 3 | 1 | F1 |
| `Boss` (világonként egyedi) | 3.0×, egyedi modell | 3–5 fázis, lásd 5.4 | 120 (W1) … 250 (W7) | 0.3× | azonnali pályavesztés-kockázat | ~120 | F5 |

**Miért kell az ötödik típus (ADR-008 indoklása):** a horde-survival műfaj
élményének kulcsa a „tömeg lekaszálása". Egy 3 HP-s `Basic` ellenséget 3 lövés öl meg, így
4 lövés/s tűzgyorsaságnál másodpercenként legfeljebb ~1,3 ellenség hal meg — ez nem „horda",
hanem sorbanállás. Az 1 HP-s, kötelékben érkező `Swarmlet` adja a másodpercenkénti 4 kill
élményét anélkül, hogy a lőszer-költségvetés felborulna (4 lőszer = 4 kill, miközben egy
`Armored` 14 lőszert kér). A `difficulty-model.md` 4. táblázata **CL2-től** számol
„rajzó aránnyal", tehát a típus nélkül a harci balance-tábla sem lenne megvalósítható.
A páncél-mezők (`ArmorPoints`, `FrontalDamageReduction`) F5-ben kerülnek be: F1–F4-ben az
`Armored` a megnövelt HP-val (8) és a lassú sebességgel (0.6×) differenciált.

**Az `EnemyType` enum bővítésének szabálya:** a `worlds.md` további archetípusokat vezet be
(`Splitter` W4, `Shifter` W5, `Mirror` W6, `Prime` W7, `Cipher` W8). Ezek **F5-ös** bővítések,
lefoglalt enum-számokkal — ld. `contracts/core-domain.md` 7. „EnemyType bővítési útiterv".
F1–F4-ben design-dokumentum sem tekintheti őket létező típusnak.

### 5.4. Boss — matematikai gyengepontok

A boss **nem sebezhető hagyományos lövéssel** 90%-ban: a sebzés 90%-át az adja, ha a gyerek a
3 gyengepont-feladatot megoldja. Ez biztosítja a spec 23/7. alapelvét („a győzelemhez valódi
matematikai teljesítmény szükséges"), és megakadályozza, hogy a felhalmozott lőszerrel
matekmentesen le lehessen darálni a bosst.

| Fázis | HP-sáv (W1 boss) | Mechanika | Matek |
|---|---|---|---|
| 1 | 120 → 80 | Pajzs nyitva, 3 sávon Swarmlet-eső | 1 sáv-feladat, helyes → 40 sebzés |
| 2 | 80 → 40 | Pajzs zárva, csak a gyengepont sebezhető | 1 sáv-feladat, helyes → 40 sebzés; hibás → a pajzs 12 s-ig zárva marad |
| 3 | 40 → 0 | „Düh": 1.5× sebesség, rövidebb döntési ablak (−25%) | 1 sáv-feladat, helyes → 40 sebzés |

Konfiguráció: `EnemyDefinitionSO` (`Type = Boss`) + `BossPhaseEntry[]` ugyanazon az asseten
(`HpThreshold`, `ProblemType`, `DamageOnCorrect`, `PenaltyOnWrongSeconds`) — **nincs külön
`BossDefinition` asset** (scriptable-object-schema.md 1.1).

Hibás gyengepont-válasz esetén a feladat **újragenerálódik** (más számokkal, azonos készségre),
tehát a gyerek nem reked meg — csak időt és lőszert veszít. Ez a „fokozatos segítség" pedagógiai
elv gyakorlati alkalmazása (spec 9.1).

---

## 6. Erőforrások

### 6.1. MVP erőforrások (Fázis 1)

| Erőforrás (`ResourceType`) | Kezdő (`Starting*`) | Max (`Max*`) | Fogyás | Forrás | Kifogyás következménye |
|---|---|---|---|---|---|
| `Ammo` (lőszer) | **60** | **200** | 1 / lövés, 4 lövés/s tüzelés közben | matek-jutalom (>=85%), kill-refund +1/kill (<=15%) | a karakter nem lő → ellenségek átjutnak (**nem vereség** — spec 1.3) |
| `Health` (életerő) | 100 | 100 | átjutott ellenségenként 3–15 | pálya végén teljes feltöltés, boss-fázisok közt +15 | pályavesztés (`HealthDepleted`) |
| `Shield` (pajzs) | 0 | 50 | a HP **előtt** fogy, 1:1 (`CombatBalanceSO.ShieldAbsorbsBeforeHealth`) | `Maximize × Shield` kapuk, boss legyőzése | – (csak puffer) |

Mindhárom erőforrásnak **saját `Starting*` és `Max*` mezője** van a `LevelDefinition`-ben
(`StartingAmmo`/`MaxAmmo`, `StartingHealth`/`MaxHealth`, `StartingShield`/`MaxShield`) —
spec 4.3. A pajzs `Max`-ja **nem** vezethető le a kezdőértékből, mert tipikusan 0-ról indul
és kapukkal töltődik. `MaxShield == 0` ⇒ a pályán nincs pajzs-mechanika.

**A „soft cap” pontos jelentése (kontrakt-megfeleltetés):** a `MaxAmmo = 200` **kemény**
felső korlát a `ResourcePool`-ban — a `Current` soha nem lépi túl (core-domain.md 5.).
A „felesleg nem vész el" annyit jelent, hogy a **vágott** (`WasClamped == true`) mennyiséget
a pálya végi pontszámítás 1 lőszer = 2 pont árfolyamon jóváírja
(`AmmoBudgetSO.OverflowScorePerAmmo = 2`, **Fázis 5**; F1–F4-ben a felesleg elveszik).
Ezzel a jól számoló gyereknek a felesleg is ér valamit, ugyanakkor a cap megakadályozza,
hogy az 1. perc túlteljesítése matekmentessé tegye a 4. percet.
A cap soha nem vághatja le egy kapu jutalmát a döntés pillanatában (3.5 / 3. invariáns).

### 6.2. Javasolt további erőforrások (a spec kéri a bővítést)

A `ResourceType` enum már most tartalmazza az `Energy` értéket a Fázis 2-es bővítésekhez;
új érték csak a lista végére kerülhet (core-domain.md 0.).

| Javaslat | Mit csinál | Miért jó pedagógiailag | Fázis |
|---|---|---|---|
| `FocusCharge` (Fókusz-töltet) | 3 egymást követő **helyes** válasz tölti fel; aktiválva 5 s-ig korlátlan lőszer + lassított ellenségek | A *pontossági sorozatot* jutalmazza, nem a gyorsaságot (spec 12: „a gyorsaság csak a pontosság mellett kapjon jelentőséget"). Erős pozitív megerősítés, ami nem szerencsén múlik. | 2 (`ResourceType.Energy` hordozza) |
| `RepairKit` (Javítócsomag) | Legfeljebb 2 db hordozható, +30 HP, a gyerek dönti el, mikor használja | Bevezeti az **erőforrás-tervezést** a számolás mellé; a mobilos horde-műfaj szokásos „mikor költsem el" döntése. Nem random drop, hanem `ReachTarget` kapuk pontos eltalálásáért jár — tehát matematikai pontosság jutalma. | 2 (új `ResourceType`-érték, 5) |
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
| Fegyverek | `StarShards`-ért vásárolva (fix ár, nincs véletlen) | eltérő tüzelési minta (szórás, átütő, gyorstüzelő) — **a DPS-ük azonos** (`GameBalanceSO.ReferenceDps` ± 5%), csak a stílus más, hogy ne legyen „pay-to-win"-jellegű hatalmi lépcső |
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
| Rendszeres sikerélmény | DDA célja ~75–85% pontosság mellett a pályateljesítés (`GameBalanceSO.TargetAccuracyMin/Max`); lásd `docs/balance/difficulty-model.md` |
| Ne lehessen tippeléssel nyerni | 4 sáv (25%) + a tippelő profil szimulált pályateljesítési aránya cél szerint < 15% (`GameBalanceSO.GuessingSuccessRateCap`, lásd `SIM-plan.md`) |
| A válaszok ne árulják el magukat | A helyes válasz sávpozíciója egyenletes eloszlású; a distraktorok a 6.4. hibamodellekből jönnek, így nagyságrendben nem lógnak ki (`GUESSABLE_PATTERN` validátor) |
| Ne legyen rendszeresen túl bonyolult | Készség-előfeltétel-kapu: a generátor csak olyan készséget használ, amelyre `SkillProfile.GetMasteryLevel(tag) >= Introduced` (spec 7.2 utolsó bekezdése, ADR-010) |
| Lehetőség a javításra | A rontott feladattípus a következő 2 pályán emelt (de nem kizárólagos) gyakorisággal tér vissza, egy szinttel könnyebb számokkal |
| A gyorsaság csak a pontosság mellett számít | Nincs idő-alapú pontszorzó; a gyorsaság csak a `FocusCharge` feltöltődésén keresztül hat, ami helyes válaszokat igényel |
| Szünetjavaslat | 25 perc folyamatos játék után barátságos, elutasítható ajánlat (`ParentSettingsDefaultsSO.BreakSuggestionMinutes = 25`); 45 perc után a szülői limit érvényesül (`HardStopMinutes = 45`) |
| Nem csak szín | Minden HUD-elem: ikon + szám + rövid szöveg. A nehézségi sáv 1–10 számmal, a kapucél ikonnal és felirattal. |

---

## 9. HUD-elemek (design-szintű lista)

| Elem | Pozíció | Tartalom |
|---|---|---|
| Kihívásszint-sáv | felül középen | 10 fokozat, zöld→piros, **számmal** (spec 7.1); a szám a három dimenzió súlyozott átlaga (`AdaptiveDifficultyConfigSO.DisplayWeight*`) |
| Feladatsáv | felül, a szint alatt | a kifejezés nagy betűvel, vagy a kapucél ikon + snapshot |
| Lőszer | bal alul | ikon + szám + vízszintes csík (`Current` / `Max`) |
| Élet / pajzs | jobb alul | szív-ikon + szám; a pajzs külön kék réteg (csak ha `MaxShield > 0`) |
| Hullámjelző | jobb felül | „3/8 hullám" (`WaveDirector.CurrentWaveIndex` / `TotalWaves`) |
| Visszajelzés-buborék | a döntési vonal felett | 1,5 s, nem blokkoló |

---

## 10. Nyitott kérdések a további agenteknek

1. `math-engine-dev`: a 3.5. pont generátor-invariánsait unit tesztként is kérjük (property-based
   teszt: 10 000 generált kapupár, egyik sem sérti az 1–5. szabályt).
2. `gameplay-dev`: a `RewardDelta` számítás és a kapacitás-vágás kölcsönhatását integrációs teszt
   fedje (spec 21/B).
3. ~~`architect`: a `FocusCharge` és `RepairKit` Fázis 2-es, de a `ResourceType` enumot már most
   érdemes bővíthetőre tervezni.~~ **Lezárva:** a `ResourceType` explicit numerikus értékekkel
   bővíthető, az `Energy` érték a `FocusCharge` számára már lefoglalva (core-domain.md 5.).
