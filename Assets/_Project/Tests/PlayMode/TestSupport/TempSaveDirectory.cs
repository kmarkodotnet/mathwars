using System;
using System.IO;

// Shared helper for every Persistence test: an isolated, uniquely-named temp
// directory so repeated/parallel test runs never read another run's
// leftover save file and never touch the real device save
// (Application.persistentDataPath).
namespace MathHorde.Tests.PlayMode.TestSupport
{
    public sealed class TempSaveDirectory : IDisposable
    {
        public string Path { get; }

        public TempSaveDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mathhorde-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup only — never let it fail a test.
            }
        }
    }
}
