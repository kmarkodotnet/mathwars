# Kapu- és válaszhordozó megjelenítés (F3-08)

A `Presentation/Gates/GatePresenter` a `Presentation.md` / ADR-015 szerződés
szerint kapcsolódik a matekdirectorhoz. A két verziózott prefab:
`Assets/_Project/Prefabs/Gates/ResourceGate.prefab` és `AnswerCarrier.prefab`.
Újragenerálás: **Math Horde / UI / Build Math Carrier Prefabs**, batch módban
`-executeMethod GateCarrierBuilder.Build` (Unity 6000.3.28f1).

## Bekötés

A kompozíciós gyökér a presenter szerializált mezőibe adja a két prefab
`GateCarrierView` komponensét, a kamerát és az index szerint sorba rendezett
`LaneAnchor[]` tömböt. A `_poolRoot` opcionális; a címkemagasság és a
kapacitás szerkeszthető. `Initialize(director, events)` a gameplay előtt
előmelegíti mindkét poolt. Az alapkapacitás típusonként 12, a három egyidejű
esemény × négy opció szerződéses maximuma; futás közben nincs poolbővítés.

A host minden `session.Tick` **visszatérése után** hívja a `RenderPending()`
metódust. Az eseményhandlerek kizárólag dirty flaget írnak; nem formáznak és
nem módosítanak Unity-objektumot. Csendes frame-ben a drain továbbra is
frissíti a pozíciókat. A hordozók logikai sávját az arena tényleges
`LaneAnchor.CenterX` koordinátájára képezi, mélységüket a directorból veszi.
A világkoordinátájú TMP Canvasok a kamera síkjával párhuzamosak.

## Látható adatok és életciklus

- Preview alatt nincsenek hordozók. A korai cél/kifejezés és időzítő a
  `MathQuestionPresenter` (F3-09) feladata.
- Approaching/AwaitingDecision alatt a resource prefab a rögzített snapshotot,
  lokalizált erőforrást és célt mutatja, továbbá felfelé/lefelé nyilat vagy
  céljelet. A nagy felirat kizárólag a director `DisplayLabel` művelete.
- A válaszhordozó ugyanilyen nagy TMP-feliratán a válaszopció jelenik meg.
  Egyik útvonal sem olvas `IsCorrect`, `CorrectOptionIndex`, `GetValue` vagy
  élő erőforrásértéket. A helyes opció nem kap előzetes kiemelést.
- Választás, befejezés, lejárat, előkészítési hiba vagy sessionvég után a
  következő drain kiüríti és visszaadja a hordozókat. A döntés utáni eredmény
  visszajelzése F3-11; a Gameplay scene bekötése F3-12.
- `Dispose()`/`OnDestroy` leiratkozik és ürít; a presenter egy sessionre
  inicializálható. Új sessionhez új presenter-példány készül.

## Nem lőhető objektum és ellenőrzési korlát

A hordozó külön `GateCarrierView` típus: nem kerül a CombatService
ellenségnyilvántartásába, nincs `EnemyView`, collider, rigidbody, gomb vagy
raycaster rajta. Minden alobjektuma `Ignore Raycast` rétegen van, a uGUI
grafikák `raycastTarget=false` értékűek. A sávinput útját nem foglalja el.

A prefab címkesávja kontrasztos, az alsó kapunyílás áttetsző; a felirat
48–96 TMP-egység között méreteződik, explicit magyar alapfonttal. Ez nem
teljes képernyős panel: Safe Area a HUD-host felelőssége. A kamera/aréna
kompozícióhoz és a tényleges készülékhez igazított olvashatóság a manuális K
kapu része marad. A batch-render helyi korlátai miatt szín- vagy eszközös
vizuális jóváhagyást ez a jegy nem állít.

A `Tests/PlayMode/Gates/GatePresenterTests.cs` a verziózott prefabokat és
valódi MathEventDirector/MathProblemGenerator szolgáltatásokat használja:
előjelzés, null Problem resource esemény, friss snapshot, mindhárom cél,
2–4 válaszopció, sávgeometria, mozgás, hibaág, eseményhandler-allokáció,
leiratkozás és folyamatos automatikus tüzelés mellett megmaradó kapuk.
