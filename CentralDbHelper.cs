using Microsoft.Data.SqlClient;

/// <summary>
/// Merkezi SQL Server ile iletişimi yönetir.
/// VPN/ağ yoksa hızlıca false döner (Connect Timeout=3).
/// </summary>
public static class CentralDbHelper
{
    private static string? _connectionString;

    public static void Configure(string connectionString) =>
        _connectionString = connectionString;

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_connectionString);

    // ── Bağlantı kontrolü ────────────────────────────────────────────────────

    /// <summary>
    /// SQL Server'a ulaşılabiliyorsa true döner.
    /// VPN yoksa 3 saniye içinde false döner.
    /// </summary>
    public static bool CanConnect()
    {
        if (!IsConfigured) return false;
        try
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Şema oluşturma (ilk çalıştırmada IT yapmak zorunda kalmadan) ─────────

    public static void EnsureSchemaExists()
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            IF OBJECT_ID('dbo.ActivityLog', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ActivityLog (
                    Id           BIGINT        IDENTITY(1,1) PRIMARY KEY,
                    LogTime      DATETIME2     NOT NULL,
                    Username     NVARCHAR(100) NOT NULL,
                    ComputerName NVARCHAR(100) NOT NULL,
                    SessionType  NVARCHAR(10)  NOT NULL DEFAULT 'LOCAL',
                    AppName      NVARCHAR(255) NOT NULL,
                    WindowTitle  NVARCHAR(500) NULL,
                    DurationSec  INT           NOT NULL DEFAULT 0,
                    ActiveSec    INT           NOT NULL DEFAULT 0,
                    IdleSec      INT           NOT NULL DEFAULT 0,
                    KeyCount     INT           NOT NULL DEFAULT 0,
                    MouseCount   INT           NOT NULL DEFAULT 0,
                    SyncedAt     DATETIME2     NOT NULL DEFAULT GETDATE()
                );

                CREATE INDEX IX_AL_LogTime  ON dbo.ActivityLog (LogTime);
                CREATE INDEX IX_AL_Username ON dbo.ActivityLog (Username, LogTime);
                CREATE INDEX IX_AL_AppName  ON dbo.ActivityLog (AppName,  LogTime);

                -- Power BI için günlük özet view
                EXEC('
                    CREATE VIEW dbo.vw_DailyUsage AS
                    SELECT
                        CAST(LogTime AS DATE)   AS LogDate,
                        Username,
                        ComputerName,
                        SessionType,
                        AppName,
                        SUM(DurationSec)        AS TotalDurationSec,
                        SUM(ActiveSec)          AS TotalActiveSec,
                        SUM(IdleSec)            AS TotalIdleSec,
                        SUM(KeyCount)           AS TotalKeys,
                        SUM(MouseCount)         AS TotalMouse
                    FROM dbo.ActivityLog
                    GROUP BY CAST(LogTime AS DATE), Username, ComputerName, SessionType, AppName
                ');

                -- Power BI için haftalık özet view
                EXEC('
                    CREATE VIEW dbo.vw_WeeklyUsage AS
                    SELECT
                        DATEPART(YEAR,  LogTime)                   AS [Year],
                        DATEPART(WEEK,  LogTime)                   AS [Week],
                        DATEADD(DAY, 1-DATEPART(WEEKDAY, CAST(LogTime AS DATE)), CAST(LogTime AS DATE)) AS WeekStart,
                        Username,
                        ComputerName,
                        AppName,
                        SUM(DurationSec)                           AS TotalDurationSec,
                        SUM(KeyCount)                              AS TotalKeys,
                        SUM(MouseCount)                            AS TotalMouse
                    FROM dbo.ActivityLog
                    GROUP BY
                        DATEPART(YEAR,  LogTime),
                        DATEPART(WEEK,  LogTime),
                        DATEADD(DAY, 1-DATEPART(WEEKDAY, CAST(LogTime AS DATE)), CAST(LogTime AS DATE)),
                        Username, ComputerName, AppName
                ');
            END";

        cmd.ExecuteNonQuery();
    }

    // ── Toplu yazma ──────────────────────────────────────────────────────────

    /// <summary>
    /// Kayıtları tek transaction içinde SQL Server'a yazar.
    /// Herhangi bir hata olursa exception fırlatır; çağıran rollback kararı verir.
    /// </summary>
    public static void BulkInsert(List<ActivityRecord> records)
    {
        if (records.Count == 0) return;

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        using var tx  = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = @"
            INSERT INTO dbo.ActivityLog
                (LogTime, Username, ComputerName, SessionType, AppName, WindowTitle,
                 DurationSec, ActiveSec, IdleSec, KeyCount, MouseCount)
            VALUES
                (@logTime, @user, @computer, @session, @app, @title,
                 @dur, @active, @idle, @keys, @mouse)";

        var pLogTime  = cmd.Parameters.Add("@logTime",  System.Data.SqlDbType.DateTime2);
        var pUser     = cmd.Parameters.Add("@user",     System.Data.SqlDbType.NVarChar, 100);
        var pComputer = cmd.Parameters.Add("@computer", System.Data.SqlDbType.NVarChar, 100);
        var pSession  = cmd.Parameters.Add("@session",  System.Data.SqlDbType.NVarChar, 10);
        var pApp      = cmd.Parameters.Add("@app",      System.Data.SqlDbType.NVarChar, 255);
        var pTitle    = cmd.Parameters.Add("@title",    System.Data.SqlDbType.NVarChar, 500);
        var pDur      = cmd.Parameters.Add("@dur",      System.Data.SqlDbType.Int);
        var pActive   = cmd.Parameters.Add("@active",   System.Data.SqlDbType.Int);
        var pIdle     = cmd.Parameters.Add("@idle",     System.Data.SqlDbType.Int);
        var pKeys     = cmd.Parameters.Add("@keys",     System.Data.SqlDbType.Int);
        var pMouse    = cmd.Parameters.Add("@mouse",    System.Data.SqlDbType.Int);

        cmd.Prepare();

        foreach (var r in records)
        {
            pLogTime.Value  = DateTime.Parse(r.LogTime);
            pUser.Value     = r.Username;
            pComputer.Value = r.ComputerName;
            pSession.Value  = r.SessionType;
            pApp.Value      = r.AppName;
            pTitle.Value    = (object?)r.WindowTitle ?? DBNull.Value;
            pDur.Value      = r.DurationSec;
            pActive.Value   = r.ActiveSec;
            pIdle.Value     = r.IdleSec;
            pKeys.Value     = r.KeyCount;
            pMouse.Value    = r.MouseCount;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
