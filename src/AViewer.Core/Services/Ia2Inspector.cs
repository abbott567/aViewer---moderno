using System.Runtime.InteropServices;
using System.Text;
using AViewer.Core.Models;

namespace AViewer.Core.Services;

internal sealed class Ia2Inspector
{
    private static readonly object ChildSelf = 0;
    private static readonly string[] RelationshipTypes =
    [
        "labelledBy", "labelFor", "describedBy", "descriptionFor",
        "controllerFor", "controlledBy", "flowsTo", "flowsFrom",
        "details", "errorMessage", "memberOf", "popupFor",
        "rowHeader", "columnHeader"
    ];

    public (AccessibilityNode? MsaaRoot, AccessibilityNode? Ia2Root) InspectTreesPoint(
        int x,
        int y,
        int maxDepth)
    {
        if (!TryAccessibleAtPoint(x, y, out var accessible, out var child))
        {
            return (null, null);
        }

        return (
            MapAccessibleTree(accessible, child, 0, NormalizeDepth(maxDepth), false),
            MapAccessibleTree(accessible, child, 0, NormalizeDepth(maxDepth), true));
    }

    public (AccessibilityNode? MsaaRoot, AccessibilityNode? Ia2Root) InspectParentTreesPoint(
        int x,
        int y,
        int maxDepth)
    {
        if (!TryAccessibleAtPoint(x, y, out var accessible, out _))
        {
            return (null, null);
        }

        Accessibility.IAccessible? parent;
        try { parent = accessible.accParent as Accessibility.IAccessible; }
        catch { parent = null; }
        if (parent is null)
        {
            return (null, null);
        }

        return (
            MapAccessibleTree(parent, ChildSelf, 0, NormalizeDepth(maxDepth), false),
            MapAccessibleTree(parent, ChildSelf, 0, NormalizeDepth(maxDepth), true));
    }

    public (AccessibilityNode? MsaaRoot, AccessibilityNode? Ia2Root) InspectCompleteTreesPoint(
        int x,
        int y,
        int maxDepth)
    {
        if (!TryAccessibleAtPoint(x, y, out var accessible, out _))
        {
            return (null, null);
        }

        var root = FindCompleteRoot(accessible);
        return (
            MapAccessibleTree(root, ChildSelf, 0, NormalizeDepth(maxDepth), false),
            MapAccessibleTree(root, ChildSelf, 0, NormalizeDepth(maxDepth), true));
    }

    private static int NormalizeDepth(int maxDepth) => maxDepth < 0 ? int.MaxValue : maxDepth;

    private static Accessibility.IAccessible FindCompleteRoot(Accessibility.IAccessible accessible)
    {
        var current = accessible;
        var applicationRoot = accessible;
        Accessibility.IAccessible? documentRoot = null;

        for (var level = 0; level < 128; level++)
        {
            var role = FormatRole(ReadObject(() => current.get_accRole(ChildSelf)));
            if (role.Contains("document", StringComparison.OrdinalIgnoreCase))
            {
                documentRoot = current;
            }

            if (role.Contains("window", StringComparison.OrdinalIgnoreCase) ||
                role.Contains("application", StringComparison.OrdinalIgnoreCase))
            {
                applicationRoot = current;
                break;
            }

            Accessibility.IAccessible? parent;
            try { parent = current.accParent as Accessibility.IAccessible; }
            catch { parent = null; }
            if (parent is null) break;

            applicationRoot = parent;
            current = parent;
        }

        return documentRoot ?? applicationRoot;
    }

    private static bool TryAccessibleAtPoint(
        int x,
        int y,
        out Accessibility.IAccessible accessible,
        out object child)
    {
        accessible = null!;
        child = ChildSelf;
        try
        {
            var hr = Ia2Interop.AccessibleObjectFromPoint(
                new Ia2Interop.NativePoint(x, y),
                out var raw,
                out var childId);
            if (hr < 0 || raw is not Accessibility.IAccessible root)
            {
                return false;
            }

            child = childId;
            var resolved = ResolveChild(root, childId);
            if (resolved is Accessibility.IAccessible childAccessible)
            {
                accessible = childAccessible;
                child = ChildSelf;
            }
            else
            {
                accessible = root;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AccessibilityNode MapAccessibleTree(
        Accessibility.IAccessible accessible,
        object child,
        int depth,
        int maxDepth,
        bool includeIa2)
    {
        GetLocation(accessible, child, out var left, out var top, out var width, out var height);
        var name = Read(() => accessible.get_accName(child));
        var roleObject = ReadObject(() => accessible.get_accRole(child));
        var role = FormatRole(roleObject);
        var state = FormatState(ReadObject(() => accessible.get_accState(child)));
        var properties = new List<AccessibilityProperty>
        {
            new(includeIa2 ? "IA2" : "MSAA", "Name", name),
            new(includeIa2 ? "IA2" : "MSAA", "Role", role),
            new(includeIa2 ? "IA2" : "MSAA", "State", state),
            new("MSAA", "Value", Read(() => accessible.get_accValue(child))),
            new("MSAA", "Description", Read(() => accessible.get_accDescription(child))),
            new("MSAA", "Keyboard shortcut", Read(() => accessible.get_accKeyboardShortcut(child))),
            new("MSAA", "Default action", Read(() => accessible.get_accDefaultAction(child)))
        };

        var relationships = new List<AccessibilityRelationship>();
        var nodeId = $"{(includeIa2 ? "ia2" : "msaa")}-{name}|{role}|{left},{top},{width},{height}";

        if (includeIa2 && child is int childNumber && childNumber == 0)
        {
            IntPtr ia2 = IntPtr.Zero;
            try
            {
                if (TryAcquireIa2(accessible, out ia2, out var acquisition))
                {
                    properties.Add(new("IA2", "Available", "True"));
                    properties.Add(new("IA2", "Acquisition", acquisition));
                    AddIa2Properties(ia2, properties, ref role, ref nodeId);
                    relationships.AddRange(ReadAllRelationships(ia2, properties));
                }
                else
                {
                    properties.Add(new("IA2", "Available", "False"));
                    properties.Add(new("IA2", "Acquisition", acquisition));
                }
            }
            finally
            {
                if (ia2 != IntPtr.Zero) Marshal.Release(ia2);
            }
        }

        var node = new AccessibilityNode
        {
            Api = includeIa2 ? "IA2" : "MSAA",
            Id = nodeId,
            Name = name,
            ControlType = role,
            BoundingRectangle = $"{left},{top} {width}x{height}",
            BoundingX = left,
            BoundingY = top,
            BoundingWidth = width,
            BoundingHeight = height,
            IsEnabled = !state.Contains("unavailable", StringComparison.OrdinalIgnoreCase),
            IsKeyboardFocusable = state.Contains("focusable", StringComparison.OrdinalIgnoreCase),
            HasKeyboardFocus = state.Contains("focused", StringComparison.OrdinalIgnoreCase),
            Properties = properties,
            Relationships = relationships
        };

        if (depth >= maxDepth || child is not int self || self != 0)
        {
            return node;
        }

        var count = ReadInt(() => accessible.accChildCount);
        for (var index = 1; index <= count; index++)
        {
            try
            {
                var childObject = accessible.get_accChild(index);
                node.Children.Add(childObject is Accessibility.IAccessible childAccessible
                    ? MapAccessibleTree(childAccessible, ChildSelf, depth + 1, maxDepth, includeIa2)
                    : MapAccessibleTree(accessible, index, depth + 1, maxDepth, includeIa2));
            }
            catch
            {
                // Sparse and changing collections are common in browsers.
            }
        }

        return node;
    }

    private static void AddIa2Properties(
        IntPtr ia2,
        ICollection<AccessibilityProperty> properties,
        ref string role,
        ref string nodeId)
    {
        try
        {
            var roleCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(ia2, Ia2Interop.Ia2RoleSlot);
            if (roleCall(ia2, out var value) >= 0)
            {
                role = FormatIa2Role(value);
                properties.Add(new("IA2", "Role", role));
            }
        }
        catch { }

        try
        {
            var idCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(ia2, Ia2Interop.Ia2UniqueIdSlot);
            if (idCall(ia2, out var value) >= 0)
            {
                nodeId = value.ToString();
                properties.Add(new("IA2", "Unique ID", nodeId));
            }
        }
        catch { }

        AddBstrProperty(ia2, properties, "Attributes", Ia2Interop.Ia2AttributesSlot);
        AddBstrProperty(ia2, properties, "Extended role", Ia2Interop.Ia2ExtendedRoleSlot);
        AddBstrProperty(ia2, properties, "Localized extended role", Ia2Interop.Ia2LocalizedExtendedRoleSlot);

        try
        {
            var stateCall = Ia2Interop.GetVtableDelegate<Ia2Interop.UIntOutDelegate>(ia2, Ia2Interop.Ia2StatesSlot);
            if (stateCall(ia2, out var states) >= 0)
            {
                properties.Add(new("IA2", "States", $"0x{states:X8}"));
            }
        }
        catch { }
    }

    private static void AddBstrProperty(
        IntPtr pointer,
        ICollection<AccessibilityProperty> properties,
        string name,
        int slot)
    {
        IntPtr bstr = IntPtr.Zero;
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntPtrOutDelegate>(pointer, slot);
            if (call(pointer, out bstr) >= 0 && bstr != IntPtr.Zero)
            {
                properties.Add(new("IA2", name, Marshal.PtrToStringBSTR(bstr) ?? string.Empty));
            }
        }
        catch { }
        finally
        {
            if (bstr != IntPtr.Zero) Marshal.FreeBSTR(bstr);
        }
    }

    private static List<AccessibilityRelationship> ReadAllRelationships(
        IntPtr ia2,
        ICollection<AccessibilityProperty> properties)
    {
        var result = ReadRelationsInterface(ia2);
        foreach (var relationship in ReadDirectRelationshipTargets(ia2))
        {
            if (!result.Any(existing => SameRelationship(existing, relationship)))
            {
                result.Add(relationship);
            }
        }

        foreach (var relationship in ReadTableCellRelationships(ia2, properties))
        {
            if (!result.Any(existing =>
                    string.Equals(existing.Type, relationship.Type, StringComparison.OrdinalIgnoreCase) &&
                    SameTarget(existing, relationship)))
            {
                result.Add(relationship);
            }
        }

        foreach (var group in result.GroupBy(item => item.Type))
        {
            properties.Add(new(
                "IA2 Relationships",
                group.Key,
                string.Join("; ", group.Select(item =>
                    string.IsNullOrWhiteSpace(item.TargetName)
                        ? item.TargetControlType
                        : $"{item.TargetName} ({item.TargetControlType})"))));
        }
        return result;
    }

    private static List<AccessibilityRelationship> ReadTableCellRelationships(
        IntPtr ia2,
        ICollection<AccessibilityProperty> properties)
    {
        var result = new List<AccessibilityRelationship>();
        IntPtr tableCell = IntPtr.Zero;
        try
        {
            if (!TryAcquireRelatedIa2Interface(
                    ia2,
                    Ia2Interop.IidIAccessibleTableCell,
                    out tableCell))
            {
                return result;
            }

            properties.Add(new("IA2 Table cell", "Available", "True"));
            AddTableCellIntProperty(
                tableCell,
                properties,
                "Column index",
                Ia2Interop.TableCellColumnIndexSlot);
            AddTableCellIntProperty(
                tableCell,
                properties,
                "Row index",
                Ia2Interop.TableCellRowIndexSlot);
            AddTableCellIntProperty(
                tableCell,
                properties,
                "Column span",
                Ia2Interop.TableCellColumnExtentSlot);
            AddTableCellIntProperty(
                tableCell,
                properties,
                "Row span",
                Ia2Interop.TableCellRowExtentSlot);

            ReadTableCellHeaderTargets(
                tableCell,
                Ia2Interop.TableCellColumnHeaderCellsSlot,
                "columnHeader",
                "IA2 IAccessibleTableCell.columnHeaderCells",
                result);
            ReadTableCellHeaderTargets(
                tableCell,
                Ia2Interop.TableCellRowHeaderCellsSlot,
                "rowHeader",
                "IA2 IAccessibleTableCell.rowHeaderCells",
                result);
        }
        catch
        {
            // Table interfaces are optional and can disappear as browser content changes.
        }
        finally
        {
            if (tableCell != IntPtr.Zero) Marshal.Release(tableCell);
        }

        return result;
    }

    private static bool TryAcquireRelatedIa2Interface(
        IntPtr source,
        Guid requestedInterface,
        out IntPtr result)
    {
        result = IntPtr.Zero;
        var iid = requestedInterface;
        if (Marshal.QueryInterface(source, ref iid, out result) >= 0 && result != IntPtr.Zero)
        {
            return true;
        }

        if (result != IntPtr.Zero)
        {
            Marshal.Release(result);
            result = IntPtr.Zero;
        }

        IntPtr serviceProvider = IntPtr.Zero;
        try
        {
            var serviceProviderIid = Ia2Interop.IidIServiceProvider;
            if (Marshal.QueryInterface(source, ref serviceProviderIid, out serviceProvider) < 0 ||
                serviceProvider == IntPtr.Zero)
            {
                return false;
            }

            var queryService = Ia2Interop.GetVtableDelegate<Ia2Interop.QueryServiceDelegate>(serviceProvider, 3);
            var service = Ia2Interop.IidIAccessible;
            iid = requestedInterface;
            var queryResult = queryService(serviceProvider, ref service, ref iid, out result);
            if (queryResult >= 0 && result != IntPtr.Zero)
            {
                return true;
            }

            if (result != IntPtr.Zero)
            {
                Marshal.Release(result);
                result = IntPtr.Zero;
            }
            return false;
        }
        catch
        {
            if (result != IntPtr.Zero)
            {
                Marshal.Release(result);
                result = IntPtr.Zero;
            }
            return false;
        }
        finally
        {
            if (serviceProvider != IntPtr.Zero) Marshal.Release(serviceProvider);
        }
    }

    private static void AddTableCellIntProperty(
        IntPtr tableCell,
        ICollection<AccessibilityProperty> properties,
        string name,
        int slot)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(tableCell, slot);
            if (call(tableCell, out var value) >= 0)
            {
                properties.Add(new("IA2 Table cell", name, value.ToString()));
            }
        }
        catch { }
    }

    private static void ReadTableCellHeaderTargets(
        IntPtr tableCell,
        int slot,
        string relationshipType,
        string source,
        ICollection<AccessibilityRelationship> result)
    {
        IntPtr targets = IntPtr.Zero;
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.PointerArrayOutDelegate>(tableCell, slot);
            if (call(tableCell, out targets, out var count) < 0 || targets == IntPtr.Zero || count <= 0)
            {
                return;
            }

            for (var index = 0; index < count; index++)
            {
                var target = Marshal.ReadIntPtr(targets, index * IntPtr.Size);
                if (target == IntPtr.Zero) continue;
                try
                {
                    var mapped = MapTarget(relationshipType, target, source);
                    if (mapped is not null && !result.Any(existing =>
                            string.Equals(existing.Type, mapped.Type, StringComparison.OrdinalIgnoreCase) &&
                            SameTarget(existing, mapped)))
                    {
                        result.Add(mapped);
                    }
                }
                finally
                {
                    Marshal.Release(target);
                }
            }
        }
        finally
        {
            if (targets != IntPtr.Zero) Marshal.FreeCoTaskMem(targets);
        }
    }

    private static List<AccessibilityRelationship> ReadRelationsInterface(IntPtr ia2)
    {
        var result = new List<AccessibilityRelationship>();
        try
        {
            var countCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(ia2, Ia2Interop.Ia2NRelationsSlot);
            if (countCall(ia2, out var count) < 0 || count <= 0) return result;
            var relationCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IndexedPointerOutDelegate>(ia2, Ia2Interop.Ia2RelationSlot);

            for (var relationIndex = 0; relationIndex < count; relationIndex++)
            {
                IntPtr relation = IntPtr.Zero;
                try
                {
                    if (relationCall(ia2, relationIndex, out relation) < 0 || relation == IntPtr.Zero) continue;
                    var type = ReadBstrMethod(relation, 3);
                    if (string.IsNullOrWhiteSpace(type) || IsContainingRelationship(type)) continue;
                    var targetCountCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(relation, 5);
                    if (targetCountCall(relation, out var targetCount) < 0 || targetCount <= 0) continue;
                    var targetCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IndexedPointerOutDelegate>(relation, 6);
                    for (var targetIndex = 0; targetIndex < targetCount; targetIndex++)
                    {
                        IntPtr target = IntPtr.Zero;
                        try
                        {
                            if (targetCall(relation, targetIndex, out target) < 0 || target == IntPtr.Zero) continue;
                            var mapped = MapTarget(type, target, $"IA2 relation {type}");
                    if (mapped is not null && !result.Any(existing => SameRelationship(existing, mapped))) result.Add(mapped);
                        }
                        finally
                        {
                            if (target != IntPtr.Zero) Marshal.Release(target);
                        }
                    }
                }
                catch { }
                finally
                {
                    if (relation != IntPtr.Zero) Marshal.Release(relation);
                }
            }
        }
        catch { }
        return result;
    }

    private static IEnumerable<AccessibilityRelationship> ReadDirectRelationshipTargets(IntPtr ia2)
    {
        IntPtr ia2_2 = IntPtr.Zero;
        var iid = Ia2Interop.IidIAccessible2_2;
        try
        {
            if (Marshal.QueryInterface(ia2, ref iid, out ia2_2) < 0 || ia2_2 == IntPtr.Zero) yield break;
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.RelationTargetsOfTypeDelegate>(
                ia2_2,
                Ia2Interop.Ia2_2RelationTargetsOfTypeSlot);

            foreach (var type in RelationshipTypes)
            {
                IntPtr targets = IntPtr.Zero;
                try
                {
                    if (call(ia2_2, type, 0, out targets, out var count) < 0 || targets == IntPtr.Zero || count <= 0)
                    {
                        continue;
                    }
                    for (var index = 0; index < count; index++)
                    {
                        var target = Marshal.ReadIntPtr(targets, index * IntPtr.Size);
                        if (target == IntPtr.Zero) continue;
                        try
                        {
                            var mapped = MapTarget(type, target, $"IA2_2 relationTargetsOfType({type})");
                            if (mapped is not null) yield return mapped;
                        }
                        finally
                        {
                            Marshal.Release(target);
                        }
                    }
                }
                finally
                {
                    if (targets != IntPtr.Zero) Marshal.FreeCoTaskMem(targets);
                }
            }
        }
        finally
        {
            if (ia2_2 != IntPtr.Zero) Marshal.Release(ia2_2);
        }
    }

    private static AccessibilityRelationship? MapTarget(string type, IntPtr unknown, string source)
    {
        IntPtr accessiblePointer = IntPtr.Zero;
        try
        {
            var iid = Ia2Interop.IidIAccessible;
            if (Marshal.QueryInterface(unknown, ref iid, out accessiblePointer) < 0 || accessiblePointer == IntPtr.Zero)
            {
                return null;
            }
            var raw = Marshal.GetObjectForIUnknown(accessiblePointer);
            if (raw is not Accessibility.IAccessible accessible) return null;
            var name = Read(() => accessible.get_accName(ChildSelf));
            var role = FormatRole(ReadObject(() => accessible.get_accRole(ChildSelf)));
            GetLocation(accessible, ChildSelf, out var x, out var y, out var width, out var height);
            var id = TryGetUniqueId(unknown, out var uniqueId)
                ? uniqueId.ToString()
                : $"{name}|{role}|{x},{y},{width},{height}";
            return new AccessibilityRelationship(
                FriendlyRelationshipName(type), source, id, name, role,
                x, y, width, height);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (accessiblePointer != IntPtr.Zero) Marshal.Release(accessiblePointer);
        }
    }

    private static bool TryGetUniqueId(IntPtr unknown, out int id)
    {
        id = 0;
        IntPtr ia2 = IntPtr.Zero;
        var iid = Ia2Interop.IidIAccessible2;
        try
        {
            if (Marshal.QueryInterface(unknown, ref iid, out ia2) < 0 || ia2 == IntPtr.Zero) return false;
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(ia2, Ia2Interop.Ia2UniqueIdSlot);
            return call(ia2, out id) >= 0;
        }
        catch { return false; }
        finally { if (ia2 != IntPtr.Zero) Marshal.Release(ia2); }
    }

    private static bool TryAcquireIa2(object candidate, out IntPtr ia2, out string detail)
    {
        ia2 = IntPtr.Zero;
        detail = "IAccessible2 not exposed";
        IntPtr unknown = IntPtr.Zero;
        IntPtr serviceProvider = IntPtr.Zero;
        try
        {
            unknown = Marshal.GetIUnknownForObject(candidate);
            var iid = Ia2Interop.IidIAccessible2;
            if (Marshal.QueryInterface(unknown, ref iid, out ia2) >= 0 && ia2 != IntPtr.Zero)
            {
                detail = "Direct QueryInterface";
                return true;
            }

            var serviceProviderIid = Ia2Interop.IidIServiceProvider;
            if (Marshal.QueryInterface(unknown, ref serviceProviderIid, out serviceProvider) < 0 || serviceProvider == IntPtr.Zero)
            {
                return false;
            }
            var queryService = Ia2Interop.GetVtableDelegate<Ia2Interop.QueryServiceDelegate>(serviceProvider, 3);
            var service = Ia2Interop.IidIAccessible;
            iid = Ia2Interop.IidIAccessible2;
            if (queryService(serviceProvider, ref service, ref iid, out ia2) >= 0 && ia2 != IntPtr.Zero)
            {
                detail = "IServiceProvider.QueryService";
                return true;
            }
            return false;
        }
        catch (Exception exception)
        {
            detail = exception.Message;
            if (ia2 != IntPtr.Zero) { Marshal.Release(ia2); ia2 = IntPtr.Zero; }
            return false;
        }
        finally
        {
            if (serviceProvider != IntPtr.Zero) Marshal.Release(serviceProvider);
            if (unknown != IntPtr.Zero) Marshal.Release(unknown);
        }
    }

    private static string ReadBstrMethod(IntPtr pointer, int slot)
    {
        IntPtr value = IntPtr.Zero;
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntPtrOutDelegate>(pointer, slot);
            return call(pointer, out value) >= 0 && value != IntPtr.Zero
                ? Marshal.PtrToStringBSTR(value) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (value != IntPtr.Zero) Marshal.FreeBSTR(value);
        }
    }

    private static bool SameTarget(AccessibilityRelationship first, AccessibilityRelationship second)
    {
        if (!string.IsNullOrWhiteSpace(first.TargetId) &&
            string.Equals(first.TargetId, second.TargetId, StringComparison.Ordinal)) return true;
        const double tolerance = 2;
        return Math.Abs(first.TargetX - second.TargetX) <= tolerance &&
               Math.Abs(first.TargetY - second.TargetY) <= tolerance &&
               Math.Abs(first.TargetWidth - second.TargetWidth) <= tolerance &&
               Math.Abs(first.TargetHeight - second.TargetHeight) <= tolerance;
    }

    private static bool SameRelationship(
        AccessibilityRelationship first,
        AccessibilityRelationship second) =>
        string.Equals(first.Type, second.Type, StringComparison.OrdinalIgnoreCase) &&
        SameTarget(first, second);

    private static bool IsContainingRelationship(string type) =>
        type.StartsWith("containing", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "nodeChildOf", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "subwindowOf", StringComparison.OrdinalIgnoreCase);

    private static string FriendlyRelationshipName(string type) => type switch
    {
        "labelledBy" or "labeledBy" => "Labeled by",
        "labelFor" => "Label for",
        "describedBy" => "Described by",
        "descriptionFor" => "Description for",
        "controllerFor" => "Controller for",
        "controlledBy" => "Controlled by",
        "flowsTo" => "Flows to",
        "flowsFrom" => "Flows from",
        "errorMessage" => "Error message",
        "rowHeader" => "Row header",
        "columnHeader" => "Column header",
        _ => type
    };

    private static object? ResolveChild(Accessibility.IAccessible accessible, object? childId)
    {
        if (childId is not int id || id == 0) return accessible;
        try { return accessible.get_accChild(id); }
        catch { return accessible; }
    }

    private static void GetLocation(
        Accessibility.IAccessible accessible,
        object child,
        out int x,
        out int y,
        out int width,
        out int height)
    {
        x = y = width = height = 0;
        try { accessible.accLocation(out x, out y, out width, out height, child); }
        catch { }
    }

    private static string Read(Func<string> value)
    {
        try { return value() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static object? ReadObject(Func<object> value)
    {
        try { return value(); }
        catch { return null; }
    }

    private static int ReadInt(Func<int> value)
    {
        try { return value(); }
        catch { return 0; }
    }

    private static string FormatRole(object? value)
    {
        if (!TryUnsigned(value, out var role)) return value?.ToString() ?? string.Empty;
        var length = Ia2Interop.GetRoleText(role, null, 0);
        if (length == 0) return $"MSAA role {role}";
        var text = new StringBuilder((int)length + 1);
        return Ia2Interop.GetRoleText(role, text, length + 1) == 0 ? $"MSAA role {role}" : text.ToString();
    }

    private static string FormatState(object? value)
    {
        if (!TryUnsigned(value, out var states) || states == 0) return "normal";
        var names = new List<string>();
        for (var bit = 0; bit < 32; bit++)
        {
            var flag = 1u << bit;
            if ((states & flag) == 0) continue;
            var length = Ia2Interop.GetStateText(flag, null, 0);
            if (length == 0) { names.Add($"0x{flag:X8}"); continue; }
            var text = new StringBuilder((int)length + 1);
            names.Add(Ia2Interop.GetStateText(flag, text, length + 1) == 0 ? $"0x{flag:X8}" : text.ToString());
        }
        return string.Join(", ", names);
    }

    private static string FormatIa2Role(int role) => role switch
    {
        0x401 => "canvas", 0x402 => "caption", 0x410 => "form",
        0x414 => "heading", 0x419 => "label", 0x41D => "page",
        0x41E => "paragraph", 0x424 => "section", 0x42A => "toggle button",
        0x42C => "complementary content", 0x42D => "landmark",
        _ when role < 0x401 => FormatRole(role),
        _ => $"IA2 role {role}"
    };

    private static bool TryUnsigned(object? value, out uint result)
    {
        switch (value)
        {
            case int number: result = unchecked((uint)number); return true;
            case uint number: result = number; return true;
            case short number: result = unchecked((uint)number); return true;
            case ushort number: result = number; return true;
            default: result = 0; return false;
        }
    }
}
