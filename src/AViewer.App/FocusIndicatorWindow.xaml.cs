using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AViewer.App;

public partial class FocusIndicatorWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTopmost = new(-1);

    private nint _handle;

    public FocusIndicatorWindow()
    {
        InitializeComponent();
        SourceInitialized += FocusIndicatorWindow_SourceInitialized;
        ApplyVisualSettings();
    }

    public void ApplyVisualSettings()
    {
        IndicatorRectangle.Stroke = AccessibilityVisualPalette.CurrentFocusBrush;
        IndicatorRectangle.StrokeThickness = SystemParameters.HighContrast ? 5 : 4;
    }

    private void FocusIndicatorWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLongPtr(_handle, GwlExStyle).ToInt64();
        styles |= WsExTransparent | WsExToolWindow | WsExNoActivate;
        SetWindowLongPtr(_handle, GwlExStyle, new nint(styles));
    }

    public void ShowAround(double x, double y, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            HideIndicator();
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        if (_handle == nint.Zero)
        {
            _handle = new WindowInteropHelper(this).Handle;
        }

        const int padding = 3;
        var left = checked((int)Math.Floor(x)) - padding;
        var top = checked((int)Math.Floor(y)) - padding;
        var ringWidth = Math.Max(1, checked((int)Math.Ceiling(width)) + (padding * 2));
        var ringHeight = Math.Max(1, checked((int)Math.Ceiling(height)) + (padding * 2));

        _ = SetWindowPos(
            _handle,
            HwndTopmost,
            left,
            top,
            ringWidth,
            ringHeight,
            SwpNoActivate | SwpShowWindow);
    }

    public void HideIndicator()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        SourceInitialized -= FocusIndicatorWindow_SourceInitialized;
        base.OnClosed(e);
    }

    private static nint GetWindowLongPtr(nint windowHandle, int index) =>
        nint.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : new nint(GetWindowLong32(windowHandle, index));

    private static nint SetWindowLongPtr(nint windowHandle, int index, nint newValue) =>
        nint.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, newValue)
            : new nint(SetWindowLong32(windowHandle, index, newValue.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(nint windowHandle, int index, int newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr64(nint windowHandle, int index, nint newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
