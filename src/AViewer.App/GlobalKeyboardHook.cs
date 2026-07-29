using System.Runtime.InteropServices;

namespace AViewer.App;

public enum FocusNavigationKey
{
    Tab,
    ShiftTab,
    ArrowLeft,
    ArrowRight,
    ArrowUp,
    ArrowDown
}

public sealed class FocusNavigationKeyEventArgs(FocusNavigationKey navigationKey) : EventArgs
{
    public FocusNavigationKey NavigationKey { get; } = navigationKey;
}

public sealed class GlobalKeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;
    private const int VkTab = 0x09;
    private const int VkShift = 0x10;
    private const int VkLeft = 0x25;
    private const int VkUp = 0x26;
    private const int VkRight = 0x27;
    private const int VkDown = 0x28;

    private readonly HookProcedure _procedure;
    private nint _handle;
    private bool _disposed;

    public GlobalKeyboardHook() => _procedure = HookCallback;

    public event EventHandler<FocusNavigationKeyEventArgs>? NavigationKeyReleased;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_handle != nint.Zero) return;
        _handle = SetWindowsHookEx(WhKeyboardLl, _procedure, GetModuleHandle(null), 0);
        if (_handle == nint.Zero)
        {
            throw new InvalidOperationException(
                $"Could not install the keyboard hook. Win32 error: {Marshal.GetLastWin32Error()}.");
        }
    }

    public void Stop()
    {
        if (_handle == nint.Zero) return;
        _ = UnhookWindowsHookEx(_handle);
        _handle = nint.Zero;
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (wParam == WmKeyUp || wParam == WmSysKeyUp))
        {
            var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            var key = Map(data.VirtualKeyCode);
            if (key is not null)
            {
                NavigationKeyReleased?.Invoke(this, new FocusNavigationKeyEventArgs(key.Value));
            }
        }
        return CallNextHookEx(_handle, code, wParam, lParam);
    }

    private static FocusNavigationKey? Map(uint key) => key switch
    {
        VkTab => (GetAsyncKeyState(VkShift) & 0x8000) != 0
            ? FocusNavigationKey.ShiftTab
            : FocusNavigationKey.Tab,
        VkLeft => FocusNavigationKey.ArrowLeft,
        VkRight => FocusNavigationKey.ArrowRight,
        VkUp => FocusNavigationKey.ArrowUp,
        VkDown => FocusNavigationKey.ArrowDown,
        _ => null
    };

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private delegate nint HookProcedure(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProcedure procedure, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
