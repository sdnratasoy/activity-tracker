using Microsoft.Extensions.Logging;

/// <summary>
/// Local SQLite → SQL Server senkronizasyonunu yönetir.
/// VPN/ağ yoksa sessizce atlar, bir sonraki çağrıda tekrar dener.
/// </summary>
public static class SyncService
{
    private static ILogger? _logger;

    public static void Configure(ILogger logger) => _logger = logger;

    /// <summary>
    /// Worker tarafından her 5 dakikada bir arka planda çağrılır.
    /// Thread-safe: tek seferde sadece bir sync çalışır.
    /// </summary>
    private static int _running = 0;

    public static void TrySync()
    {
        // Eş zamanlı ikinci sync başlamasını önle
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;

        try
        {
            if (!CentralDbHelper.IsConfigured)
            {
                _logger?.LogWarning("Sync atlandı: SQL Server bağlantı dizesi yapılandırılmamış.");
                return;
            }

            // VPN / ağ kontrolü (Connect Timeout=3 ile kısa tutuluyor)
            if (!CentralDbHelper.CanConnect())
            {
                _logger?.LogDebug("Sync atlandı: SQL Server'a ulaşılamıyor (VPN kapalı olabilir).");
                return;
            }

            // İlk çalıştırmada şemayı oluştur
            CentralDbHelper.EnsureSchemaExists();

            // Bekleyen kayıtları al (max 500 / döngü)
            var pending = DatabaseHelper.GetPendingRecords();
            if (pending.Count == 0) return;

            // Toplu gönder
            CentralDbHelper.BulkInsert(pending);

            // Başarılıysa local'den sil
            DatabaseHelper.DeleteSyncedRecords(pending.Select(r => r.Id));

            _logger?.LogInformation(
                "Sync tamamlandı: {Count} kayıt SQL Server'a gönderildi.", pending.Count);
        }
        catch (Exception ex)
        {
            // Hata olursa kayıtlar silinmez, bir sonraki sync'te tekrar denenir
            _logger?.LogWarning("Sync başarısız, bir sonraki denemede tekrar denenecek: {Msg}", ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}
