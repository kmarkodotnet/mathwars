# Kontrakt — Matematikai Presentation és külső frame-host (ADR-015)

Namespace/assembly: `MathHorde.Presentation`. A matekszámítás, binding és
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
immutable sablon/kifejezés korábban is cache-elhető.

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
A Prepare nem hívhat Pause-t, nem módosíthat erőforrást, és nem kérhet jövőbeli
snapshotot. Szünetben a játékidő/határidő fagy; nem fut szimulációs frame.
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
| Resource Preview | kizárólag Resource/Goal/TargetValue; nincs snapshot, művelet vagy eredmény |
| Más típus Preview | a teljes kifejezés korán látszik; az opciók a spawnkor jönnek |
| Resource Approaching/AwaitingDecision | a rögzített snapshot, cél és műveleti címkék; az eredmény rejtett |
| Resolving/Completed | a műveleti eredmények, optimális választás és alkalmazott delta visszajelezhető |
| PREPARATION_FAILED | cél-előjelzés eltűnik, tartalomdiagnosztika; nincs gyermek hibás/helyes visszajelzés |

A null Problem resource eseménynél szándékos, nem hiányzó adat: a presenter
Type/ResourceGate alapján választ útvonalat. `IsCorrect`, GetValue,
CorrectOptionIndex vagy kapacitással vágott érték nem szivároghat ki döntés előtt.
A lokalizált cél és eredmény formázása RenderPending alatt történik; a resource
gate Preview üres DisplayExpression-je nem jelenít meg üres feladványablakot.
Reward/Max/snapshot nem Presentationből változik. A sávinput a már szerződött
OnPlayerLaneSelected bemenetre jut; a host nem hív Resolve/ApplyReward-ot UI-ból.

## 3. Végtelen tüzelés és fegyverválasztó — ADR-017

F1-20 eltávolítja a GameplayHudModel/GameplayHUD lőszer Current/Max számát,
csíkját, lekérdezését és ammo ResourceChanged ágát. Nincs készletből képzett ∞
vagy véges mód. A normál HUD Health/Shield/hullám/szünet funkciója megmarad.
A MathQuestion/Gate/Feedback presenter nem fogad Ammo resource-t; Initialize/
RenderPending tartalomhibát jelez, nem lokalizálja vagy rajzolja ki ammo feladatként.
A lokalizációs táblából az aktív ammo-matek cél/jutalom kulcsok és opciók kikerülnek.

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
