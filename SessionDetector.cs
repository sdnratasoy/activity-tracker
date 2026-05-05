using System.Runtime.InteropServices;

public static class SessionDetector
{
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_REMOTESESSION = 0x1000;

    public static string GetSessionType() =>
        GetSystemMetrics(SM_REMOTESESSION) != 0 ? "REMOTE" : "LOCAL";
}
