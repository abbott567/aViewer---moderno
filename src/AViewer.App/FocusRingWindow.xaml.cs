using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AViewer.App;

public partial class FocusRingWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;

    public FocusRingWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
            _ = SetWindowLongPtr(handle, GwlExStyle, new nint(style | WsExTransparent | WsExToolWindow | WsExNoActivate));
        };
        ApplyVisualSettings();
    }

    public void ApplyVisualSettings() => Ring.BorderBrush = AViewerOverlayPalette.CurrentFocusBrush;

    public void ShowAround(double x, double y, double width, double height)
    {
        if (width <= 0 || height <= 0 || double.IsNaN(x) || double.IsNaN(y))
        {
            HideRing();
            return;
        }
        const double inset = 3;
        Left = x - inset;
        Top = y - inset;
        Width = Math.Max(1, width + (inset * 2));
        Height = Math.Max(1, height + (inset * 2));
        if (!IsVisible) Show();
    }

    public void HideRing()
    {
        if (IsVisible) Hide();
    }

    private static nint GetWindowLongPtr(nint handle, int index) =>
        nint.Size == 8 ? GetWindowLongPtr64(handle, index) : new nint(GetWindowLong32(handle, index));

    private static nint SetWindowLongPtr(nint handle, int index, nint value) =>
        nint.Size == 8 ? SetWindowLongPtr64(handle, index, value) : new nint(SetWindowLong32(handle, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern nint GetWindowLongPtr64(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(nint handle, int index, int value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr64(nint handle, int index, nint value);
}
