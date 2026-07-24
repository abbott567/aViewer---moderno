using System.Runtime.InteropServices;

namespace AViewer.App;

internal static partial class Win32Cursor
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Point(int X, int Y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    public static Point GetPosition() => GetCursorPos(out var point) ? point : default;
}
