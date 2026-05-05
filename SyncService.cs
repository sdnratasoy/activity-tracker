using Microsoft.Extensions.Logging;

public static class SyncService
{
    private static ILogger? _logger;
    private static bool _useAccess;
    private static int _running = 0;

    public static void Configure(ILogger logger, string connectionString)
    {
        _logger    = logger;
        _useAccess = connectionString.Contains(".accdb", StringComparison.OrdinalIgnoreCase);
    }

    public static void TrySync()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;

        try
        {
            bool configured = _useAccess ? AccessSyncHelper.IsConfigured : CentralDbHelper.IsConfigured;
            if (!configured) return;

            bool canConnect = _useAccess ? AccessSyncHelper.CanConnect() : CentralDbHelper.CanConnect();
            if (!canConnect) return;

            if (_useAccess) AccessSyncHelper.EnsureTableExists();
            else            CentralDbHelper.EnsureSchemaExists();

            var pending = DatabaseHelper.GetPendingRecords();
            if (pending.Count == 0) return;

            if (_useAccess) AccessSyncHelper.BulkInsert(pending);
            else            CentralDbHelper.BulkInsert(pending);

            DatabaseHelper.DeleteSyncedRecords(pending.Select(r => r.Id));

            _logger?.LogInformation(
                "Sync tamamlandı: {Count} kayıt {Target}'a gönderildi.",
                pending.Count, _useAccess ? "Access" : "SQL Server");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Sync başarısız, tekrar denenecek: {Msg}", ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}
