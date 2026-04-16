using System;
using System.Runtime.InteropServices;
using System.Threading;

public class GlobalInputHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX, ptY;
    }

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;

    private HookProc? _keyboardProc;
    private HookProc? _mouseProc;

    private Thread? _hookThread;

    public void Start()
    {
        _hookThread = new Thread(() =>
        {
            _keyboardProc = KeyboardCallback;
            _mouseProc = MouseCallback;

            IntPtr hMod = GetModuleHandle(null);
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);

            // Hook'ların çalışması için mesaj döngüsü gerekiyor
            while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        });

        _hookThread.IsBackground = true;
        _hookThread.Start();
    }

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            InputTracker.RegisterKey();

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                InputTracker.RegisterMouse();
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }
}
