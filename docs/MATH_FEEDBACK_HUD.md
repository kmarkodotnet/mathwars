# Döntés utáni matematikai visszajelzés (F3-11)

A `Presentation/Hud/MathFeedbackPresenter` és `MathFeedbackView` a már
kiértékelt döntés olvasott adatait jeleníti meg. A verziózott, önálló prefab:
`Assets/_Project/Prefabs/UI/MathFeedbackHUD.prefab`. Újragenerálás: **Math Horde /
UI / Build Math Feedback HUD**, illetve Unity 6000.3.28f1 batch módban
`-executeMethod MathFeedbackHudBuilder.Build`.

## Bekötés

Az F3-12 kompozíciós gyökér a játék első Tickje előtt hívja:

```csharp
feedbackPresenter.BindLayout(level.LaneCount, level.MathEvents.Count);
feedbackPresenter.Initialize(mathDirector, events);
// Minden session.Tick visszatérése után, a többi presenter drainjével együtt:
feedbackPresenter.RenderPending();
```

A teljes pályaütemezés elemszáma a szükséges előfoglalás, nem csak a legfeljebb
három nem terminális esemény párhuzamossága. A startup előre létrehozza a
visszajelző nézeteket és a struct rekordokat. Játék közben nincs példányosítás,
Destroy, poolbővítés, saját Update vagy coroutine. Túl kicsi foglalás a külső
drainben látható konfigurációs hibát ad; nem vész el csendben esemény. Új
sessionhöz új prefabpéldány szükséges. Dispose/OnDestroy leiratkozik és ürít.

A `MathEventResolved` a választott sávot és a GameLogic helyességi döntését,
a `MathEventCompleted` a tényleges alkalmazott deltát és erőforrást adja. A
szinkron handlerek csak az előfoglalt rekordokat írják. Az első payloadot
őrzik, az ismételt esemény nem változtatja meg a kijelzett döntést. A szövegek
formázása kizárólag a Tick utáni drainben történik. A megjelenítés mindkét
payloadot és a director `Completed` állapotát igényli: így a még feldolgozatlan
jutalom következményét nem találgatja.

## Látható tartalom és időtartam

- Resource kapunál az összes felkínált sávon a rögzített binding műveleti
  címkéje és **nyers**, kapacitással nem vágott eredménye látszik. A jobb
  választást és a választott sávot magyar szöveg is jelöli, nem csak szín.
- Eredményválasztó feladatnál minden felkínált érték, a helyes válasz és a
  választott sáv látszik. A helyességet a már lezárt GameLogic hordozóadata
  határozza meg; a presenter nem értékel kifejezést és nem hasonlít eredményt.
- A helyes döntés összegzése a `MathEventCompleted.AppliedDelta` értéket
  mutatja előjellel. Ez lehet negatív páncélváltozás vagy Max miatt nulla is.
  Hibás döntésnél támogató szöveg és „Nincs jutalom.” jelenik meg.
- Preview, Approaching, AwaitingDecision és a még jutalomra váró Resolving
  nem fed fel visszajelzést. Lejárat és előkészítési hiba nem kap hamis gyermek
  helyes/hibás minősítést. A sessionvég a következő drainben mindent elrejt.
- A rövid megjelenítési időt a director meglévő `FeedbackDisplaySeconds`
  konfigurációja és az ActiveEvents terminális megtartása szabályozza. Az
  eltávolított esemény a következő drainben teljesen ürül. Nincs második óra,
  időzítő vagy szüneteltetés.

A presenter nem generál feladatot, nem bindol újra, nem olvas élő resource
service-t, nem könyvel jutalmat és nem küld inputot. A harc és a meglévő
InputAdapter → PlayerController → CombatService út tovább működik.

## Portrait elrendezés és igazolás

A Canvas Scale With Screen Size módú, 1080×1920 referenciával és
SafeAreaFitterrel. A visszajelző sáv a Safe Area alsó 1–23%-ában található;
a középső harci kép és a felső kérdés-/statisztika-HUD szabad. A sávok index
szerint balról jobbra 2–4 oszlopra oszlanak, a párhuzamos visszajelzések
külön sorokat kapnak. A szöveg magyar TMP alapfonttal, minimum 24-es
mérettel jelenik meg. Nincs gomb, Selectable vagy GraphicRaycaster, minden
grafika átengedi az inputot. A teljes Gameplay scene és a kamera/sávokkal
együtt véglegesített elrendezés bekötése F3-12.

A `Tests/PlayMode/Hud/MathFeedbackPresenterTests.cs` a verziózott prefabbal és
valódi director/generátor/feloldó/jutalomszolgáltatással ellenőrzi a helyes és
hibás döntést, mindkét műveleti eredményt, a fagyasztott snapshotot, negatív és
Max-vágott deltát, 2–4 eredménysávot, párhuzamos eseményeket, rövid törlést,
hibaágakat, leiratkozást és a handler/csendes drain nulla allokációját.
A korábbi tesztfutás Unity-frame-jein a visszajelzés közben még lőszer fogyott, az ellenség sérült és
közeledik, a PlayerController fogadja a sávinputot. A 9:16 és notch-os
9:19.5 elrendezést három párhuzamos visszajelzéssel geometriai és TMP
túlcsordulás-ellenőrzés igazolja. Nagyobb terminális átfedés adatvesztés nélkül
foglalható, végső olvashatóságát a konkrét pályaütemezéssel együtt kell
ellenőrizni az F3-12 kompozícióban.

A helyi batch-render korlát miatt ez nem manuális szín-, eszközös olvashatóság-
vagy teljes K kapujóváhagyás, és nem Android build-igazolás.

**ADR-017:** a fenti futási bizonyíték történeti. F1-20 kivezette az ammo-adatokat
és az ammo feladatmegjelenítést; a folyamatos tüzelés/sebzés, nyers eredmény/live cap,
G4 és negatív Ammo regresszió a friss teljes kapuban zöld.
[F1-20 bizonyíték](TESTING.md#f1-20-friss-migrációs-bizonyíték--2026-11-21).
A teljes math scene-integráció továbbra is F3-12.
