public static class InputTracker
{
    private static int keyCount = 0;
    private static int mouseCount = 0;

    public static void RegisterKey() => Interlocked.Increment(ref keyCount);
    public static void RegisterMouse() => Interlocked.Increment(ref mouseCount);

    public static (int keys, int mouse) GetAndReset()
    {
        int keys = Interlocked.Exchange(ref keyCount, 0);
        int mouse = Interlocked.Exchange(ref mouseCount, 0);
        return (keys, mouse);
    }
}
