// Author: Ochilov Ilyosjon (ID: B2300540)
// File logger for the Cashier terminal — mirrors the Admin version so both
// apps emit diagnostics in a consistent format to the same folder.

using System;
using System.IO;

namespace UrbanBoutiqueCashier
{
    /// <summary>
    /// Lightweight append-only logger. Writes to
    /// <c>%LOCALAPPDATA%\UrbanBoutique\logs\urban-boutique-{date}.log</c>.
    /// </summary>
    public static class FileLogger
    {
        private static readonly object _lock = new();
        private static readonly string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UrbanBoutique", "logs");

        public static void Info(string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
            => Write("INFO", message, caller, null);

        public static void Warn(string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
            => Write("WARN", message, caller, null);

        public static void Error(string message, Exception? ex = null,
                                  [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
            => Write("ERROR", message, caller, ex);

        private static void Write(string level, string message, string caller, Exception? ex)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                var path = Path.Combine(LogFolder, $"urban-boutique-{DateTime.UtcNow:yyyy-MM-dd}.log");
                var line = $"[{DateTime.UtcNow:HH:mm:ss}] [{level,-5}] [Cashier/{caller}] {message}";
                if (ex != null) line += $"{Environment.NewLine}    → {ex.GetType().Name}: {ex.Message}";

                lock (_lock)
                {
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch { /* never crash on log failure */ }
        }
    }
}
