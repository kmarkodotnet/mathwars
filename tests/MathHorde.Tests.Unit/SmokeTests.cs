using NUnit.Framework;

namespace MathHorde.Tests.EditMode
{
    // F0-03 smoke-teszt — KIZÁRÓLAG azt igazolja, hogy a tükör-solution
    // (ADR-003) fordul és a dotnet test futtató lát legalább egy tesztet,
    // mivel az Assets/_Project/Tests/EditMode alatt F0-03 idején még nincs
    // valódi tesztkód (azt az F1+ fázis írja, lásd TESTING.md). Ha a linkelt
    // Tests/EditMode mappában megjelenik az első igazi teszt, ez a fájl
    // elhagyható / törölhető.
    [TestFixture]
    public class MirrorInfrastructureSmokeTests
    {
        [Test]
        public void MirrorSolution_CompilesAndRuns()
        {
            // A puszta tény, hogy ez a teszt lefut, igazolja: a Core/MathEngine/
            // GameLogic/Analytics tükör-projektek linkelése és a dotnet test
            // futtatólánc (ADR-003) működik, Unity Editor nélkül.
            Assert.Pass("Tükör .NET solution (ADR-003) smoke-teszt zöld.");
        }
    }
}
