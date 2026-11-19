# Matematikai eseménykonfiguráció — F3-07

Forrás: SO-séma 3., 3.3., 5.1., 6., 6.1.; GAME_DESIGN 2.2., 3.4–3.5.;
spec 5., 7.3., 10.1., 16.; elfogadott ADR-015. Új balance-hangolás nincs:
az alábbi kezdőértékek a hatályos séma értékei.

## Globális asset

`Assets/_Project/Configuration/MathEventConfigDefault.asset` valódi,
Inspectorban szerkeszthető `MathEventConfigSO`. A mezők nem runtime fallbackek.

| Mező | Kezdőérték | Indok / alkalmazás |
|---|---:|---|
| MathEventIntervalSeconds | 20 | Globális gyakorisági alap; explicit pályaschedule-t nem ír át. |
| MathEventIntervalJitter | 0,15 | Gyakorisági tervezési korlát, seedelt schedule-előkészítéshez. |
| MathEventCooldownSecondsFloor | 6 | Hard floor; a CL-szünettel együtt a nagyobbik érvényes. |
| MaxConcurrentMathEvents | 1 | Kezdő játékosnak egy döntés legyen aktív; director settings. |
| GateSpawnDepth | 30 | Globális geometria; director settings. |
| DecisionZoneDepth | 3 | A spawnnál közelebbi döntési pont; director settings. |
| FeedbackDisplaySeconds | 1,5 | Rövid, pozitív visszajelzés; director settings. |
| GoalIconLeadSeconds | 2 | GAME_DESIGN 3.4. szerinti minimum; a schema nyers 1 s-os alsó határánál szigorúbb. |
| MaxConsecutiveSameProblemType | 2 | Mindkét mechanika váltakozása, GAME_DESIGN 2.2. |
| MaxConsecutiveSameGateSide | 2 | Oldalpreferencia elleni generálási korlát. |
| NonObviousGatePairRatioMin | 0,30 | Ne a nagyobb operandus felismerése legyen a megoldás. |
| IncorrectRewardAmount | 0 | MVP: a hibás válaszhoz nincs matematikai jutalom. |
| ResourceGateOperandMin / Max | 2 / 50 | A séma szerinti szerkeszthető generálási korlát. |
| ResourceGateMultiplierMin / Max | 2 / 4 | A séma szerinti szerkeszthető szorzótartomány. |

A `Validate()` a teljes tartományt, a véges számokat, a két min/max párt és
a `GateSpawnDepth > DecisionZoneDepth` összefüggést ellenőrzi. A globális mező
nyers tartománya és az aktuális MVP felhasználhatósága külön ellenőrzés:
például pozitív `IncorrectRewardAmount` az adapterben hibát ad.

## Beágyazott pályaadatok és adapter

A `LevelDefinitionSO.MathEvents` nyolc szerkeszthető mezőt hordoz:
`TriggerGameTime`, `ProblemType`, `PreviewDurationSeconds`,
`ThinkTimeSecondsOverride`, `RewardResource`, `Goal`, `RewardAmountOverride`,
`TargetValueOverride`. A három 0-sentinel jelentése: a gondolkodási idő a
profilból, a fix jutalom a jutalomtáblából, a cél a host generálásából jön.
A generált cél lehet ténylegesen 0; ezt a dictionary-bejegyzés jelenléte
különbözteti meg a hiánytól. `RequiredMathAccuracy` is szerkeszthető, 0–1.

A SO-validáció 10–40 eseményt követel (`Tutorial`: 8–40), ellenőrzi az
enumokat, a tartományokat, a pályavég előtti triggeridőt, a null elemeket,
az azonos triggeridőt, a `ReachTarget → TargetValueSelection` és
`Minimize → EnemyArmor` szabályokat. Időrendben legfeljebb két azonos típus
követheti egymást. A validáció csak egy indexmásolatot rendez.

A repository a hat Core-schedule-mezőt és az accuracyt fordítja; az override-ok
nem kerülnek új, kontrakton kívüli Core-mezőkbe. A
`MathEventConfigurationAdapter.CreateDirectorSettings` még `Initialize` előtt
állítja elő a már létező immutable `MathEventDirectorSettings` példányt:

1. A host a kiválasztott **TimePressureTableSO** sor feloldott
   `GateApproachSpeed` és `MinGapBetweenProblemsSeconds` értékeit adja át.
   A sebesség 0,5–20, a CL-szünet 8–60 s. A kötelező triggerköz a CL-szünet
   és a globális hard floor maximuma. A típusismétlés az effektív
   `min(2, MaxConsecutiveSameProblemType)` korláttal vizsgált.
2. A host a **RewardTableSO** kiválasztott sorából, a típus/erőforrás szerint
   feloldott alapjutalmat az eredeti schedule-indexhez rendeli. Pozitív
   `RewardAmountOverride` ezt felülírja a nem-resource feladatoknál.
   Resource gate mindig `UseSnapshotDelta=true`, `FlatAmount=0`;
   fix override ehhez hibás konfiguráció, nem második jutalom.
3. `TargetValueSelection` esetén pozitív cél-override vagy a host által
   már generált, indexhez rendelt cél kell. Hiány esetén nincs kitalált érték.
4. Az adapter minden bejegyzéshez `RewardSpec`-et készít. A settings másolatokat
   őriz; sem a SO, sem a bemeneti dictionary utólagos módosítása nem hat rá.

Nincs második TimePressure/Reward tábla, új balance-konstans vagy publikus
GameLogic/MathEngine/Core-kontrakt. A két tábla SO-implementációja a meglévő
F4-06 feladat része. A director által még nem fogyasztott globális mezők
(automatikus intervallum/jitter, cél-ikon, oldalkorlát, nem nyilvánvaló párok,
operandus/szorzótartomány) szerkeszthető adatok; e jegy **nem állít** hozzájuk
teljes scene/generátor/presenter bekötést. A host a lefagyasztott API-kat
használja; az adapter nem módosít `DifficultyProfile`-t ezek kedvéért.

## Meglévő W1L01 prototípus és ismert integrációs hiba

Az eddigi `LevelW1L01.asset` 150 másodperces F1-pálya, változatlan erőforrásokkal,
fegyverrel és hullámokkal. Most 10 váltakozó resource/lane esemény van benne:
trigger 4, 18, 32, 46, 60, 74, 88, 102, 116, 130 s; Preview 2 s; valamennyi
override 0; cél `Maximize`, erőforrás `Ammo`, accuracy-követelmény 0.
A 14 s-os triggerköz a CL1 táblabeli szünetét teljesíti. Ez szerializált,
strukturálisan valid **előkészítési adat**, nem már lejátszható F3 referencia:
a teljes lifecycle ideje/concurrency és a 15 s-os matekmentes lezárás pályaszintű
kalibrálása a valódi hosttal az F3-12 integrációban szükséges.

Az alap CL1 profil `MaxResultValue=10`, miközben `MaxAmmo=200`. Ez a teljes
`0..Max` snapshot-tartományra nem adható érvényes resource gate; a tényleges
director `PrepareUpcomingEvents` eredménye **GENERATION_EXHAUSTED**. A shipped
asseteket betöltő teszt ezt a valódi generátorral igazolja. Nem csökkentjük
emiatt a kapacitást, nem tágítjuk a profilt, nem cseréljük a snapshotot és nem
engedjük el a gap/skill/validátor invariánsait. Startup failure után a végleges
host nem indíthat gameplay-frame-et; futó sessionben az ADR-015 diagnosztikus
hibafolyamata érvényes. Ez ismert technikai hiba, amelyet a teljes F3 rollout
előtt rendezni kell. Az F1 bootstrap jelenleg továbbra a korábbi null directorral
fut; a valódi scene/presenter/session összekötése **F3-12**.

Igazolás: `MathEventConfigurationTests` PlayMode tesztek — szerializált asset,
adapter-indexstabilitás és immutabilitás, override/sentinel, határértékek,
CL/global szigorúság, helyi és integrációs hibák. A teljes kapu konkrét
eredménye a [jegy naplójában](../backlog/done/F3-07.md) található.
