using System;
using System.IO;
using System.Text;

// local-persistence skill: "Atomi írás: ideiglenes fájlba írás, majd
// File.Replace/átnevezés — soha nem írsz közvetlenül a végleges fájlba,
// hogy egy váratlan bezárás ne hagyjon sérült mentést." (ADR-011 4.)
namespace MathHorde.Infrastructure.Persistence
{
    /// <summary>
    /// Low-level atomic text-file IO shared by every repository in this
    /// namespace. Every write goes to a sibling temp file first; only a
    /// single File.Replace/Move call swaps it into the real path, so an
    /// unexpected process kill mid-write can never leave a half-written
    /// save file on disk — the target path is always either the OLD
    /// complete content or the NEW complete content, never a partial one.
    ///
    /// Reads never throw: a missing or corrupt file reports failure through
    /// the out parameters instead of an exception, so every caller can
    /// implement the contract's "no unhandled exception on a corrupt save —
    /// log and start fresh" rule without its own try/catch.
    /// </summary>
    public static class AtomicFileStore
    {
        public static void WriteAllText(string path, string contents)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path kötelező.", nameof(path));
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(tempPath, contents ?? string.Empty, Encoding.UTF8);

            try
            {
                if (File.Exists(path))
                {
                    // Atomic swap on the same volume — no window where the
                    // target is partially written or missing.
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        /// <summary>
        /// Reads the file's full text. Returns false (never throws) if the
        /// file is missing, locked, or otherwise unreadable; errorMessage
        /// then carries a human-readable reason for the caller to log.
        /// </summary>
        public static bool TryReadAllText(string path, out string contents, out string errorMessage)
        {
            contents = null;
            errorMessage = null;

            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    errorMessage = "A fájl nem létezik.";
                    return false;
                }

                contents = File.ReadAllText(path, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>Best-effort delete — missing file or IO error is silently ignored (used for cleanup and profile erasure).</summary>
        public static void DeleteIfExists(string path)
        {
            TryDelete(path);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort only — a cleanup failure must never mask the
                // original error or crash the caller.
            }
        }
    }
}
