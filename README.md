# Számharcosok (Math Horde)

3D-s, matematikai túlélő-lövöldözős mobiljáték 7–12 éves gyermekeknek.
A játékos karaktere automatikusan tüzel a felülről érkező ellenségekre,
miközben matematikai kapukon és eredményválasztó sávokon keresztül szerez
életerőt, pajzsot és egyéb nem-ammo erőforrást — **a harc a matematikai feladatok alatt sem
áll le**. A nehézség 1–10 skálán, automatikusan alkalmazkodik a gyermek
teljesítményéhez.

> **Állapot: fejlesztés alatt (ARCH fázis kész).**
> F3 fejlesztés folyamatban. Az ADR-017 végtelen tüzelését és valódi fegyverváltását
> az új F1-20/F1-21 jegyek valósítják meg; a dokumentáció nem kész funkció.
> A pontos, naprakész állapot: [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md).

## Technológia
- Unity 6.3 LTS — rögzített patch: **6000.3.28f1**, C#, URP (mobilra optimalizálva)
- Android (ARM64, IL2CPP) elsődlegesen, iOS későbbi támogatással
- Unity Input System, uGUI + TextMeshPro, portrait orientáció
- **Teljesen offline**: nincs backend, nincs bejelentkezés, nincs hirdetés,
  nincs mikrotranzakció, és a gyermek adatai nem hagyják el az eszközt
- Tesztek: Unity Test Framework (EditMode + PlayMode) és egy Unity-független
  tükör .NET solution (`dotnet test`)

## Dokumentáció
| Dokumentum | Tartalom |
|---|---|
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | rétegek, modulhatárok, állapotgépek, a harc↔matek szinkronizáció |
| [`docs/GAME_DESIGN.md`](docs/GAME_DESIGN.md) | játékmenet, a két matek-mechanika, ellenségek, progresszió |
| [`docs/TESTING.md`](docs/TESTING.md) | teszt-stratégia és teljes teszteset-katalógus |
| [`docs/backlog.md`](docs/backlog.md) | a 7 implementációs fázis feladatokra bontva |
| [`docs/contracts/`](docs/contracts/) | réteg-interfész kontraktok |
| [`docs/decisions/`](docs/decisions/) | ADR-ek — architekturális döntések és indoklásuk |
| [`docs/balance/`](docs/balance/) | nehézségi modell, világok, balancing-szimulációs terv |
| [`docs/IMPLEMENTATION_STATUS.md`](docs/IMPLEMENTATION_STATUS.md) | mi készült el, mi hiányzik, ismert problémák |

## Fejlesztés

A projekt megnyitása Unity Hubból: a repó gyökere a Unity projekt.

```bash
# Gyors, Unity-független tesztek (a motor- és nehézségi logika)
dotnet test tests/MathHorde.Tests.sln

# Teljes tesztkapu (Unity batch-mode)
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -quit
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -quit
```

A parancsok akkor futtathatók, ha a `tests/` solution és a Unity projekt
már létezik — a jelenlegi állapotot lásd fentebb.

## Licenc és adatvédelem
A játék nem gyűjt és nem küld adatot külső szerverre, nem tartalmaz
analitikai követőkódot, és nem igényel internetkapcsolatot.
