using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AViewer.Core.Models;
using AViewer.Core.Services;
using Microsoft.Win32;

namespace AViewer.App;

public partial class MainWindow : Window
{
    private enum InspectionMode
    {
        None,
        Pointer,
        Focus
    }

    private readonly IAccessibilityInspector _inspector = new Uia3Inspector();
    private readonly DispatcherTimer _inspectionTimer;
    private readonly FocusRingWindow _inspectionRing = new();
    private readonly RelationshipOverlayWindow _relationshipOverlay = new();
    private readonly FocusOrderOverlayWindow _focusOrderOverlay = new();
    private readonly GlobalKeyboardHook _keyboardHook = new();
    private readonly List<FocusOrderStep> _focusOrderSteps = [];
    private readonly PropertyFilterService _propertyFilter = new();
    private readonly AppSettingsService _appSettings = new();
    private readonly AccessibilityPreferencesService _accessibilityPreferences = new();
    private AccessibilityInspectionSnapshot? _snapshot;
    private AccessibilityNode? _activeRoot;
    private AccessibilityNode? _selectedNode;
    private InspectionMode _inspectionMode;
    private bool _inspectionBusy;
    private int _inspectionGeneration;
    private string? _lastElementId;
    private DateTime _lastElementRefreshUtc = DateTime.MinValue;
    private bool _recordingFocusOrder;
    private bool _includeArrowNavigation;
    private int _focusCaptureGeneration;

    public MainWindow()
    {
        InitializeComponent();

        _inspectionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _inspectionTimer.Tick += InspectionTimer_Tick;

        Topmost = _appSettings.AlwaysOnTop;
        AlwaysOnTopCheckBox.IsChecked = Topmost;
        ShowRelationshipsCheckBox.IsChecked = _appSettings.ShowRelationships;
        _includeArrowNavigation = _appSettings.IncludeArrowNavigation;
        IncludeArrowNavigationCheckBox.IsChecked = _includeArrowNavigation;
        _keyboardHook.NavigationKeyReleased += KeyboardHook_NavigationKeyReleased;
        _accessibilityPreferences.PreferencesChanged += AccessibilityPreferencesChanged;
        ApplyAccessibilityPreferences();
        ApplyActiveApi();
    }


    private void AccessibilityPreferencesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(ApplyAccessibilityPreferences);
    }

    private void ApplyAccessibilityPreferences()
    {
        _inspectionRing.ApplyVisualSettings();
        RefreshRelationshipOverlay();

        if (_focusOrderSteps.Count > 0)
        {
            _focusOrderOverlay.ShowPath(_focusOrderSteps);
        }

    }

    private int Depth => int.TryParse(
        (DepthBox.SelectedItem as ComboBoxItem)?.Content?.ToString(),
        out var value)
            ? value
            : 2;

    private string ActiveApi => ApiTabControl.SelectedIndex switch
    {
        1 => "MSAA",
        2 => "IA2",
        _ => "UIA"
    };

    private void InspectPointer_Click(object sender, RoutedEventArgs e)
    {
        SetInspectionMode(
            _inspectionMode == InspectionMode.Pointer
                ? InspectionMode.None
                : InspectionMode.Pointer);
    }

    private void InspectFocus_Click(object sender, RoutedEventArgs e)
    {
        SetInspectionMode(
            _inspectionMode == InspectionMode.Focus
                ? InspectionMode.None
                : InspectionMode.Focus);
    }

    private void SetInspectionMode(InspectionMode mode)
    {
        _inspectionGeneration++;
        _inspectionMode = mode;
        _lastElementId = null;
        _lastElementRefreshUtc = DateTime.MinValue;

        InspectPointerButton.Content = mode == InspectionMode.Pointer
            ? "Stop _pointer inspection"
            : "_Inspect pointer";
        InspectPointerButton.IsEnabled = mode != InspectionMode.Focus;

        InspectFocusButton.Content = mode == InspectionMode.Focus
            ? "Stop _focus inspection"
            : "Inspect _focus";
        InspectFocusButton.IsEnabled = mode != InspectionMode.Pointer;

        if (mode == InspectionMode.None)
        {
            _inspectionTimer.Stop();
            _inspectionRing.HideRing();
            _relationshipOverlay.HideOverlay();
            StatusText.Text = _activeRoot is null ? "Ready" : "Inspection stopped";
            return;
        }

        _inspectionTimer.Start();
        StatusText.Text = mode == InspectionMode.Pointer
            ? "Pointer inspection active. Move the pointer over another application; press Escape to stop."
            : "Focus inspection active. Move keyboard focus to another application; press Escape to stop.";
    }

    private async void InspectionTimer_Tick(object? sender, EventArgs e)
    {
        if (_inspectionMode == InspectionMode.None || _inspectionBusy)
        {
            return;
        }

        var generation = _inspectionGeneration;
        var mode = _inspectionMode;
        var depth = Depth;
        _inspectionBusy = true;

        try
        {
            var result = await Task.Run(() => InspectCurrentTarget(mode, depth).Snapshot);

            if (generation != _inspectionGeneration || mode != _inspectionMode)
            {
                return;
            }

            var targetRoot = result.UiaRoot;
            if (targetRoot is null || targetRoot.ProcessId == Environment.ProcessId)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (string.Equals(targetRoot.Id, _lastElementId, StringComparison.Ordinal)
                && now - _lastElementRefreshUtc < TimeSpan.FromMilliseconds(750))
            {
                return;
            }

            _lastElementId = targetRoot.Id;
            _lastElementRefreshUtc = now;
            ShowResult(result, mode == InspectionMode.Pointer ? "Pointer" : "Keyboard focus");
        }
        catch (Exception ex)
        {
            if (generation == _inspectionGeneration)
            {
                SetInspectionMode(InspectionMode.None);
                StatusText.Text = $"Inspection failed: {ex.Message}";
            }
        }
        finally
        {
            _inspectionBusy = false;
        }
    }

    private (AccessibilityInspectionSnapshot Snapshot, string Source) InspectCurrentTarget(
        InspectionMode mode,
        int depth)
    {
        if (mode == InspectionMode.Pointer)
        {
            var point = Win32Cursor.GetPosition();
            return (_inspector.InspectPoint(point.X, point.Y, depth), $"Pointer: {point.X}, {point.Y}");
        }

        return (_inspector.InspectFocused(depth), "Keyboard focus");
    }

    private void ShowResult(AccessibilityInspectionSnapshot snapshot, string source)
    {
        _snapshot = snapshot;
        ApplyActiveApi();

        if (_activeRoot is not null)
        {
            _inspectionRing.ShowAround(
                _activeRoot.BoundingX,
                _activeRoot.BoundingY,
                _activeRoot.BoundingWidth,
                _activeRoot.BoundingHeight);
        }
        else
        {
            _inspectionRing.HideRing();
        }

        RefreshRelationshipOverlay();
        StatusText.Text = _activeRoot is null
            ? $"No {ActiveApi} element found ({source})"
            : $"{source} · {ActiveApi}: {_activeRoot.ControlType} — {_activeRoot.Name}";
    }

    private void ApplyActiveApi()
    {
        _activeRoot = _snapshot?.RootFor(ActiveApi);
        _selectedNode = _activeRoot;
        NodeTree.ItemsSource = _activeRoot is null ? null : new[] { _activeRoot };
        TreeGroupBox.Header = $"{(ActiveApi == "IA2" ? "IAccessible2" : ActiveApi)} accessibility tree";
        RefreshDisplayedProperties();
        RefreshRelationshipOverlay();

        if (_activeRoot is not null && _inspectionMode != InspectionMode.None)
        {
            _inspectionRing.ShowAround(
                _activeRoot.BoundingX,
                _activeRoot.BoundingY,
                _activeRoot.BoundingWidth,
                _activeRoot.BoundingHeight);
        }
    }

    private void ApiTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || !ReferenceEquals(e.Source, ApiTabControl))
        {
            return;
        }

        ApplyActiveApi();
        StatusText.Text = $"Showing {TreeGroupBox.Header}";
    }

    private void NodeTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is AccessibilityNode node)
        {
            _selectedNode = node;
            RefreshDisplayedProperties();
            RefreshRelationshipOverlay();
            if (_inspectionMode != InspectionMode.None)
            {
                _inspectionRing.ShowAround(
                    node.BoundingX,
                    node.BoundingY,
                    node.BoundingWidth,
                    node.BoundingHeight);
            }
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_recordingFocusOrder)
        {
            SetFocusOrderRecording(false);
            e.Handled = true;
        }
        else if (_inspectionMode != InspectionMode.None)
        {
            SetInspectionMode(InspectionMode.None);
            e.Handled = true;
        }
    }

    private void RecordFocusOrder_Click(object sender, RoutedEventArgs e)
    {
        SetFocusOrderRecording(!_recordingFocusOrder);
    }

    private void SetFocusOrderRecording(bool enabled)
    {
        _recordingFocusOrder = enabled;
        _focusCaptureGeneration++;
        RecordFocusOrderButton.Content = enabled
            ? "Stop _recording focus order"
            : "_Record focus order";

        if (enabled)
        {
            try
            {
                _keyboardHook.Start();
                StatusText.Text = _includeArrowNavigation
                    ? "Recording Tab, Shift+Tab and arrow-key focus transitions outside AViewer."
                    : "Recording Tab and Shift+Tab focus stops outside AViewer.";
            }
            catch (Exception ex)
            {
                _recordingFocusOrder = false;
                RecordFocusOrderButton.Content = "_Record focus order";
                StatusText.Text = $"Could not start focus-order recording: {ex.Message}";
            }
        }
        else
        {
            _keyboardHook.Stop();
            StatusText.Text = _focusOrderSteps.Count == 0
                ? "Focus-order recording stopped; no external focus stops were captured."
                : $"Focus-order recording stopped with {_focusOrderSteps.Count} stops.";
        }
    }

    private void ClearFocusPath_Click(object sender, RoutedEventArgs e)
    {
        _focusOrderSteps.Clear();
        _focusOrderOverlay.HideOverlay();
        StatusText.Text = "Focus-order path cleared";
    }

    private void IncludeArrowNavigation_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = IncludeArrowNavigationCheckBox.IsChecked == true;
        _includeArrowNavigation = enabled;
        _appSettings.IncludeArrowNavigation = enabled;
        StatusText.Text = enabled
            ? "Arrow-key focus transitions will be included"
            : "Only Tab and Shift+Tab focus stops will be recorded";
    }

    private void KeyboardHook_NavigationKeyReleased(
        object? sender,
        FocusNavigationKeyEventArgs e)
    {
        if (!_recordingFocusOrder ||
            (IsArrowKey(e.NavigationKey) && !_includeArrowNavigation))
        {
            return;
        }

        var generation = ++_focusCaptureGeneration;
        _ = Dispatcher.InvokeAsync(
            async () => await CaptureFocusStopAsync(e.NavigationKey, generation),
            DispatcherPriority.Background);
    }

    private async Task CaptureFocusStopAsync(
        FocusNavigationKey navigationKey,
        int generation)
    {
        await Task.Delay(120);
        if (!_recordingFocusOrder || generation != _focusCaptureGeneration)
        {
            return;
        }

        AccessibilityInspectionSnapshot snapshot;
        try
        {
            snapshot = await Task.Run(() => _inspector.InspectFocused(0));
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not read the focused element: {ex.Message}";
            return;
        }

        if (!_recordingFocusOrder || generation != _focusCaptureGeneration)
        {
            return;
        }

        var focused = snapshot.RootFor(ActiveApi) ?? snapshot.UiaRoot;
        if (focused is null || focused.ProcessId == Environment.ProcessId)
        {
            return;
        }

        var previous = _focusOrderSteps.LastOrDefault();
        if (previous is not null && string.Equals(
                previous.Element.Id,
                focused.Id,
                StringComparison.Ordinal))
        {
            return;
        }

        _focusOrderSteps.Add(new FocusOrderStep(
            _focusOrderSteps.Count + 1,
            navigationKey,
            focused));
        _focusOrderOverlay.ShowPath(_focusOrderSteps);
        StatusText.Text = $"Focus stop {_focusOrderSteps.Count}: {focused.ControlType} — {focused.Name}";
    }

    private static bool IsArrowKey(FocusNavigationKey key) =>
        key is FocusNavigationKey.ArrowLeft or
            FocusNavigationKey.ArrowRight or
            FocusNavigationKey.ArrowUp or
            FocusNavigationKey.ArrowDown;

    private void ShowRelationships_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = ShowRelationshipsCheckBox.IsChecked == true;
        _appSettings.ShowRelationships = enabled;
        RefreshRelationshipOverlay();
        StatusText.Text = enabled ? "Relationship visualization enabled" : "Relationship visualization disabled";
    }

    private void RefreshRelationshipOverlay()
    {
        if (ShowRelationshipsCheckBox.IsChecked == true && _selectedNode is not null)
        {
            _relationshipOverlay.ShowRelationships(_selectedNode);
        }
        else
        {
            _relationshipOverlay.HideOverlay();
        }
    }

    private void AlwaysOnTop_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = AlwaysOnTopCheckBox.IsChecked == true;
        Topmost = enabled;
        _appSettings.AlwaysOnTop = enabled;
        StatusText.Text = enabled ? "Always on top enabled" : "Always on top disabled";
    }

    private void ChooseProperties_Click(object sender, RoutedEventArgs e)
    {
        var availableProperties = EnumerateSnapshotProperties(_snapshot).ToList();
        var choices = _propertyFilter.GetChoices(availableProperties);
        var dialog = new PropertySelectionWindow(choices)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _propertyFilter.Apply(dialog.Choices);
        RefreshDisplayedProperties();
        StatusText.Text = $"Displaying {_propertyFilter.Filter(_selectedNode?.Properties ?? []).Count} selected properties";
    }

    private void RefreshDisplayedProperties()
    {
        UiaPropertyGrid.ItemsSource = null;
        MsaaPropertyGrid.ItemsSource = null;
        Ia2PropertyGrid.ItemsSource = null;

        if (_selectedNode is null)
        {
            return;
        }

        var visibleProperties = _propertyFilter.Filter(_selectedNode.Properties);
        switch (ActiveApi)
        {
            case "MSAA":
                MsaaPropertyGrid.ItemsSource = visibleProperties.ToList();
                break;
            case "IA2":
                Ia2PropertyGrid.ItemsSource = visibleProperties.ToList();
                break;
            default:
                UiaPropertyGrid.ItemsSource = visibleProperties.ToList();
                break;
        }
    }

    private static IEnumerable<AccessibilityProperty> EnumerateSnapshotProperties(
        AccessibilityInspectionSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            yield break;
        }

        foreach (var root in new[] { snapshot.UiaRoot, snapshot.MsaaRoot, snapshot.Ia2Root })
        {
            foreach (var property in EnumerateProperties(root))
            {
                yield return property;
            }
        }
    }

    private static IEnumerable<AccessibilityProperty> EnumerateProperties(AccessibilityNode? node)
    {
        if (node is null)
        {
            yield break;
        }

        foreach (var property in node.Properties)
        {
            yield return property;
        }

        foreach (var child in node.Children)
        {
            foreach (var property in EnumerateProperties(child))
            {
                yield return property;
            }
        }
    }

    private void CopyJson_Click(object sender, RoutedEventArgs e)
    {
        if (_activeRoot is null)
        {
            return;
        }

        Clipboard.SetText(JsonExportService.Serialize(_activeRoot));
        StatusText.Text = $"{ActiveApi} JSON copied";
    }

    private void SaveJson_Click(object sender, RoutedEventArgs e)
    {
        if (_activeRoot is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            FileName = $"{ActiveApi.ToLowerInvariant()}-accessibility-tree.json"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, JsonExportService.Serialize(_activeRoot));
        StatusText.Text = $"Saved {dialog.FileName}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _inspectionGeneration++;
        _inspectionTimer.Stop();
        _inspectionTimer.Tick -= InspectionTimer_Tick;
        _inspectionRing.Close();
        _relationshipOverlay.Close();
        _focusOrderOverlay.Close();
        _keyboardHook.NavigationKeyReleased -= KeyboardHook_NavigationKeyReleased;
        _accessibilityPreferences.PreferencesChanged -= AccessibilityPreferencesChanged;
        _accessibilityPreferences.Dispose();
        _keyboardHook.Dispose();
        _inspector.Dispose();
        base.OnClosed(e);
    }
}
