# Matekfeladat-HUD (F3-09)

A `Presentation/Hud/MathQuestionPresenter` a Presentation / ADR-015 szerinti
külső drainnel jeleníti meg az aktív feladatokat. A verziózott prefab
`Assets/_Project/Prefabs/UI/MathQuestionHUD.prefab`; újragenerálása **Math Horde /
UI / Build Math Question HUD**, illetve batch módban
`-executeMethod MathQuestionHudBuilder.Build` (Unity 6000.3.28f1).

## Bekötés és elrendezés

Az inicializálás előtt `BindClock(sessionClock)` kapja a session tényleges
`IGameClock` példányát, majd `Initialize(director, events)` iratkozik fel.
A host minden `session.Tick` visszatérése után hívja a `RenderPending()`-et;
az eseményhandlerek kizárólag jelzőket írnak. A `BindClock` egy meglévő,
olvasott függőség injektálása; nem helyettesítő óra és nem változtatja a
Presentation kontrakt Initialize/RenderPending felületét. Nincs saját Update,
időléptetés, jutalomkezelés, helyességvizsgálat vagy sávinput a komponensben.

A prefab három előrefoglalt sort tartalmaz a director legfeljebb három
párhuzamos eseményéhez, az ActiveEvents sorrendjében. Nem példányosít futás
közben. A teljes képernyős Canvas Scale With Screen Size módban működik,
1080×1920 referenciafelbontással és SafeAreaFitterrel. A kérdéssáv a Safe Area
y=0.66–0.90 tartományában marad; az alsó 66% harci terület szabad. A kérdések
TMP mérete 32–42, az időjelzésé 26–32, szövegtördeléssel és explicit magyar
alapfonttal. Minden grafika átereszti az inputot; nincs gomb vagy raycaster.

Ez saját, önálló prefab és bekötési felület. A meglévő GameplayHUD
`MathEventArea_ReservedF2` 8%-os helye nem elegendő három teljes kérdéshez.
Az F3-12 scene-kompozícióban a kérdés-HUD és a GameplayHUD statisztika- és
kihívásszint-elemei számára egymást nem fedő helyet kell kialakítani a felső
HUD-sávban; e prefab ráhelyezése a jelenlegi GameplayHUD-ra önmagában nem
végleges scene-integráció. A Gameplay scene ebben a jegyben nem változik.

## Látható tartalom és idő

- AnswerLaneSelection és a többi nem-resource feladat már Preview alatt a
  teljes `Problem.DisplayExpression` kifejezést mutatja. Opció, helyes válasz,
  magyarázat és kiértékelés nem kerül a kérdés-HUD-ba.
- Resource Preview alatt kizárólag a lokalizált erőforrás és cél látszik,
  ReachTarget esetén a célértékkel. A null Problem szándékos. Spawn után a
  director rögzített snapshotja jelenik meg; élő resource service-t nem olvas.
  A műveletek a GatePresenter hordozóin látszanak, az eredmény rejtett marad.
- A Preview és Approaching állapotban a lokalizált „Készülj!” / „Közeledik”
  felirat jelenik meg. A publikus director állapotában a határidő csak a
  döntési ablak nyitásakor áll rendelkezésre; a HUD nem talál ki korábbi időt.
- AwaitingDecision alatt `DeadlineGameTime - sessionClock.GameTime` adja a
  hátralévő időt, felfelé kerekített másodpercekkel és folytonos kitöltéssel.
  Szünetben a session óra fagy, így a kijelzés is. A másodpercfelirat csak
  változásakor formázódik, a Tick utáni drainben.
- Resolving/Completed, Expired, preparation hiba, eltávolított esemény és
  sessionvég után a következő drain törli és elrejti a sort. Nem mutat
  gyermeknek helyes/hibás visszajelzést; ez F3-11. Dispose/OnDestroy minden
  előfizetést megszüntet és ürít. Új sessionhez új példány szükséges.

A magyar TMP alapfont ugyanazzal a GUID-val bővült a Core kifejezéseiben
használt ×, ÷ és − jelekkel. Az idempotens `TmpHungarianFontSetup` a meglévő
assetet egészíti ki, Static atlaszként mentve. A `hu.json` kérdéskulcsai is a
meglévő teljes karakterlefedettség-teszt hatálya alá esnek.

## Igazolás és korlát

A `Tests/PlayMode/Hud/MathQuestionPresenterTests.cs` a verziózott prefab és
valódi director/generátor mellett vizsgálja a korai kifejezést, a három
resource célt, a friss és utólag változatlan snapshotot, visszaszámlálást,
szünetet, három aktív sort, lezárást, hibaágat, leiratkozást és a handler /
csendes drain allokációmentességét. Valódi Unity-frame-eken futó harc mellett
is látható marad a kérdés. A 9:16 és 9:19.5 Safe Area elrendezést és a TMP
túlcsordulását automatikus geometriai ellenőrzés igazolja. A helyi batch-render
korlátja miatt ez nem szín- vagy valós eszközös K jóváhagyás; az a végső
scene-kompozícióval együtt manuálisan ellenőrizendő.
