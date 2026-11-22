# Kontrakt — Matematikai Presentation és külső frame-host

> **Revízió — ADR-018 + ADR-019 (2026-11-22, architect):** a kapu-feladatnak nincs
> erőforrás-snapshotja és nincs runtime bindingja. A `Preview` a célt és a **fix
> életerő-jutalmat** jelzi előre; az `Approaching` a kapu **generált bázisértékét**
> (`ActiveMathEvent.GateBaseValue`) és a műveleti címkéket fedi fel; az opciók
> **eredménye** csak a feloldás után jeleníthető meg. A `ResourceGateBinding`,
> `GetValue` és „kapacitással vágott érték” fogalmak kivezetve.

Namespace/assembly: `MathHorde.Presentation`. A matekszámítás és az
erőforrásmódosítás továbbra sem Presentation-felelősség. A Core/MathEngine/GameLogic
Unity-mentes marad. Az alábbi szerződés F3-08/F3-09/F3-12 kötelező bemenete.

## 1. A Prepare pontos hívási helye

A meglévő `Presentation/Bootstrap/GameplaySessionBootstrap` a külső host.
Az összetett session felépítésekor a kompozíciós gyökér inicializálja a resource
poolokat, a directort, a generátort és a konfigurációs mapeket. **Az első gameplay
Tick előtt**, a betöltési/előkészítési szakaszban a host meghívja a director
`PrepareUpcomingEvents(clock.GameTime)` metódusát. Siker nélkül gameplay-frame
nem indul; az eredmény konfigurációs hibaként jelenik meg, nem gyermekvereségként.
Nem szükséges pontosan a Preview előtti utolsó frame-ben újragenerálni: a kész
immutable `MathProblem` korábban is cache-elhető — sőt, az ADR-019 óta **minden**
feladattípus teljes feladata itt készül el.

Minden futó, nem szüneteltetett frame hívási rendje:

```csharp
// GameplaySessionBootstrap.Update — a session.Tick híváson KÍVÜL:
// 0. A kompozíciós gyökér ütemezett Max/config parancsai, ha vannak.
var preparation = mathDirector.PrepareUpcomingEvents(clock.GameTime);
session.Tick(deltaTime); // a belső clock→wave→combat→math→resolution→analytics sorrend változatlan
gatePresenter.RenderPending();
mathQuestionPresenter.RenderPending();
// analytics: itt materializálhat MathAnswer/QuestionAttempt/string objektumokat és írhat mentést
// preparation hibát és MathEventPreparationFailed diagnosztikát a host itt jelenít meg
```

Változatlan kontextusra a Prepare cache-találat, nem generál frame-enként.
Futó sessionben az előkészítési hiba miatt a host nem ugorhatja át a harci Tick-et.
A Prepare nem hívhat Pause-t, **nem olvas és nem módosít erőforrást**, és nem kér
semmilyen játékállapot-pillanatképet (ADR-018). Szünetben a játékidő/határidő fagy;
nem fut szimulációs frame. Az erőforrás-`Max` menet közbeni változása **nem**
igényel újraelőkészítést, mert a feladat tartalma nem erőforrásból jön.
Headless driver ugyanezt a Prepare → Tick → külső drain rendet használja.
A F3-02 szolgáltatásteszt maga a host; a scene tényleges bekötése F3-12.

## 2. Presenter-felület és megjelenítési invariáns

```csharp
// GatePresenter és MathQuestionPresenter azonos bekötési/ürítési életciklussal:
void Initialize(IMathEventDirector director, IGameEventSource events);
void RenderPending(); // csak session.Tick VISSZATÉRÉSE UTÁN
```

Az IGameEventSource a már létező `MathHorde.Presentation.Events` interfész:
Subscribe/Unsubscribe marad a megfigyelési út. Nincs új GameLogic-visszahivatkozás.
A szinkron eseményhandler csak előre foglalt struct-sort vagy dirty flaget ír;
nem formáz stringet, nem készít UI-objektumot és nem növel listát Tick közben.
Az előfizetések Initialize alatt, a leiratkozás Dispose/OnDestroy alatt történik.

| Állapot/típus | Látható adat |
|---|---|
| Kapu (`ResourceOperationComparison`) `Preview` | kizárólag `Goal`, `TargetValue` és a fix `RewardAmount` (szív-ikon + szám); **nincs** bázisérték, művelet vagy eredmény |
| Más típus `Preview` | a teljes kifejezés korán látszik; az opciók a spawnkor jönnek |
| Kapu `Approaching`/`AwaitingDecision` | a `GateBaseValue`, a cél, a fix jutalom és a műveleti címkék (`Carriers[].DisplayLabel`); az **eredmény rejtett** |
| `Resolving`/`Completed` | a műveleti eredmények, az optimális választás és a tényleges `AppliedDelta` visszajelezhető |
| `PREPARATION_FAILED` | cél-előjelzés eltűnik, tartalomdiagnosztika; nincs gyermek hibás/helyes visszajelzés |

A null `Problem` kapu-eseménynél (`Preview` alatt) szándékos, nem hiányzó adat: a
presenter `Type` és `State` alapján választ útvonalat. A műveleti címke egyetlen
megengedett forrása a `Carriers[].DisplayLabel`; az `ActiveMathEvent.Problem.Options`
értékei, az `IsCorrect` és a helyes opció indexe **nem szivároghat ki** döntés előtt.
A lokalizált cél és eredmény formázása RenderPending alatt történik; a kapu `Preview`
üres `DisplayExpression`-je nem jelenít meg üres feladványablakot.
Jutalom, `Max` vagy feladattartalom nem Presentationből változik. A sávinput a már
szerződött `OnPlayerLaneSelected` bemenetre jut; a host nem hív `Resolve`/`ApplyReward`-ot UI-ból.

A jutalom-kijelzés a fix életerő-összeg (`+30 ♥`); **jutalom-típus választó ikon
nincs** (ADR-018 6. pont — F5-13 hatóköre). A kapu-visszajelzés tele életerőnél is
`Applied`: a HUD a `ResourceChange.AppliedDelta`-t animálja, nem a kért összeget.

## 3. Végtelen tüzelés és fegyverválasztó — ADR-017

F1-20 eltávolítja a GameplayHudModel/GameplayHUD lőszer Current/Max számát,
csíkját, lekérdezését és ammo ResourceChanged ágát. Nincs készletből képzett ∞
vagy véges mód. A normál HUD Health/Shield/hullám/szünet funkciója megmarad.
A MathQuestion/Gate/Feedback presenter nem fogad Ammo resource-t; Initialize/
RenderPending tartalomhibát jelez, nem lokalizálja vagy rajzolja ki ammo feladatként.
**ADR-018 kiegészítés:** ugyanez érvényes `Shield`, `EnemyArmor` és `Energy`
jutalomra is — ezek matematikai feladatként tartalomhibák
(`REWARD_RESOURCE_NOT_ALLOWED`), nem rajzolhatók ki. A lokalizációs táblából az
aktív ammo-, pajzs- és páncél-matek cél/jutalom kulcsok kikerülnek; a kapucél
kulcsai `ui.gate.goal_max` / `ui.gate.goal_min` / `ui.gate.goal_target`
(GAME_DESIGN 3.4).

F1-21 a meglévő HUD-ra épülő felülete:

```csharp
// A meglévő GameplayHUD.Initialize mellé vagy annak kiegészítéseként, startupkor:
void InitializeWeaponSelection(IGameSessionService session, IGameEventSource events);
// A meglévő host külső renderfázisában, session.Tick után:
void RenderPending();
```

A modell AvailableWeapons/CurrentWeaponId-t olvas; lokalizált típusonkénti gombok
közvetlenül a session.SelectWeapon(Id)-t hívják. A view nem módosít konfigurációt,
Damage-t vagy ResourceService-t. A WeaponChanged handler csak előre foglalt értéket/
dirty flaget ír; stringfeloldás és UI-frissítés a külső RenderPending-ben történik.
A kezdő kijelölés StartSession után szinkronizált. A bootstrap és scene-builder
ugyanazt a két gombot, referenciákat és lifecycle-leiratkozást szállítja.

A két első típus és a váltási eredmény/hibakód az ADR-017 szerint. Legalább 48 dp
érintési felület, magyar név, aktív jelölés, portrait elrendezés kell; a választó nem
takarhat matekfeladatot. 1 elérhető típusnál elrejthető. Pause/sessionvég alatt tiltott.
A UI-n indult érintés nem jut az InputAdapter sávválasztó/swipe csatornájára; ezt
valódi PlayMode pointeres bemenet ellenőrzi. A váltás nem állítja meg a harcot,
nem számít matekválasznak és nem jelent jutalmat vagy feloldást.
