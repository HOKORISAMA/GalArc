using GalArc.I18n;
using GalArc.Infrastructure.Settings;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace GalArc.Infrastructure.Logging;

internal static class Logger
{
    public static event EventHandler<LogEventArgs> LogEvent;

    static Logger()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath));
    }

    public static readonly string DefaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Assembly.GetExecutingAssembly().GetName().Name, "log.txt");

    private static readonly ConcurrentQueue<LogEntry> _logQueue = new();

    private static void Log(string message, LogLevel level = LogLevel.Info)
    {
        LogEntry entry = new(message, DateTime.Now, level);
        _logQueue.Enqueue(entry);
        LogEvent?.Invoke(null, new LogEventArgs(entry));
    }

    public static void Debug(string message) => Log(message, LogLevel.Debug);

    public static void Info(string message) => Log(message, LogLevel.Info);

    public static void Error(string message) => Log(message, LogLevel.Error);

    public static void DebugFormat(string format, params object[] args) => Debug(string.Format(format, args));

    public static void InfoFormat(string format, params object[] args) => Info(string.Format(format, args));

    public static void ErrorFormat(string format, params object[] args) => Error(string.Format(format, args));

    public static void Persist()
    {
        try
        {
            using StreamWriter writer = new(SettingsManager.Settings.LogFilePath, true);
            while (_logQueue.TryDequeue(out LogEntry entry))
            {
                writer.WriteLine(entry.ToString());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to persist logs: {ex.Message}");
        }
    }

    public static void ShowVersion(string extension, object version) => InfoFormat(MsgStrings.ValidArchiveDetected, extension, version);
}
