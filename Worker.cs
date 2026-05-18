using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private readonly string _username     = Environment.UserName;
    private readonly string _computerName = Environment.MachineName;
    private string _sessionType           = "LOCAL";

    private string   _currentApp   = "";
    private string   _currentTitle = "";
    private DateTime _startTime;
    private int      _activeSeconds = 0;
    private int      _idleSeconds   = 0;

    private DateTime _lastFlushTime  = DateTime.Now;
    private const int FlushIntervalSec = 60;

    private DateTime _lastSyncTime  = DateTime.MinValue;
    private const int SyncIntervalMin = 5;

    private DateTime _lastReportDate = DateTime.Today;

    private readonly GlobalInputHook _inputHook = new();

    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        _logger = logger;

        var cs = config.GetConnectionString("CentralDb");
        if (!string.IsNullOrWhiteSpace(cs))
        {
            CentralDbHelper.Configure(cs);
            AccessSyncHelper.Configure(cs);
            SyncService.Configure(logger, cs);
        }
        else
        {
            _logger.LogWarning("CentralDb bağlantı dizesi bulunamadı. Sync devre dışı.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DatabaseHelper.Initialize();
        _inputHook.Start();
        _startTime = DateTime.Now;

        _logger.LogInformation(
            "ActivityTracker başladı. Kullanıcı: {User} | Makine: {PC}",
            _username, _computerName);

        GenerateScheduledReports(DateTime.Today);

        while (!stoppingToken.IsCancellationRequested)
        {
            _sessionType = SessionDetector.GetSessionType();

            var rawProcess = ActiveWindow.GetActiveWindowProcess();
            var rawTitle   = ActiveWindow.GetActiveWindowTitle();

            if (IdleDetector.IsIdle()) _idleSeconds++;
            else                       _activeSeconds++;

            var resolvedNew     = AppFilter.Resolve(rawProcess, rawTitle)     ?? rawProcess;
            var resolvedCurrent = AppFilter.Resolve(_currentApp, _currentTitle) ?? _currentApp;

            if (rawProcess != _currentApp || resolvedNew != resolvedCurrent)
            {
                FlushCurrent();
                _currentApp    = rawProcess;
                _currentTitle  = rawTitle;
                _startTime     = DateTime.Now;
                _activeSeconds = 0;
                _idleSeconds   = 0;
                _lastFlushTime = DateTime.Now;
            }
            else if ((DateTime.Now - _lastFlushTime).TotalSeconds >= FlushIntervalSec)
            {
                FlushCurrent(periodic: true);
                _lastFlushTime = DateTime.Now;
            }

            if ((DateTime.Now - _lastSyncTime).TotalMinutes >= SyncIntervalMin)
            {
                _lastSyncTime = DateTime.Now;
                _ = Task.Run(SyncService.TrySync, stoppingToken);
            }

            if (DateTime.Today > _lastReportDate)
            {
                GenerateScheduledReports(_lastReportDate);
                _lastReportDate = DateTime.Today;
            }

            await Task.Delay(1000, stoppingToken);
        }

        FlushCurrent();
        SyncService.TrySync();
        _inputHook.Dispose();
    }

    private void FlushCurrent(bool periodic = false)
    {
        if (string.IsNullOrEmpty(_currentApp)) return;

        var (keys, mouse) = InputTracker.GetAndReset();
        int durationSec   = (int)(DateTime.Now - _startTime).TotalSeconds;
        if (durationSec <= 0) return;

        var appName = AppFilter.Resolve(_currentApp, _currentTitle) ?? _currentApp;

        DatabaseHelper.Insert(
            _username, _computerName, _sessionType,
            appName, _currentTitle,
            durationSec, _activeSeconds, _idleSeconds, keys, mouse);

        if (periodic)
        {
            _startTime     = DateTime.Now;
            _activeSeconds = 0;
            _idleSeconds   = 0;
        }

        _logger.LogInformation(
            "[{Time}] {User}@{PC} [{Session}] | {App} | {Dur}sn | Tuş: {Keys} | Mouse: {Mouse}",
            DateTime.Now, _username, _computerName, _sessionType,
            appName, durationSec, keys, mouse);
    }

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
