// ObjectPoolTests.cs
// F1-14 — proves ObjectPool<T> reuse semantics (math-horde.md 18.: nincs
// Instantiate/Destroy a harci ciklusban). Lives under Tests/PlayMode
// (not EditMode) purely because it references the Presentation assembly —
// MathHorde.Tests.EditMode.asmdef and the dotnet mirror solution
// (game/tests/MathHorde.Tests.sln) do not reference Presentation at all, so
// a Presentation-referencing test here would fail to compile under
// `dotnet test` (see MathHorde.Tests.PlayMode.asmdef vs
// MathHorde.Tests.EditMode.asmdef references). The tested type itself has
// zero UnityEngine dependency.
using NUnit.Framework;
using MathHorde.Presentation.Pooling;

namespace MathHorde.Tests.PlayMode.Pooling
{
    [TestFixture]
    public class ObjectPoolTests
    {
        private sealed class Widget
        {
            public int GetCount;
            public int ReleaseCount;
        }

        [Test]
        public void Get_WhenPoolEmpty_CreatesExactlyOneNewInstance()
        {
            int created = 0;
            var pool = new ObjectPool<Widget>(factory: () => { created++; return new Widget(); });

            Widget widget = pool.Get();

            Assert.That(widget, Is.Not.Null);
            Assert.That(created, Is.EqualTo(1));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void GetThenRelease_ThenGetAgain_ReusesTheSameInstanceWithoutAllocating()
        {
            int created = 0;
            var pool = new ObjectPool<Widget>(factory: () => { created++; return new Widget(); });

            Widget first = pool.Get();
            pool.Release(first);
            Widget second = pool.Get();

            Assert.That(second, Is.SameAs(first));
            Assert.That(created, Is.EqualTo(1), "a második Get() nem hozhat létre új példányt, ha van szabad elem.");
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
        }

        [Test]
        public void Prewarm_CreatesInstancesUpFront_SoSubsequentGetsDoNotAllocate()
        {
            int created = 0;
            var pool = new ObjectPool<Widget>(factory: () => { created++; return new Widget(); }, prewarmCount: 3);

            Assert.That(created, Is.EqualTo(3));
            Assert.That(pool.FreeCount, Is.EqualTo(3));

            pool.Get();
            pool.Get();
            pool.Get();

            Assert.That(created, Is.EqualTo(3), "a harci ciklusban (itt: 3 egymást követő Get()) nem szabad új példányt létrehozni, ha a prewarm elég volt.");
        }

        [Test]
        public void Get_WhenDemandExceedsFreeInstances_GrowsOnlyByTheShortfall()
        {
            int created = 0;
            var pool = new ObjectPool<Widget>(factory: () => { created++; return new Widget(); }, prewarmCount: 1);

            pool.Get(); // consumes the single prewarmed instance
            pool.Get(); // must grow by exactly one

            Assert.That(created, Is.EqualTo(2));
        }

        [Test]
        public void Get_InvokesOnGetCallback_AndRelease_InvokesOnReleaseCallback()
        {
            var pool = new ObjectPool<Widget>(
                factory: () => new Widget(),
                onGet: w => w.GetCount++,
                onRelease: w => w.ReleaseCount++);

            Widget widget = pool.Get();
            pool.Release(widget);

            Assert.That(widget.GetCount, Is.EqualTo(1));
            Assert.That(widget.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void ActiveCount_TracksOutstandingInstances()
        {
            var pool = new ObjectPool<Widget>(factory: () => new Widget());

            Widget a = pool.Get();
            Widget b = pool.Get();
            Assert.That(pool.ActiveCount, Is.EqualTo(2));

            pool.Release(a);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));

            pool.Release(b);
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Release_WithNull_Throws()
        {
            var pool = new ObjectPool<Widget>(factory: () => new Widget());

            Assert.That(() => pool.Release(null), Throws.TypeOf<System.ArgumentNullException>());
        }
    }
}
