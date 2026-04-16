using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public static class ActiveWindow
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static string GetActiveWindowProcess()
    {
        IntPtr handle = GetForegroundWindow();
        _ = GetWindowThreadProcessId(handle, out uint processId);
        try
        {
            var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "Bilinmiyor";
        }
    }

    public static string GetActiveWindowTitle()
    {
        const int nChars = 256;
        StringBuilder buffer = new(nChars);
        IntPtr handle = GetForegroundWindow();

        if (GetWindowText(handle, buffer, nChars) > 0)
            return buffer.ToString();

        return "Bilinmiyor";
    }
}
