using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AViewer.Core.Models;
using AViewer.Core.Services;
using Microsoft.Win32;

namespace AViewer.App;

public partial class MainWindow : Window
{
    private const int CompleteTreeMaxDepth = 64;
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
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        PopulateHelpMenu();
        ApplyActiveApi();
    }

    private int Depth
    {
        get
        {
            var value = (DepthBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
            {
                return CompleteTreeMaxDepth;
            }
            return int.TryParse(value, out var depth) ? depth : 2;
        }
    }

    private string ActiveApi => ApiTabControl.SelectedIndex switch
    {
        1 => "MSAA",
        2 => "IA2",
        _ => "UIA"
    };

    private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _inspectionRing.ApplyVisualSettings();
        RefreshRelationshipOverlay();
        if (_focusOrderSteps.Count > 0) _focusOrderOverlay.ShowPath(_focusOrderSteps);
    }

    private void InspectPointer_Click(object sender, RoutedEventArgs e) =>
        SetInspectionMode(_inspectionMode == InspectionMode.Pointer ? InspectionMode.None : InspectionMode.Pointer);

    private void InspectFocus_Click(object sender, RoutedEventArgs e) =>
        SetInspectionMode(_inspectionMode == InspectionMode.Focus ? InspectionMode.None : InspectionMode.Focus);

    private void SetInspectionMode(InspectionMode mode)
    {
        _inspectionGeneration++;
        _inspectionMode = mode;
        _lastElementId = null;
        _lastElementRefreshUtc = DateTime.MinValue;

        var pointerActive = mode == InspectionMode.Pointer;
        var focusActive = mode == InspectionMode.Focus;
        InspectPointerButton.IsChecked = pointerActive;
        PointerToolbarButton.IsChecked = pointerActive;
        InspectFocusButton.IsChecked = focusActive;
        FocusToolbarButton.IsChecked = focusActive;

        AutomationProperties.SetHelpText(
            PointerToolbarButton,
            pointerActive
                ? "Pointer inspection is on. Activate to turn it off."
                : "Pointer inspection is off. Activate to turn it on.");
        AutomationProperties.SetHelpText(
            FocusToolbarButton,
            focusActive
                ? "Keyboard focus inspection is on. Activate to turn it off."
                : "Keyboard focus inspection is off. Activate to turn it on.");

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
            ? "Pointer inspection active"
            : "Keyboard focus inspection active";
    }

    private async void InspectionTimer_Tick(object? sender, EventArgs e)
    {
        if (_inspectionMode == InspectionMode.None || _inspectionBusy) return;

        var generation = _inspectionGeneration;
        var mode = _inspectionMode;
        var depth = Depth;
        _inspectionBusy = true;

        try
        {
            var snapshot = await Task.Run(() => InspectCurrentTarget(mode, depth));
            if (generation != _inspectionGeneration || mode != _inspectionMode) return;

            var identityRoot = snapshot.UiaRoot ?? snapshot.RootFor(ActiveApi);
            if (identityRoot is null || identityRoot.ProcessId == Environment.ProcessId) return;

            var now = DateTime.UtcNow;
            if (string.Equals(identityRoot.Id, _lastElementId, StringComparison.Ordinal) &&
                now - _lastElementRefreshUtc < TimeSpan.FromMilliseconds(750))
            {
                return;
            }

            _lastElementId = identityRoot.Id;
            _lastElementRefreshUtc = now;
            ShowResult(snapshot, mode == InspectionMode.Pointer ? "Pointer" : "Keyboard focus");
        }
        catch (Exception exception)
        {
            if (generation == _inspectionGeneration)
            {
                SetInspectionMode(InspectionMode.None);
                StatusText.Text = $"Inspection failed: {exception.Message}";
            }
        }
        finally
        {
            _inspectionBusy = false;
        }
    }

    private AccessibilityInspectionSnapshot InspectCurrentTarget(InspectionMode mode, int depth)
    {
        if (mode == InspectionMode.Pointer)
        {
            var point = Win32Cursor.GetPosition();
            return _inspector.InspectPoint(point.X, point.Y, depth);
        }
        return _inspector.InspectFocused(depth);
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
            ? $"No {ActiveApi} element found from {source}."
            : $"{source}: {ActiveApi} {_activeRoot.ControlType} — {_activeRoot.Name}";
    }

    private void ApplyActiveApi()
    {
        _activeRoot = _snapshot?.RootFor(ActiveApi);
        _selectedNode = _activeRoot;
        NodeTree.ItemsSource = _activeRoot is null ? null : new[] { _activeRoot };
        TreeHeading.Text = $"{(ActiveApi == "IA2" ? "IAccessible2" : ActiveApi)} accessibility tree";
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
        if (!IsLoaded || !ReferenceEquals(e.Source, ApiTabControl)) return;
        ApplyActiveApi();
        StatusText.Text = $"Showing {TreeHeading.Text}";
    }

    private void NodeTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not AccessibilityNode node) return;
        _selectedNode = node;
        RefreshDisplayedProperties();
        RefreshRelationshipOverlay();
        if (_inspectionMode != InspectionMode.None)
        {
            _inspectionRing.ShowAround(node.BoundingX, node.BoundingY, node.BoundingWidth, node.BoundingHeight);
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F7) { InspectPointer_Click(sender, e); e.Handled = true; return; }
        if (e.Key == Key.F8) { InspectFocus_Click(sender, e); e.Handled = true; return; }
        if (e.Key == Key.F9) { RecordFocusOrder_Click(sender, e); e.Handled = true; return; }
        if (e.Key == Key.A && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            LoadCompleteTree_Click(sender, e); e.Handled = true; return;
        }
        if (e.Key == Key.Up && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            UpOneLevel_Click(sender, e); e.Handled = true; return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            CopyJson_Click(sender, e); e.Handled = true; return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            SaveJson_Click(sender, e); e.Handled = true; return;
        }
        if (e.Key != Key.Escape) return;

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

    private void RecordFocusOrder_Click(object sender, RoutedEventArgs e) =>
        SetFocusOrderRecording(!_recordingFocusOrder);

    private void SetFocusOrderRecording(bool enabled)
    {
        _recordingFocusOrder = enabled;
        _focusCaptureGeneration++;
        RecordFocusOrderButton.Header = enabled ? "Stop _recording focus order" : "_Record focus order";
        FocusOrderToolbarButton.Content = enabled ? "Stop focus _order" : "Focus _order";

        if (enabled)
        {
            try
            {
                _keyboardHook.Start();
                StatusText.Text = _includeArrowNavigation
                    ? "Recording Tab, Shift+Tab and arrow-key focus transitions outside AViewer."
                    : "Recording Tab and Shift+Tab focus stops outside AViewer.";
            }
            catch (Exception exception)
            {
                _recordingFocusOrder = false;
                RecordFocusOrderButton.Header = "_Record focus order";
                FocusOrderToolbarButton.Content = "Focus _order";
                StatusText.Text = $"Could not start focus-order recording: {exception.Message}";
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
        if (!IsInitialized) return;
        _includeArrowNavigation = IncludeArrowNavigationCheckBox.IsChecked == true;
        _appSettings.IncludeArrowNavigation = _includeArrowNavigation;
        StatusText.Text = _includeArrowNavigation
            ? "Arrow-key focus transitions will be included"
            : "Only Tab and Shift+Tab focus stops will be recorded";
    }

    private void KeyboardHook_NavigationKeyReleased(object? sender, FocusNavigationKeyEventArgs e)
    {
        if (!_recordingFocusOrder || (IsArrowKey(e.NavigationKey) && !_includeArrowNavigation)) return;
        var generation = ++_focusCaptureGeneration;
        _ = Dispatcher.InvokeAsync(
            async () => await CaptureFocusStopAsync(e.NavigationKey, generation),
            DispatcherPriority.Background);
    }

    private async Task CaptureFocusStopAsync(FocusNavigationKey navigationKey, int generation)
    {
        await Task.Delay(120);
        if (!_recordingFocusOrder || generation != _focusCaptureGeneration) return;

        AccessibilityInspectionSnapshot snapshot;
        try { snapshot = await Task.Run(() => _inspector.InspectFocused(0)); }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not read the focused element: {exception.Message}";
            return;
        }

        if (!_recordingFocusOrder || generation != _focusCaptureGeneration) return;
        var focused = snapshot.RootFor(ActiveApi) ?? snapshot.UiaRoot;
        if (focused is null || focused.ProcessId == Environment.ProcessId) return;

        var previous = _focusOrderSteps.LastOrDefault();
        if (previous is not null && string.Equals(previous.Element.Id, focused.Id, StringComparison.Ordinal)) return;

        _focusOrderSteps.Add(new FocusOrderStep(_focusOrderSteps.Count + 1, navigationKey, focused));
        _focusOrderOverlay.ShowPath(_focusOrderSteps);
        StatusText.Text = $"Focus stop {_focusOrderSteps.Count}: {focused.ControlType} — {focused.Name}";
    }

    private static bool IsArrowKey(FocusNavigationKey key) => key is
        FocusNavigationKey.ArrowLeft or FocusNavigationKey.ArrowRight or
        FocusNavigationKey.ArrowUp or FocusNavigationKey.ArrowDown;

    private void ShowRelationships_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        var enabled = ShowRelationshipsCheckBox.IsChecked == true;
        _appSettings.ShowRelationships = enabled;
        RefreshRelationshipOverlay();
        StatusText.Text = enabled ? "Relationship visualization enabled" : "Relationship visualization disabled";
    }

    private void RefreshRelationshipOverlay()
    {
        if (ShowRelationshipsCheckBox.IsChecked == true && _selectedNode is not null)
        {
            _relationshipOverlay.ShowRelationships(BuildRelationshipVisualizationNode(_selectedNode));
        }
        else
        {
            _relationshipOverlay.HideOverlay();
        }
    }

    private AccessibilityNode BuildRelationshipVisualizationNode(AccessibilityNode selected)
    {
        var merged = new List<AccessibilityRelationship>();
        AddDistinctRelationships(merged, selected.Relationships);

        if (_snapshot is not null)
        {
            foreach (var root in new[] { _snapshot.UiaRoot, _snapshot.MsaaRoot, _snapshot.Ia2Root })
            {
                if (root is null) continue;
                var matching = FindBestMatchingNode(root, selected);
                if (matching is not null) AddDistinctRelationships(merged, matching.Relationships);
            }
        }

        var resolvedRelationships = RelationshipTargetBoundsResolver.Resolve(
            merged,
            selected,
            _snapshot?.UiaRoot,
            _snapshot?.MsaaRoot,
            _snapshot?.Ia2Root);

        return new AccessibilityNode
        {
            Api = selected.Api,
            Id = selected.Id,
            Name = selected.Name,
            ControlType = selected.ControlType,
            Framework = selected.Framework,
            AutomationId = selected.AutomationId,
            ClassName = selected.ClassName,
            ProcessName = selected.ProcessName,
            ProcessId = selected.ProcessId,
            BoundingRectangle = selected.BoundingRectangle,
            BoundingX = selected.BoundingX,
            BoundingY = selected.BoundingY,
            BoundingWidth = selected.BoundingWidth,
            BoundingHeight = selected.BoundingHeight,
            IsEnabled = selected.IsEnabled,
            IsKeyboardFocusable = selected.IsKeyboardFocusable,
            HasKeyboardFocus = selected.HasKeyboardFocus,
            Properties = selected.Properties,
            Relationships = resolvedRelationships,
            Children = selected.Children
        };
    }

    private static AccessibilityNode? FindBestMatchingNode(AccessibilityNode root, AccessibilityNode selected)
    {
        AccessibilityNode? best = null;
        var bestScore = double.MinValue;
        foreach (var candidate in EnumerateNodes(root))
        {
            var score = RelationshipNodeMatchScore(selected, candidate);
            if (score <= bestScore) continue;
            best = candidate;
            bestScore = score;
        }
        return bestScore >= 1.0 ? best : null;
    }

    private static IEnumerable<AccessibilityNode> EnumerateNodes(AccessibilityNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in EnumerateNodes(child)) yield return descendant;
        }
    }

    private static double RelationshipNodeMatchScore(AccessibilityNode first, AccessibilityNode second)
    {
        if (first.BoundingWidth <= 0 || first.BoundingHeight <= 0 ||
            second.BoundingWidth <= 0 || second.BoundingHeight <= 0)
        {
            return double.MinValue;
        }

        var firstRight = first.BoundingX + first.BoundingWidth;
        var firstBottom = first.BoundingY + first.BoundingHeight;
        var secondRight = second.BoundingX + second.BoundingWidth;
        var secondBottom = second.BoundingY + second.BoundingHeight;
        var intersectionWidth = Math.Max(0, Math.Min(firstRight, secondRight) - Math.Max(first.BoundingX, second.BoundingX));
        var intersectionHeight = Math.Max(0, Math.Min(firstBottom, secondBottom) - Math.Max(first.BoundingY, second.BoundingY));
        var intersectionArea = intersectionWidth * intersectionHeight;
        var smallerArea = Math.Min(first.BoundingWidth * first.BoundingHeight, second.BoundingWidth * second.BoundingHeight);
        var overlap = smallerArea > 0 ? intersectionArea / smallerArea : 0;

        var centreDistance = Math.Sqrt(
            Math.Pow(first.BoundingX + (first.BoundingWidth / 2) - second.BoundingX - (second.BoundingWidth / 2), 2) +
            Math.Pow(first.BoundingY + (first.BoundingHeight / 2) - second.BoundingY - (second.BoundingHeight / 2), 2));
        var size = Math.Max(1, Math.Min(first.BoundingWidth, first.BoundingHeight));
        var proximity = Math.Max(0, 1 - (centreDistance / Math.Max(20, size)));
        var score = (overlap * 4) + proximity;
        if (!string.IsNullOrWhiteSpace(first.Name) && string.Equals(first.Name, second.Name, StringComparison.Ordinal)) score += 1;
        return score;
    }

    private static void AddDistinctRelationships(
        ICollection<AccessibilityRelationship> destination,
        IEnumerable<AccessibilityRelationship> source)
    {
        foreach (var relationship in source)
        {
            var duplicate = destination.Any(existing =>
                string.Equals(existing.Type, relationship.Type, StringComparison.OrdinalIgnoreCase) &&
                SameRelationshipTarget(existing, relationship));
            if (!duplicate) destination.Add(relationship);
        }
    }

    private static bool SameRelationshipTarget(
        AccessibilityRelationship first,
        AccessibilityRelationship second)
    {
        if (!string.IsNullOrWhiteSpace(first.TargetId) &&
            !string.IsNullOrWhiteSpace(second.TargetId) &&
            string.Equals(first.TargetId, second.TargetId, StringComparison.Ordinal)) return true;

        const double tolerance = 4;
        return Math.Abs(first.TargetX - second.TargetX) <= tolerance &&
               Math.Abs(first.TargetY - second.TargetY) <= tolerance &&
               Math.Abs(first.TargetWidth - second.TargetWidth) <= tolerance &&
               Math.Abs(first.TargetHeight - second.TargetHeight) <= tolerance;
    }

    private void AlwaysOnTop_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        var enabled = AlwaysOnTopCheckBox.IsChecked == true;
        Topmost = enabled;
        _appSettings.AlwaysOnTop = enabled;
        StatusText.Text = enabled ? "Always on top enabled" : "Always on top disabled";
    }

    private void ChooseProperties_Click(object sender, RoutedEventArgs e)
    {
        var properties = ApiPropertyCatalog.All
            .Concat(EnumerateSnapshotProperties(_snapshot))
            .ToList();
        var dialog = new PropertySelectionWindow(
            _propertyFilter.GetChoices(properties),
            _appSettings.ShowUnavailableProperties)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true) return;

        _propertyFilter.Apply(dialog.AllChoices);
        _appSettings.ShowUnavailableProperties = dialog.ShowUnavailableProperties;
        RefreshDisplayedProperties();
        StatusText.Text = _selectedNode is null
            ? "Preferences saved"
            : $"Displaying {_propertyFilter.Filter(BuildDisplayedProperties(_selectedNode)).Count} selected properties";
    }

    private void RefreshDisplayedProperties()
    {
        UiaPropertyGrid.ItemsSource = null;
        MsaaPropertyGrid.ItemsSource = null;
        Ia2PropertyGrid.ItemsSource = null;
        if (_selectedNode is null) return;

        var displayed = BuildDisplayedProperties(_selectedNode);
        var visible = _propertyFilter.Filter(displayed);
        switch (ActiveApi)
        {
            case "MSAA": MsaaPropertyGrid.ItemsSource = visible; break;
            case "IA2": Ia2PropertyGrid.ItemsSource = visible; break;
            default: UiaPropertyGrid.ItemsSource = visible; break;
        }
    }

    private IReadOnlyList<AccessibilityProperty> BuildDisplayedProperties(AccessibilityNode node)
    {
        var relationshipNode = BuildRelationshipVisualizationNode(node);
        var properties = BuildRelationshipProperties(relationshipNode.Relationships).ToList();
        properties.AddRange(node.Properties
            .Where(property => !IsInspectorRelationshipProperty(property)));

        if (_appSettings.ShowUnavailableProperties)
        {
            var existing = properties
                .Select(PropertyKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var property in ApiPropertyCatalog.ForApi(node.Api))
            {
                if (existing.Add(PropertyKey(property)))
                {
                    properties.Add(new AccessibilityProperty(property.Group, property.Name, "Not exposed"));
                }
            }
        }

        return properties
            .OrderBy(property => IsInspectorRelationshipProperty(property) ? 0 : 1)
            .ThenBy(property => property.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string PropertyKey(AccessibilityProperty property) =>
        $"{property.Group}|{property.Name}";

    private static IEnumerable<AccessibilityProperty> BuildRelationshipProperties(
        IEnumerable<AccessibilityRelationship> relationships)
    {
        return relationships
            .GroupBy(relationship => new { relationship.Type, relationship.Source })
            .OrderBy(group => group.Key.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.Source, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AccessibilityProperty(
                "Relationships",
                group.Key.Type,
                $"{group.Key.Source}: {string.Join("; ", group.Select(FormatRelationshipTarget))}"));
    }

    private static string FormatRelationshipTarget(AccessibilityRelationship relationship)
    {
        var target = string.IsNullOrWhiteSpace(relationship.TargetName)
            ? relationship.TargetControlType
            : string.IsNullOrWhiteSpace(relationship.TargetControlType)
                ? relationship.TargetName
                : $"{relationship.TargetControlType}: {relationship.TargetName}";

        if (!string.IsNullOrWhiteSpace(relationship.TargetId))
        {
            target += $" [ID {relationship.TargetId}]";
        }

        if (relationship.TargetWidth > 0 && relationship.TargetHeight > 0)
        {
            target += $" — {relationship.TargetX:0},{relationship.TargetY:0} " +
                      $"{relationship.TargetWidth:0}x{relationship.TargetHeight:0}";
        }

        return target;
    }

    private static bool IsInspectorRelationshipProperty(AccessibilityProperty property) =>
        property.Group.Equals("Relationships", StringComparison.OrdinalIgnoreCase) ||
        property.Group.EndsWith(" Relationships", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<AccessibilityProperty> EnumerateSnapshotProperties(AccessibilityInspectionSnapshot? snapshot)
    {
        if (snapshot is null) yield break;
        foreach (var root in new[] { snapshot.UiaRoot, snapshot.MsaaRoot, snapshot.Ia2Root })
        {
            foreach (var property in EnumerateProperties(root)) yield return property;
        }
    }

    private static IEnumerable<AccessibilityProperty> EnumerateProperties(AccessibilityNode? node)
    {
        if (node is null) yield break;
        foreach (var property in node.Properties.Where(property => !IsInspectorRelationshipProperty(property)))
        {
            yield return property;
        }
        foreach (var property in BuildRelationshipProperties(node.Relationships))
        {
            yield return property;
        }
        foreach (var child in node.Children)
        {
            foreach (var property in EnumerateProperties(child)) yield return property;
        }
    }

    private async void LoadCompleteTree_Click(object sender, RoutedEventArgs e)
    {
        if (_inspectionBusy) return;

        var selected = _selectedNode;
        if (_inspectionMode != InspectionMode.None)
        {
            SetInspectionMode(InspectionMode.None);
        }

        _inspectionBusy = true;
        StatusText.Text = "Loading the complete application or web-page accessibility tree…";
        try
        {
            var result = await Task.Run(() => _inspector.InspectComplete(selected, CompleteTreeMaxDepth));
            if (result.RootFor(ActiveApi) is null && result.UiaRoot is null)
            {
                StatusText.Text = "No complete accessibility tree was available. Inspect an element in the target application first.";
                return;
            }

            ShowResult(result, "Complete app/page tree");
            ExpandRootNode();
            var count = _activeRoot is null ? 0 : CountNodes(_activeRoot);
            StatusText.Text = $"Loaded complete {TreeHeading.Text.ToLowerInvariant()} with {count:N0} nodes.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not load the complete accessibility tree: {exception.Message}";
        }
        finally
        {
            _inspectionBusy = false;
        }
    }

    private void ExpandAll_Click(object sender, RoutedEventArgs e)
    {
        NodeTree.UpdateLayout();
        SetTreeExpansion(NodeTree, true);
        StatusText.Text = "Accessibility tree expanded";
    }

    private void CollapseAll_Click(object sender, RoutedEventArgs e)
    {
        NodeTree.UpdateLayout();
        SetTreeExpansion(NodeTree, false);
        StatusText.Text = "Accessibility tree collapsed";
    }

    private void ExpandRootNode()
    {
        NodeTree.UpdateLayout();
        if (_activeRoot is not null &&
            NodeTree.ItemContainerGenerator.ContainerFromItem(_activeRoot) is TreeViewItem rootItem)
        {
            rootItem.IsExpanded = true;
        }
    }

    private static void SetTreeExpansion(ItemsControl parent, bool expanded)
    {
        for (var index = 0; index < parent.Items.Count; index++)
        {
            if (parent.ItemContainerGenerator.ContainerFromIndex(index) is not TreeViewItem item) continue;
            item.IsExpanded = expanded;
            if (expanded) item.UpdateLayout();
            SetTreeExpansion(item, expanded);
        }
    }

    private static int CountNodes(AccessibilityNode root) =>
        1 + root.Children.Sum(CountNodes);

    private async void UpOneLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode is null || _inspectionBusy)
        {
            StatusText.Text = "Select a tree element first.";
            return;
        }

        _inspectionBusy = true;
        try
        {
            var result = await Task.Run(() => _inspector.InspectParent(_selectedNode, ActiveApi, Depth));
            if (result.RootFor(ActiveApi) is null)
            {
                StatusText.Text = $"No parent is available through {ActiveApi}.";
                return;
            }
            ShowResult(result, "Parent level");
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not move up one level: {exception.Message}";
        }
        finally
        {
            _inspectionBusy = false;
        }
    }

    private void CopyHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode is null) { StatusText.Text = "Select a tree element first."; return; }
        Clipboard.SetText(HtmlExportService.SerializeElement(_selectedNode));
        StatusText.Text = "Element HTML copied";
    }

    private void CopyHtmlSubtree_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNode is null) { StatusText.Text = "Select a tree element first."; return; }
        Clipboard.SetText(HtmlExportService.SerializeSubtree(_selectedNode));
        StatusText.Text = "HTML subtree copied";
    }

    private void CopyJson_Click(object sender, RoutedEventArgs e)
    {
        if (_activeRoot is null) return;
        Clipboard.SetText(JsonExportService.Serialize(_activeRoot));
        StatusText.Text = $"{ActiveApi} accessibility tree copied as JSON";
    }

    private void SaveJson_Click(object sender, RoutedEventArgs e)
    {
        if (_activeRoot is null) return;
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"{ActiveApi.ToLowerInvariant()}-accessibility-tree.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, JsonExportService.Serialize(_activeRoot));
        StatusText.Text = $"Saved {dialog.FileName}";
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void PopulateHelpMenu()
    {
        HelpMenu.Items.Clear();
        var entries = HelpMenuLinkService.Load();
        if (entries.Count == 0)
        {
            HelpMenu.Items.Add(new MenuItem { Header = "No help links configured", IsEnabled = false });
            return;
        }

        foreach (var entry in entries)
        {
            if (entry.IsSeparator) { HelpMenu.Items.Add(new Separator()); continue; }
            var item = new MenuItem { Header = entry.Label ?? entry.Url, Tag = entry.Url, ToolTip = entry.Url };
            item.Click += HelpLink_Click;
            HelpMenu.Items.Add(item);
        }
    }

    private void HelpLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string url } || !HelpMenuLinkService.IsAllowedUrl(url))
        {
            MessageBox.Show(this, "The configured help link is not a valid HTTP or HTTPS address.", "Help link", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not open help link", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        _keyboardHook.Dispose();
        SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        _inspector.Dispose();
        base.OnClosed(e);
    }
}
