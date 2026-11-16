# Math Horde (Számharcosok) — Game Design Document

> Státusz: ARCH fázis, v1.0 · Felelős: game-designer agent
> Kapcsolódó dokumentumok: `docs/ARCHITECTURE.md` (architect agent),
> `docs/contracts/scriptable-object-schema.md` (architect agent),
> `docs/TESTING.md` (qa-unity agent),
> `docs/balance/difficulty-model.md`, `docs/balance/worlds.md`, `docs/balance/SIM-plan.md`.
> Ez a dokumentum a spec 20. szakasz 13. pontja által kötelezően előírt leszállítandó.
> Minden itt szereplő szám **ScriptableObject-alapértelmezés**, nem kódba égetett konstans.

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
**14 döntési helyzet**, ami teljesíti a 10.1. minimum 10-es követelményét tartalékkal.

| Szakasz | Idő (s) | Tartalom | Matek-döntés | Kumulált |
|---|---|---|---|---|
| S0 Bevezetés | 0–12 | Első, lassú alap-ellenségek, nincs matek — a gyerek megtanulja a sávváltást | 0 | 0 |
| S1 Első kapu | 12–35 | Erőforrás-módosító kapu (2 kapu), közben 1. hullám | 1 gate | 1 |
| S2 Harc | 35–60 | 2. hullám, gyors ellenségek bevezetése | 1 lane | 2 |
| S3 Sáv-blokk | 60–95 | 2 db eredményválasztó sáv-feladat, 3. hullám | 2 lane | 4 |
| S4 Vegyes | 95–135 | 1 kapu (cél: *kevesebb ellenséges páncél*) + 2 sáv, páncélos ellenség | 1 gate + 2 lane | 7 |
| S5 Nyomás | 135–175 | Sűrű 5. hullám, 2 sáv rövidebb döntési ablakkal | 2 lane | 9 |
| S6 Felkészülés | 175–195 | „Tankoló" kapu (cél: *több lőszer*) + 1 sáv a boss előtt | 1 gate + 1 lane | 11 |
| S7 Boss | 195–235 | Főellenség, 3 gyengepont-feladat (lásd 5.4) | 3 lane | 14 |
| S8 Eredmény | 235–250 | Eredményképernyő, csillagok, hibajavító áttekintés | – | 14 |

**Tervezési szabályok a loopra:**
- Két egymást követő matek-döntés között legyen **legalább 8 s** tiszta harc, különben a gyerek
  kognitívan túlterhelődik és a lövöldözős játékélmény eltűnik (spec 23/1.).
- Egy pályán **legfeljebb 2 egymást követő** azonos típusú feladat, hogy a két kötelező mechanika
  váltakozzon (spec 23/3.).
- A pálya utolsó 15 másodperce matek-mentes „győzelmi" szakasz: a gyerek élvezheti a felhalmozott
  lőszer elköltését.
- Pályahossz-paraméter: `LevelDefinition.targetDurationSec` (default 240, tartomány 180–300).
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
nincs az MVP-ben. Negatív delta csak akkor fordulhat elő, ha maga a feladat célja a csökkentés
(páncél-kapu), és ott a *jutalom* a nagyobb csökkenés.

### 3.4. Kapu-célok és ikonjaik (spec 5.1 „a cél ne mindig a nagyobb legyen")

| GateGoal | Ikon + felirat | Optimális | Példa | Mikor |
|---|---|---|---|---|
| `MoreAmmo` | 🟡 felfelé nyíl + töltény, „Több lőszert!" | nagyobb eredmény | `20: ×2 vs +25` | minden világ |
| `LessEnemyArmor` | 🔵 lefelé nyíl + pajzs, „Kevesebb páncél!" | **kisebb** eredmény | `48: ÷4 vs −30` → 12 vs 18 → `÷4` nyer | W3-tól |
| `ReachTarget` | 🎯 céltábla + szám, „Érd el a 60-at!" | célhoz legközelebbi | `cél 60, 15: ×4 vs +40` → 60 vs 55 | W5-től |
| `MoreShield` | 🟢 pajzs + plusz, „Több pajzsot!" | nagyobb eredmény | `10: +12 vs ×2` | W2-től |

A célikon **mindig** megjelenik a kapupár felett, legalább 2 másodperccel a döntési pont előtt,
és soha nem csak színnel kódolt (spec 12: ikon + szám + felirat együtt).

### 3.5. Generátor-invariánsok (a `math-engine-dev` számára kötelező validáció)

1. `|résultatA − résultatB| ≥ minGateGap` (default **4**, CL1–3-on **8**) — ne legyen „majdnem
   döntetlen", amit a gyerek nem tud biztonsággal eldönteni.
2. Döntetlen (`A == B`) tiltott.
3. Mindkét eredmény legyen `≥ 0` és `≤ AmmoSoftCap` (200), különben a cap megfordítaná a
   tényleges előnyt (spec 5.1 utolsó bekezdés). Ha a snapshot magas, a generátor kisebb
   műveleteket választ, vagy `ReachTarget` célra vált.
4. A „nagyobb szám mindig nyer" heurisztika ne legyen eredményes: pályánként legalább **30%**
   olyan kapupár, ahol a nagyobb *operandus* nem a jobb választás (pl. `×2` vs `+25` 20-nál),
   és legalább 1 nem-`MoreAmmo` célú kapu a 3. világtól.
5. A kapupozíció (bal/jobb) egyenletesen random, max 2 azonos oldal egymás után.

---

## 4. Mechanika B — Eredményválasztó sávok (spec 5.2)

### 4.1. Működés és időzítés

A pálya 2–4 sávra oszlik. A kifejezés a képernyő **felső sávjában** jelenik meg nagy
betűkkel; a válaszokat ellenséges egységek vagy kapuk hordozzák, sávonként egy-egy.
Pontosan egy válasz helyes. A választás a karakter sávváltásával történik, **nincs kvízablak**
(spec 5.2).

Referencia-ütemezés CL5-ön (a spec 5.2 példája alapján, konfigurálható):

| t (s) | Esemény | Paraméter |
|---|---|---|
| 0.0 | A kifejezés megjelenik a felső HUD-sávban | `problemRevealLead` |
| 1.5 | A válaszhordozó egységek belépnek a képernyő tetején | `answerSpawnDelay` = 1.5 |
| 1.5–8.0 | Harc folyik, a gyerek számol és sávot vált, **a döntés visszavonható** | `thinkTimeSec` = 8.0 |
| 8.0 | Döntési vonal: amelyik sávban a karakter áll, az a válasz | `decisionLineY` |
| 8.0–9.5 | Visszajelzés: a helyes érték felvillan, a jutalom beszáll | `feedbackDurationSec` = 1.5 |

**Timeout-kezelés:** ha a karakter a döntési vonalnál „senkiföldjén" (két sáv között) áll, az
`Timeout`-nak számít — nincs jutalom, de nincs extra büntetés sem, és a timeout külön
statisztikaként megy a DDA-ba (időnyomás-probléma, nem tudásprobléma — spec 7.3).

### 4.2. Kidolgozott példa (spec 21/C elfogadási kritérium)

```
Feladat:  7 × 8 = ?
1. sáv: 54   2. sáv: 63   3. sáv: 56 ✅   4. sáv: 64
Helyes sáv jutalma: +30 lőszer (CL4 lane-reward)
Hibás sáv: 0 lőszer + a helyes érték 1,5 s-ig zölden felvillan a 3. sáv felett
```

A hibás válaszok a spec 6.4. hibamodelljeit követik: `54` = szorzótábla-szomszéd (`6×9`),
`63` = `7×9`, `64` = `8×8` — mind „hihető" tévedés, nem véletlen szám.

### 4.3. Sávszám és nehézség

| Sávszám | Találgatási esély | Használat |
|---|---|---|
| 2 | 50% | CL1–2, és minden `Tier3` segítség-állapotban |
| 3 | 33% | CL3–5 |
| 4 | 25% | CL6–10 alapértelmezés |

A sávszám **nem** mehet 4 fölé: a mobil portré képernyőn 4 sávnál szélesebb elrendezésben a
célpontok 60 dp alá csökkennének (érinthetőségi minimum).

---

## 5. Ellenségek

Vizuális megkülönböztethetőség szabálya: minden ellenségtípus **sziluettben, méretben és
mozgásritmusban** is eltér, nem csak színben (spec 12: ne csak színnel közvetíts információt).

| Típus | Sziluett / méret | Viselkedés | HP | Sebesség | Átjutási seb. | Lövés/kill |
|---|---|---|---|---|---|---|
| `Basic` (Számfaló) | zömök kocka, 1.0× | egyenes, állandó tempó | 3 | 1.0× | 8 | 3 |
| `Fast` (Villanó) | keskeny, nyúlánk, 0.8× | 2× sebesség, cikcakkban sávot vált | 2 | 2.0× | 5 | 2 |
| `Armored` (Páncélos) | széles, bordázott pajzs, 1.4× | lassú, elölről 60% sebzéscsökkentés; a pajzs a `LessEnemyArmor` kapukkal gyengíthető | 8 (+pajzs 6) | 0.6× | 15 | 8–14 |
| `Swarmlet` (Rajzó) | apró, 0.5×, 4–6 fős kötelékben | együtt mozog, 1 HP, de sok | 1 | 1.3× | 3 | 1 |
| `Boss` (világonként egyedi) | 3.0×, egyedi modell | 3 fázis, lásd 5.4 | 120 | 0.3× | azonnali pályavesztés-kockázat | ~120 |

A `Swarmlet` a tervem kiegészítése a spec minimum 4 típusához: a horde-survival műfaj
élményének kulcsa a „tömeg lekaszálása", amit 3 HP-s ellenségekkel nem lehet elérni.

### 5.4. Boss — matematikai gyengepontok

A boss **nem sebezhető hagyományos lövéssel** 90%-ban: a sebzés 90%-át az adja, ha a gyerek a
3 gyengepont-feladatot megoldja. Ez biztosítja a spec 23/7. alapelvét („a győzelemhez valódi
matematikai teljesítmény szükséges"), és megakadályozza, hogy a felhalmozott lőszerrel
matekmentesen le lehessen darálni a bosst.

| Fázis | HP-sáv | Mechanika | Matek |
|---|---|---|---|
| 1 | 120 → 80 | Pajzs nyitva, 3 sávon Swarmlet-eső | 1 sáv-feladat, helyes → 40 sebzés |
| 2 | 80 → 40 | Pajzs zárva, csak a gyengepont sebezhető | 1 sáv-feladat, helyes → 40 sebzés; hibás → a pajzs 12 s-ig zárva marad |
| 3 | 40 → 0 | „Düh": 1.5× sebesség, rövidebb döntési ablak (−25%) | 1 sáv-feladat, helyes → 40 sebzés |

Hibás gyengepont-válasz esetén a feladat **újragenerálódik** (más számokkal, azonos készségre),
tehát a gyerek nem reked meg — csak időt és lőszert veszít. Ez a „fokozatos segítség" pedagógiai
elv gyakorlati alkalmazása (spec 9.1).

---

## 6. Erőforrások

### 6.1. MVP erőforrások (Fázis 1)

| Erőforrás | Kezdő | Max | Fogyás | Forrás | Kifogyás következménye |
|---|---|---|---|---|---|
| `Ammo` (lőszer) | 60 | 200 soft cap | 1 / lövés, 4 lövés/s tüzelés közben | matek-jutalom (~90%), kill-refund +1/kill (~10%) | a karakter nem lő → ellenségek átjutnak |
| `Health` (életerő) | 100 | 100 | átjutott ellenségenként 3–15 | pálya végén teljes feltöltés, boss-fázisok közt +15 | pályavesztés |
| `Shield` (pajzs) | 0 | 50 | a HP előtt fogy, 1:1 | `MoreShield` kapuk, boss legyőzése | – (csak puffer) |

A **soft cap** azt jelenti, hogy a 200 feletti lőszer nem vész el, hanem a pálya végén 1 lőszer =
2 pont árfolyamon pontszámmá válik. Ezzel a jól számoló gyereknek a felesleg is ér valamit,
ugyanakkor a cap megakadályozza, hogy az 1. perc túlteljesítése matekmentessé tegye a 4. percet.
A cap soha nem vághatja le egy kapu jutalmát a döntés pillanatában (3.5 / 3. invariáns).

### 6.2. Javasolt további erőforrások (a spec kéri a bővítést)

| Javaslat | Mit csinál | Miért jó pedagógiailag | Fázis |
|---|---|---|---|
| `FocusCharge` (Fókusz-töltet) | 3 egymást követő **helyes** válasz tölti fel; aktiválva 5 s-ig korlátlan lőszer + lassított ellenségek | A *pontossági sorozatot* jutalmazza, nem a gyorsaságot (spec 12: „a gyorsaság csak a pontosság mellett kapjon jelentőséget"). Erős pozitív megerősítés, ami nem szerencsén múlik. | 2 |
| `RepairKit` (Javítócsomag) | Legfeljebb 2 db hordozható, +30 HP, a gyerek dönti el, mikor használja | Bevezeti az **erőforrás-tervezést** a számolás mellé; a mobilos horde-műfaj szokásos „mikor költsem el" döntése. Nem random drop, hanem `ReachTarget` kapuk pontos eltalálásáért jár — tehát matematikai pontosság jutalma. | 2 |
| `StarShards` (Csillagszilánk) | Meta-valuta pályateljesítésért és új készségek első elsajátításáért; fegyverekre és kinézetekre költhető | Teljesen determinisztikus bolt, nincs véletlen. A *rendszeres gyakorlást* jutalmazza, nem a hibátlanságot (spec 10.2). | 1 (minimálisan), 2 (bolt) |

Kifejezetten **elvetett** ötletek: energiarendszer / élet-limit (játékidő-korlátozás csak a
szülői felületről jöhet — spec 11.), véletlenszerű loot, időzített napi láda (spec 10.2 tiltja).

---

## 7. Jutalmazás és progresszió

### 7.1. Csillagok (pályánként 0–3)

| Csillag | Feltétel | Indoklás |
|---|---|---|
| ★1 | A pálya teljesítése (HP > 0 a boss legyőzésekor) | Minden gyerek elérje, aki végigcsinálta — ez a „rendszeres sikerélmény" (spec 23/7.) |
| ★2 | A matek-döntések **≥ 70%-a** helyes | A célsáv (75–85%) alsó pereme alatt van, hogy a célsávban játszó gyerek biztosan megkapja |
| ★3 | A matek-döntések **≥ 90%-a** helyes **és** 0 timeout | Mesterfokú teljesítmény; nem követel hibátlanságot (10%-ig hibázhat) |

A csillagok **sebességtől függetlenek** — nincs „gyorsasági csillag", mert az a tippelést
jutalmazná (spec 12.).

### 7.2. Progressziós sávok

| Sáv | Mivel nyílik | Tartalom |
|---|---|---|
| Világok | az előző világ pályáinak ≥ 60%-a ★1-gyel teljesítve | lásd `docs/balance/worlds.md` |
| Fegyverek | `StarShards`-ért vásárolva (fix ár, nincs véletlen) | eltérő tüzelési minta (szórás, átütő, gyorstüzelő) — **a DPS-ük azonos**, csak a stílus más, hogy ne legyen „pay-to-win"-jellegű hatalmi lépcső |
| Karakterkinézet | csillag-mérföldkövek (10/25/50/100 ★) | tisztán kozmetikai |
| Képességek | új készség első elsajátítása (lásd `SkillProfile`) | pl. „Kivonás mestere" → +1 másodperc gondolkodási idő minden kivonásos feladatnál |
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
| A rossz válasz ne legyen megalázó | Nincs piros X, nincs „HIBÁS!" felirat, nincs csalódott hangeffekt. A helyes érték zölden felvillan, a jutalom egyszerűen nem száll be. Szövegezés: *„56 volt. Jön a következő!"* |
| Értelmes visszajelzés | Minden döntés után 1,5 s-os, a harcot nem megállító overlay: mindkét kapueredmény / a helyes érték + a jutalom. Részletes magyarázat a pálya végi eredményképernyőn, kérésre (spec 6.5). |
| Rendszeres sikerélmény | DDA célja ~75–85% pontosság mellett a pályateljesítés; lásd `docs/balance/difficulty-model.md` |
| Ne lehessen tippeléssel nyerni | 4 sáv (25%) + a tippelő profil szimulált pályateljesítési aránya cél szerint < 15% (lásd `SIM-plan.md`) |
| A válaszok ne árulják el magukat | A helyes válasz sávpozíciója egyenletes eloszlású; a distraktorok a 6.4. hibamodellekből jönnek, így nagyságrendben nem lógnak ki |
| Ne legyen rendszeresen túl bonyolult | Készség-előfeltétel-kapu: a generátor csak olyan készséget használ, amely a `SkillProfile`-ban `Introduced` vagy magasabb (spec 7.2 utolsó bekezdése) |
| Lehetőség a javításra | A rontott feladattípus a következő 2 pályán emelt (de nem kizárólagos) gyakorisággal tér vissza, egy szinttel könnyebb számokkal |
| A gyorsaság csak a pontosság mellett számít | Nincs idő-alapú pontszorzó; a gyorsaság csak a `FocusCharge` feltöltődésén keresztül hat, ami helyes válaszokat igényel |
| Szünetjavaslat | 25 perc folyamatos játék után barátságos, elutasítható ajánlat; 45 perc után a szülői limit érvényesül |
| Nem csak szín | Minden HUD-elem: ikon + szám + rövid szöveg. A nehézségi sáv 1–10 számmal, a kapucél ikonnal és felirattal. |

---

## 9. HUD-elemek (design-szintű lista)

| Elem | Pozíció | Tartalom |
|---|---|---|
| Kihívásszint-sáv | felül középen | 10 fokozat, zöld→piros, **számmal** (spec 7.1) |
| Feladatsáv | felül, a szint alatt | a kifejezés nagy betűvel, vagy a kapucél ikon + snapshot |
| Lőszer | bal alul | ikon + szám + vízszintes csík |
| Élet / pajzs | jobb alul | szív-ikon + szám; a pajzs külön kék réteg |
| Hullámjelző | jobb felül | „3/8 hullám" |
| Visszajelzés-buborék | a döntési vonal felett | 1,5 s, nem blokkoló |

---

## 10. Nyitott kérdések a további agenteknek

1. `math-engine-dev`: a 3.5. pont generátor-invariánsait unit tesztként is kérjük (property-based
   teszt: 10 000 generált kapupár, egyik sem sérti az 1–5. szabályt).
2. `gameplay-dev`: a `RewardDelta` számítás és a soft cap kölcsönhatását integrációs teszt fedje
   (spec 21/B).
3. `architect`: a `FocusCharge` és `RepairKit` Fázis 2-es, de a `ResourceType` enumot már most
   érdemes bővíthetőre tervezni.
