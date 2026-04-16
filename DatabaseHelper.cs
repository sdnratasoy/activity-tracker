using Microsoft.Data.Sqlite;
using System.IO;

public static class DatabaseHelper
{
    private static readonly string DbPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "Activity_Tracker", "ActivityLog.db");

    public static string ConnectionString =>
        $"Data Source={DbPath}";

    public static void Initialize()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ActivityLog (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                LogTime     TEXT    NOT NULL,
                Username    TEXT    NOT NULL,
                AppName     TEXT    NOT NULL,
                WindowTitle TEXT,
                DurationSec INTEGER DEFAULT 0,
                ActiveSec   INTEGER DEFAULT 0,
                IdleSec     INTEGER DEFAULT 0,
                KeyCount    INTEGER DEFAULT 0,
                MouseCount  INTEGER DEFAULT 0
            )";
        cmd.ExecuteNonQuery();
    }

    public static void Insert(
        string username,
        string appName,
        string windowTitle,
        int durationSec,
        int activeSec,
        int idleSec,
        int keyCount,
        int mouseCount)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ActivityLog
                (LogTime, Username, AppName, WindowTitle,
                 DurationSec, ActiveSec, IdleSec, KeyCount, MouseCount)
            VALUES
                ($logTime, $user, $app, $title,
                 $dur, $active, $idle, $keys, $mouse)";

        cmd.Parameters.AddWithValue("$logTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("$user",    username);
        cmd.Parameters.AddWithValue("$app",     appName);
        cmd.Parameters.AddWithValue("$title",   windowTitle);
        cmd.Parameters.AddWithValue("$dur",     durationSec);
        cmd.Parameters.AddWithValue("$active",  activeSec);
        cmd.Parameters.AddWithValue("$idle",    idleSec);
        cmd.Parameters.AddWithValue("$keys",    keyCount);
        cmd.Parameters.AddWithValue("$mouse",   mouseCount);

        cmd.ExecuteNonQuery();
    }
}
