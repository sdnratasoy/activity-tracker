using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string logFilePath = "log.txt";
    private readonly GlobalInputHook _inputHook = new();

    private string currentApp = "";
    private string currentTitle = "";
    private DateTime startTime;

    private int activeSeconds = 0;
    private int idleSeconds = 0;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _inputHook.Start(); 
        startTime = DateTime.Now;

        while (!stoppingToken.IsCancellationRequested)
        {
            var activeApp = ActiveWindow.GetActiveWindowProcess();
            currentTitle = ActiveWindow.GetActiveWindowTitle();

            if (IdleDetector.IsIdle())
                idleSeconds++;
            else
                activeSeconds++;

            if (activeApp != currentApp)
            {
                if (!string.IsNullOrEmpty(currentApp))
                {
                    var (keys, mouse) = InputTracker.GetAndReset(); 
                    var duration = DateTime.Now - startTime;

                    string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] " +
                                 $"App: {currentApp} | Title: {currentTitle} | " +
                                 $"Süre: {(int)duration.TotalSeconds} sn | " +
                                 $"Aktif: {activeSeconds} sn | Idle: {idleSeconds} sn | " +
                                 $"Tuş: {keys} | Mouse: {mouse}";

                    File.AppendAllText(logFilePath, log + Environment.NewLine);
                    _logger.LogInformation(log);
                }

                currentApp = activeApp;
                startTime = DateTime.Now;
                activeSeconds = 0;
                idleSeconds = 0;
            }

            await Task.Delay(1000, stoppingToken);
        }

        _inputHook.Dispose();
    }
}
