# Sávkiemelés és döntési zóna (F3-10)

A `Presentation/Arena/LanePresenter` a meglévő `LaneAnchor` geometriát, a
`CombatService.PlayerLane` aktuális állapotát és a matekdirector nyilvános
eseményállapotát képezi le. A verziózott prefab:
`Assets/_Project/Prefabs/Arena/LanePresentation.prefab`. Újragenerálása:
**Math Horde / UI / Build Lane Presentation**, batch módban
`-executeMethod LanePresentationBuilder.Build` (Unity 6000.3.28f1).

## Bekötés

A kompozíciós gyökér a változatlan méretarányú, világkoordinátás prefab
példányán a játék indulása előtt hívja:

```csharp
lanePresenter.BindGeometry(orderedLaneAnchors, mathEventConfig.DecisionZoneDepth);
lanePresenter.Initialize(combatService, mathDirector, events);
// Minden session.Tick visszatérése után, a többi presenter drainjével együtt:
lanePresenter.RenderPending();
```

A sávok index szerint rendezett, 2–4 elemű `LaneAnchor` listája ugyanaz az
arénaadat, amelyet a PlayerView és GatePresenter használ. A döntési mélység
ugyanabból a `MathEventConfigSO` konfigurációból érkezik, mint a directorba;
a presenter nem számít érkezési időt vagy határidőt. Hibás, hiányos, rossz
sorrendű geometria és ismételt inicializálás látható hibával leáll, még az
eseményfeliratkozás előtt. Új sessionhöz új presenter példány kell.

A prefab négy előre felépített `LaneHighlightView` komponenst tartalmaz;
a nem használt sávokat a kötés kikapcsolja. Nincs runtime példányosítás,
poolbővítés, materialmásolás vagy saját Update. A szinkron handlerek csak
dirty flaget írnak. A külső drain allokáció nélkül egyezteti az aktuális sávot
és az aktív események állapotát csendes frame-ben is: a már foglalt sáv
választása új PlayerLaneChanged esemény nélkül is lezárhat egy döntési ablakot.

## Látható állapot és input

- Az aktuális játékossávot két hosszanti szegély és egy közeli nyíl jelöli.
- Preview és Approaching alatt nincs aktív döntési keret. Kizárólag a
  director `AwaitingDecision` állapotában jelenik meg a rövid, nyitott
  padlókeret a részt vevő hordozók sávjaiban. Több párhuzamos eseménynél
  ezek uniója látszik; minden válaszopció azonos jelölést kap.
- Resolving, Completed, Expired, előkészítési hiba és eltávolított esemény
  nem tart fenn döntési keretet. SessionEnded után mindkét jelölés eltűnik.
  Dispose/OnDestroy leiratkozik és kiüríti a jelölőket.

A presenter nem olvas `IsCorrect`, `CorrectOptionIndex`, műveleti eredmény,
élő erőforrás vagy jutalom adatot. Nem választ sávot, nem küld választ és nem
állítja meg a harcot. A meglévő InputAdapter → PlayerController →
CombatService út változatlan; a host továbbítja a sávválasztást a directorba.
A saját kiválasztott sáv jelölése a játékos helyzetét mutatja, nem helyességi
visszajelzés. A két állapot alakban is eltér, nem csak színben.

A jelölők vékony, talajközeli URP/Unlit szegélyek; közepük nyitott, nincs
képernyőt fedő panel. Nincs rajtuk collider, rigidbody, EnemyView, gomb vagy
raycaster, minden objektum Ignore Raycast rétegű. Nem tartalmaznak szöveget,
így nem igényelnek új lokalizációs kulcsot vagy fontot. A teljes képernyős
Safe Area továbbra is a HUD-host felelőssége.

## Igazolás és hatókör

A `Tests/PlayMode/Arena/LanePresenterTests.cs` a verziózott prefabbal,
valódi CombatService, MathEventDirector, MathProblemGenerator és
PlayerController mellett igazolja a 2–4 sávot, swipe/tap után a megfelelő
kiemelést, mindkét kaputípust, egyidejű ablakokat, esemény nélküli
állapotegyeztetést, hiba-/lejárati-/sessionvég-ágat, leiratkozást és nulla
handler/drain allokációt. Korábbi jutalomfeloldást nem helyettesít.
Valódi Unity-frame-eken a lőszer fogy és az ellenség sérül, miközben a
döntési keretek láthatók. A 9:16 és 9:19.5 kamera geometriai ellenőrzése
a döntési jelölőket a harci terület talaján tartja.

A teljes Gameplay scene kompozíciója F3-12. A batch-render helyi korlátai
miatt a színek és a végső mobilos olvashatóság manuális Editor Game View /
valós eszköz ellenőrzése a K kapu része marad; ez a jegy nem állít ilyen
jóváhagyást, Android buildet vagy teljes session-integrációt.
