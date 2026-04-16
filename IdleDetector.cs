using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

public static class IdleDetector
{
    struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;

    }
    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static uint GetIdleTime()
    {
        LASTINPUTINFO lii=new LASTINPUTINFO();
        lii.cbSize=(uint)Marshal.SizeOf(lii);

        GetLastInputInfo(ref lii);

        return ((uint)Environment.TickCount - lii.dwTime)/1000;

    }

    public static bool IsIdle()
    {
        return GetIdleTime() > 60;
    }
}
