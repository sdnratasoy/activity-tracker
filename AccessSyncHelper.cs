using System.Data.OleDb;

public static class AccessSyncHelper
{
    private static string? _connectionString;

    public static void Configure(string connectionString) =>
        _connectionString = connectionString;

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public static bool CanConnect()
    {
        if (!IsConfigured) return false;
        try
        {
            using var conn = new OleDbConnection(_connectionString);
            conn.Open();
            return true;
        }
        catch { return false; }
    }

    public static void EnsureTableExists()
    {
        using var conn = new OleDbConnection(_connectionString);
        conn.Open();

        var schema = conn.GetSchema("Tables");
        foreach (System.Data.DataRow row in schema.Rows)
        {
            if (row["TABLE_NAME"]?.ToString() == "ActivityLog") return;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE ActivityLog (
                Id           AUTOINCREMENT CONSTRAINT PK_ActivityLog PRIMARY KEY,
                LogTime      DATETIME,
                Username     TEXT(100),
                ComputerName TEXT(100),
                SessionType  TEXT(10),
                AppName      TEXT(255),
                WindowTitle  MEMO,
                DurationSec  LONG,
                ActiveSec    LONG,
                IdleSec      LONG,
                KeyCount     LONG,
                MouseCount   LONG,
                SyncedAt     DATETIME DEFAULT Now()
            )";
        cmd.ExecuteNonQuery();
    }

    public static void BulkInsert(List<ActivityRecord> records)
    {
        if (records.Count == 0) return;

        using var conn = new OleDbConnection(_connectionString);
        conn.Open();

        using var tx  = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = @"
            INSERT INTO ActivityLog
                (LogTime, Username, ComputerName, SessionType, AppName, WindowTitle,
                 DurationSec, ActiveSec, IdleSec, KeyCount, MouseCount)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        var pars = new OleDbParameter[11];
        for (int i = 0; i < pars.Length; i++)
            pars[i] = cmd.Parameters.Add($"p{i}", OleDbType.VarChar);

        pars[0].OleDbType  = OleDbType.Date;
        pars[6].OleDbType  = OleDbType.Integer;
        pars[7].OleDbType  = OleDbType.Integer;
        pars[8].OleDbType  = OleDbType.Integer;
        pars[9].OleDbType  = OleDbType.Integer;
        pars[10].OleDbType = OleDbType.Integer;

        foreach (var r in records)
        {
            pars[0].Value  = DateTime.Parse(r.LogTime);
            pars[1].Value  = r.Username;
            pars[2].Value  = r.ComputerName;
            pars[3].Value  = r.SessionType;
            pars[4].Value  = r.AppName;
            pars[5].Value  = (object?)r.WindowTitle ?? DBNull.Value;
            pars[6].Value  = r.DurationSec;
            pars[7].Value  = r.ActiveSec;
            pars[8].Value  = r.IdleSec;
            pars[9].Value  = r.KeyCount;
            pars[10].Value = r.MouseCount;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
