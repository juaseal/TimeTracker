using System.IO;

namespace TimeTracker;

public static class AppPaths
{
    // Keep the historical storage folder so TaskUp reuses existing user data.
    public const string ProductName = "TimeTracker";
    public static string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName);
    public static string DatabasePath => Path.Combine(DataDirectory, "timetracker.db");
    public static string LogPath => Path.Combine(DataDirectory, "errors.log");
    public static string LegacyPortableDatabasePath => Path.Combine(AppContext.BaseDirectory, "data", "timetracker.db");

    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
}