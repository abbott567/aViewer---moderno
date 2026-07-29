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

    private readonly HookProcedure _hookProcedure;
    private nint _hookHandle;
    private bool _disposed;

    public GlobalKeyboardHook()
    {
        _hookProcedure = HookCallback;
    }

    public event EventHandler<FocusNavigationKeyEventArgs>? NavigationKeyReleased;

    public bool IsRunning => _hookHandle != nint.Zero;

    public void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GlobalKeyboardHook));
        }
        if (_hookHandle != nint.Zero)
        {
            return;
        }

        var moduleHandle = GetModuleHandle(null);
        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProcedure, moduleHandle, 0);
        if (_hookHandle == nint.Zero)
        {
            throw new InvalidOperationException(
                $"Could not install the keyboard hook. Win32 error: {Marshal.GetLastWin32Error()}.");
        }
    }

    public void Stop()
    {
        if (_hookHandle == nint.Zero)
        {
            return;
        }

        _ = UnhookWindowsHookEx(_hookHandle);
        _hookHandle = nint.Zero;
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (wParam == WmKeyUp || wParam == WmSysKeyUp))
        {
            var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            var navigationKey = MapNavigationKey(data.VirtualKeyCode);
            if (navigationKey is not null)
            {
                NavigationKeyReleased?.Invoke(
                    this,
                    new FocusNavigationKeyEventArgs(navigationKey.Value));
            }
        }

        return CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private static FocusNavigationKey? MapNavigationKey(uint virtualKeyCode) =>
        virtualKeyCode switch
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
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private delegate nint HookProcedure(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KeyboardHookData
    {
        public readonly uint VirtualKeyCode;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookId,
        HookProcedure hookProcedure,
        nint moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(
        nint hookHandle,
        int code,
        nint wParam,
        nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
