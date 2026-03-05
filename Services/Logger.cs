using System;
using System.IO;

namespace ClashXW.Services
{
    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(ConfigManager.AppDataDir, "clashxw.log");
        private static readonly object Lock = new();
        private const long MaxLogSize = 1024 * 1024; // 1 MB

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex}");

        private static void Write(string level, string message)
        {
            try
            {
                lock (Lock)
                {
                    RotateIfNeeded();
                    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, line);
                }
            }
            catch
            {
                // Logging should never crash the app
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(LogFilePath)) return;
                var info = new FileInfo(LogFilePath);
                if (info.Length > MaxLogSize)
                {
                    var backup = LogFilePath + ".old";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(LogFilePath, backup);
                }
            }
            catch
            {
                // Ignore rotation errors
            }
        }
    }
}
