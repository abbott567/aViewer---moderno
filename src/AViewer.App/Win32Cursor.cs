using System.Runtime.InteropServices;

namespace AViewer.App;

internal static class Win32Cursor
{
    public static (int X, int Y) GetPosition()
    {
        if (!GetCursorPos(out var point)) throw new InvalidOperationException("Could not read pointer position.");
        return (point.X, point.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);
}
