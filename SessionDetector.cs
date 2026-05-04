using System.Runtime.InteropServices;

/// <summary>
/// Oturumun lokal mi yoksa Remote Desktop (RDP) üzerinden mi açıldığını tespit eder.
/// </summary>
public static class SessionDetector
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_REMOTESESSION = 0x1000;

    /// <summary>"LOCAL" veya "REMOTE" döner.</summary>
    public static string GetSessionType() =>
        GetSystemMetrics(SM_REMOTESESSION) != 0 ? "REMOTE" : "LOCAL";
}
