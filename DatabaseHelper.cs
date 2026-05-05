using Microsoft.Data.Sqlite;

public static class DatabaseHelper
{
    private static readonly string DbPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "Activity_Tracker", "ActivityLog.db");

    public static string ConnectionString => $"Data Source={DbPath}";

    public static void Initialize()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        Execute(conn, "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;");

        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS ActivityLog (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                LogTime      TEXT    NOT NULL,
                Username     TEXT    NOT NULL,
                ComputerName TEXT    NOT NULL DEFAULT '',
                SessionType  TEXT    NOT NULL DEFAULT 'LOCAL',
                AppName      TEXT    NOT NULL,
                WindowTitle  TEXT,
                DurationSec  INTEGER DEFAULT 0,
                ActiveSec    INTEGER DEFAULT 0,
                IdleSec      INTEGER DEFAULT 0,
                KeyCount     INTEGER DEFAULT 0,
                MouseCount   INTEGER DEFAULT 0,
                IsSynced     INTEGER DEFAULT 0
            )");

        MigrateColumns(conn);

        Execute(conn, @"
            CREATE INDEX IF NOT EXISTS IX_ActivityLog_IsSynced
                ON ActivityLog (IsSynced)");
    }

    private static void MigrateColumns(SqliteConnection conn)
    {
        TryAddColumn(conn, "ComputerName", "TEXT NOT NULL DEFAULT ''");
        TryAddColumn(conn, "SessionType",  "TEXT NOT NULL DEFAULT 'LOCAL'");
        TryAddColumn(conn, "IsSynced",     "INTEGER DEFAULT 0");
    }

    private static void TryAddColumn(SqliteConnection conn, string column, string definition)
    {
        try { Execute(conn, $"ALTER TABLE ActivityLog ADD COLUMN {column} {definition}"); }
        catch { }
    }

    public static void Insert(
        string username, string computerName, string sessionType,
        string appName,  string windowTitle,
        int durationSec, int activeSec, int idleSec, int keyCount, int mouseCount)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ActivityLog
                (LogTime, Username, ComputerName, SessionType, AppName, WindowTitle,
                 DurationSec, ActiveSec, IdleSec, KeyCount, MouseCount)
            VALUES
                ($logTime, $user, $computer, $session, $app, $title,
                 $dur, $active, $idle, $keys, $mouse)";

        cmd.Parameters.AddWithValue("$logTime",  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("$user",     username);
        cmd.Parameters.AddWithValue("$computer", computerName);
        cmd.Parameters.AddWithValue("$session",  sessionType);
        cmd.Parameters.AddWithValue("$app",      appName);
        cmd.Parameters.AddWithValue("$title",    windowTitle);
        cmd.Parameters.AddWithValue("$dur",      durationSec);
        cmd.Parameters.AddWithValue("$active",   activeSec);
        cmd.Parameters.AddWithValue("$idle",     idleSec);
        cmd.Parameters.AddWithValue("$keys",     keyCount);
        cmd.Parameters.AddWithValue("$mouse",    mouseCount);

        cmd.ExecuteNonQuery();
    }

    public static List<ActivityRecord> GetPendingRecords()
    {
        var list = new List<ActivityRecord>();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, LogTime, Username, ComputerName, SessionType,
                   AppName, WindowTitle,
                   DurationSec, ActiveSec, IdleSec, KeyCount, MouseCount
            FROM   ActivityLog
            WHERE  IsSynced = 0
            ORDER  BY Id
            LIMIT  500";

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ActivityRecord(
                r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.GetString(3), r.GetString(4), r.GetString(5),
                r.IsDBNull(6) ? "" : r.GetString(6),
                r.GetInt32(7), r.GetInt32(8), r.GetInt32(9),
                r.GetInt32(10), r.GetInt32(11)));
        }

        return list;
    }

    public static void DeleteSyncedRecords(IEnumerable<long> ids)
    {
        var idList = string.Join(",", ids);
        if (string.IsNullOrEmpty(idList)) return;

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        Execute(conn, $"DELETE FROM ActivityLog WHERE Id IN ({idList})");
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
