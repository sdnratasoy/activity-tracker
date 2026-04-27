using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly GlobalInputHook _inputHook = new();

    private readonly string _username = Environment.UserName;

    private string _currentApp   = "";
    private string _currentTitle = "";
    private DateTime _startTime;
    private int _activeSeconds = 0;
    private int _idleSeconds   = 0;

    // Her 60 saniyede bir periyodik kayıt
    private DateTime _lastFlushTime = DateTime.Now;
    private const int FlushIntervalSec = 60;

    // Rapor zamanlaması
    private DateTime _lastReportDate = DateTime.Today;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DatabaseHelper.Initialize();
        _inputHook.Start();
        _startTime = DateTime.Now;

        _logger.LogInformation("ActivityTracker başladı. Kullanıcı: {User}", _username);

        // Başlangıçta bugünün raporunu oluştur
        GenerateScheduledReports(DateTime.Today);

        while (!stoppingToken.IsCancellationRequested)
        {
            var rawProcess = ActiveWindow.GetActiveWindowProcess();
            var rawTitle   = ActiveWindow.GetActiveWindowTitle();

            if (IdleDetector.IsIdle())
                _idleSeconds++;
            else
                _activeSeconds++;

            // Uygulama değiştiyse öncekini kaydet
            if (rawProcess != _currentApp)
            {
                FlushCurrent();
                _currentApp   = rawProcess;
                _currentTitle = rawTitle;
                _startTime    = DateTime.Now;
                _activeSeconds = 0;
                _idleSeconds   = 0;
                _lastFlushTime = DateTime.Now;
            }
            // Aynı uygulama açıkken 60 saniyede bir ara kayıt yap
            else if ((DateTime.Now - _lastFlushTime).TotalSeconds >= FlushIntervalSec)
            {
                FlushCurrent(periodic: true);
                _lastFlushTime = DateTime.Now;
            }

            // Gün dönümünde rapor oluştur
            if (DateTime.Today > _lastReportDate)
            {
                GenerateScheduledReports(_lastReportDate);
                _lastReportDate = DateTime.Today;
            }

            await Task.Delay(1000, stoppingToken);
        }

        // Servis kapanırken son kaydı yaz
        FlushCurrent();
        _inputHook.Dispose();
    }

    // ── Kayıt ────────────────────────────────────────────────────────────────
    private void FlushCurrent(bool periodic = false)
    {
        if (string.IsNullOrEmpty(_currentApp)) return;

        var (keys, mouse) = InputTracker.GetAndReset();
        int durationSec = (int)(DateTime.Now - _startTime).TotalSeconds;
        if (durationSec <= 0) return;

        var appName = AppFilter.Resolve(_currentApp, _currentTitle) ?? _currentApp;

        DatabaseHelper.Insert(
            _username,
            appName,
            _currentTitle,
            durationSec,
            _activeSeconds,
            _idleSeconds,
            keys,
            mouse);

        // Periyodik kayıtta timer ve sayaçları sıfırla (uygulama hâlâ açık)
        if (periodic)
        {
            _startTime     = DateTime.Now;
            _activeSeconds = 0;
            _idleSeconds   = 0;
        }

        _logger.LogInformation(
            "[{Time}] {User} | {App} | Süre: {Dur}sn | Aktif: {Active}sn | " +
            "Tuş: {Keys} | Mouse: {Mouse}",
            DateTime.Now, _username, appName, durationSec, _activeSeconds, keys, mouse);
    }

    // ── Rapor ────────────────────────────────────────────────────────────────
    private void GenerateScheduledReports(DateTime reportDate)
    {
        try
        {
            ReportGenerator.GenerateDailyReport(reportDate);
            _logger.LogInformation("Günlük rapor oluşturuldu: {Date:dd.MM.yyyy}", reportDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Günlük rapor oluşturulamadı.");
        }

        if (reportDate.DayOfWeek == DayOfWeek.Monday)
        {
            try
            {
                ReportGenerator.GenerateWeeklyReport(reportDate.AddDays(-1));
                _logger.LogInformation("Haftalık rapor oluşturuldu.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Haftalık rapor oluşturulamadı.");
            }
        }
    }
}
