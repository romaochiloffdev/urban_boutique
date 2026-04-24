// Author: Ochilov Ilyosjon (ID: B2300540)
// A tiny file-based logger for the WPF applications. Writes to a rolling daily
// log so that production issues can be diagnosed without a debugger attached.

using System;
using System.IO;

namespace UrbanBoutiqueAdmin.Data
{
    /// <summary>
    /// Lightweight append-only logger. Each line is prefixed with a timestamp
    /// and a severity level. Log files roll daily and live in
    /// <c>%LOCALAPPDATA%\UrbanBoutique\logs</c>.
    /// </summary>
    /// <remarks>
    /// Using a file-based logger rather than <c>Debug.WriteLine</c> means
    /// end-users can send us the log file when something goes wrong in
    /// production — useful for a POS system that will run unattended.
    /// </remarks>
    public static class FileLogger
    {
        private static readonly object _lock = new();
        private static readonly string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UrbanBoutique", "logs");

        /// <summary>Records an informational message.</summary>
        public static void Info(string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
            => Write("INFO", message, caller, null);

        /// <summary>Records a non-fatal warning.</summary>
        public static void Warn(string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
            => Write("WARN", message, caller, null);

        /// <summary>Records an error — optionally captures the exception stack.</summary>
        public static void Error(string message, Exception? ex = null,
                                  [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
            => Write("ERROR", message, caller, ex);

        private static void Write(string level, string message, string caller, Exception? ex)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                var path = Path.Combine(LogFolder, $"urban-boutique-{DateTime.UtcNow:yyyy-MM-dd}.log");
                var line = $"[{DateTime.UtcNow:HH:mm:ss}] [{level,-5}] [{caller}] {message}";
                if (ex != null) line += $"{Environment.NewLine}    → {ex.GetType().Name}: {ex.Message}";

                lock (_lock)
                {
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never let logging itself crash the app.
            }
        }
    }
}
