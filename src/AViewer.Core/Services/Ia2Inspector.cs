using System.Runtime.InteropServices;
using System.Text;
using AViewer.Core.Models;

namespace AViewer.Core.Services;

internal sealed class Ia2Inspector
{
    private static readonly object ChildSelf = 0;

    private static readonly string[] Ia2RelationshipTypes =
    [
        "labelledBy",
        "labelFor",
        "describedBy",
        "descriptionFor",
        "controllerFor",
        "controlledBy",
        "flowsTo",
        "flowsFrom",
        "details",
        "errorMessage",
        "memberOf",
        "popupFor",
        "rowHeader",
        "columnHeader"
    ];

    public Ia2InspectionResult InspectPoint(int x, int y)
    {
        try
        {
            var hr = Ia2Interop.AccessibleObjectFromPoint(
                new Ia2Interop.NativePoint(x, y),
                out var rawAccessible,
                out var childId);

            if (hr < 0 || rawAccessible is not Accessibility.IAccessible accessible)
            {
                return Result([
                    new("IA2", "Available", "False"),
                    new("IA2", "Acquisition", $"AccessibleObjectFromPoint failed: 0x{hr:X8}")
                ]);
            }

            var target = ResolveChild(accessible, childId) ?? accessible;
            var msaa = target as Accessibility.IAccessible ?? accessible;
            var msaaRole = ReadObject(() => msaa.get_accRole(ChildSelf));
            var properties = ReadMsaa(msaa, ChildSelf);

            if (!TryAcquireIa2(target, out var ia2Pointer, out var acquisition)
                && !ReferenceEquals(target, rawAccessible))
            {
                TryAcquireIa2(rawAccessible, out ia2Pointer, out acquisition);
            }

            if (ia2Pointer == IntPtr.Zero)
            {
                properties.Add(new("IA2", "Available", "False"));
                properties.Add(new("IA2", "Acquisition", acquisition));
                return Result(properties);
            }

            try
            {
                properties.Add(new("IA2", "Acquisition", acquisition));
                properties.AddRange(ReadIa2(ia2Pointer, msaaRole, Read(() => msaa.get_accName(ChildSelf))));
                var relationships = ReadAllIa2Relationships(ia2Pointer, properties);
                ReadIa2TableProperties(ia2Pointer, properties, relationships);
                return new Ia2InspectionResult(properties, relationships);
            }
            finally
            {
                Marshal.Release(ia2Pointer);
            }
        }
        catch (COMException ex)
        {
            return Result([
                new("IA2", "Available", "False"),
                new("IA2", "Error", $"0x{ex.HResult:X8}: {ex.Message}")
            ]);
        }
        catch (Exception ex)
        {
            return Result([
                new("IA2", "Available", "False"),
                new("IA2", "Error", ex.Message)
            ]);
        }
    }


    public (AccessibilityNode? MsaaRoot, AccessibilityNode? Ia2Root) InspectParentTreesPoint(
        int x,
        int y,
        int maxDepth)
    {
        try
        {
            var hr = Ia2Interop.AccessibleObjectFromPoint(
                new Ia2Interop.NativePoint(x, y),
                out var rawAccessible,
                out var childId);

            if (hr < 0 || rawAccessible is not Accessibility.IAccessible accessible)
            {
                return (null, null);
            }

            var target = ResolveChild(accessible, childId);
            if (target is Accessibility.IAccessible childAccessible)
            {
                accessible = childAccessible;
            }

            Accessibility.IAccessible? parent;
            try
            {
                parent = accessible.accParent as Accessibility.IAccessible;
            }
            catch
            {
                parent = null;
            }
            if (parent is null)
            {
                return (null, null);
            }

            var depth = Math.Max(0, maxDepth);
            return (
                MapAccessibleTree(parent, ChildSelf, 0, depth, includeIa2: false),
                MapAccessibleTree(parent, ChildSelf, 0, depth, includeIa2: true));
        }
        catch
        {
            return (null, null);
        }
    }

    public (AccessibilityNode? MsaaRoot, AccessibilityNode? Ia2Root) InspectTreesPoint(
        int x,
        int y,
        int maxDepth)
    {
        try
        {
            var hr = Ia2Interop.AccessibleObjectFromPoint(
                new Ia2Interop.NativePoint(x, y),
                out var rawAccessible,
                out var childId);

            if (hr < 0 || rawAccessible is not Accessibility.IAccessible accessible)
            {
                return (null, null);
            }

            var target = ResolveChild(accessible, childId);
            if (target is Accessibility.IAccessible childAccessible)
            {
                accessible = childAccessible;
                childId = ChildSelf;
            }

            var depth = Math.Max(0, maxDepth);
            return (
                MapAccessibleTree(accessible, childId, 0, depth, includeIa2: false),
                MapAccessibleTree(accessible, childId, 0, depth, includeIa2: true));
        }
        catch
        {
            return (null, null);
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
        var msaaRoleObject = ReadObject(() => accessible.get_accRole(child));
        var role = FormatMsaaRole(msaaRoleObject);
        var properties = includeIa2
            ? ReadIa2TreeProperties(accessible, msaaRoleObject, name)
            : ReadMsaa(accessible, child);

        var node = new AccessibilityNode
        {
            Api = includeIa2 ? "IA2" : "MSAA",
            Id = $"{(includeIa2 ? "ia2" : "msaa")}-{Guid.NewGuid():N}",
            Name = name,
            ControlType = includeIa2
                ? GetPropertyValue(properties, "IA2", "Role", role)
                : role,
            BoundingRectangle = $"{left},{top} {width}x{height}",
            BoundingX = left,
            BoundingY = top,
            BoundingWidth = width,
            BoundingHeight = height,
            HasKeyboardFocus = HasFocusedState(ReadObject(() => accessible.get_accState(child))),
            Properties = properties
        };

        if (includeIa2)
        {
            IntPtr ia2Pointer = IntPtr.Zero;
            try
            {
                if (TryAcquireIa2(accessible, out ia2Pointer, out _)
                    && ia2Pointer != IntPtr.Zero)
                {
                    node.Relationships.AddRange(ReadAllIa2Relationships(ia2Pointer, node.Properties));
                    ReadIa2TableProperties(ia2Pointer, node.Properties, node.Relationships);
                }
            }
            finally
            {
                if (ia2Pointer != IntPtr.Zero)
                {
                    Marshal.Release(ia2Pointer);
                }
            }
        }

        if (depth >= maxDepth || child is not int childNumber || childNumber != 0)
        {
            return node;
        }

        var childCount = ReadInt(() => accessible.accChildCount);
        for (var index = 1; index <= childCount; index++)
        {
            try
            {
                var childObject = accessible.get_accChild(index);
                if (childObject is Accessibility.IAccessible childAccessible)
                {
                    node.Children.Add(MapAccessibleTree(
                        childAccessible,
                        ChildSelf,
                        depth + 1,
                        maxDepth,
                        includeIa2));
                }
                else
                {
                    node.Children.Add(MapAccessibleTree(
                        accessible,
                        index,
                        depth + 1,
                        maxDepth,
                        includeIa2));
                }
            }
            catch
            {
                // Providers may expose a sparse or changing child collection.
            }
        }

        return node;
    }

    private static List<AccessibilityProperty> ReadIa2TreeProperties(
        Accessibility.IAccessible accessible,
        object? msaaRole,
        string accessibleName)
    {
        IntPtr ia2Pointer = IntPtr.Zero;
        try
        {
            if (!TryAcquireIa2(accessible, out ia2Pointer, out var acquisition)
                || ia2Pointer == IntPtr.Zero)
            {
                return
                [
                    new("IA2", "Available", "False"),
                    new("IA2", "Acquisition", acquisition)
                ];
            }

            var properties = new List<AccessibilityProperty>
            {
                new("IA2", "Acquisition", acquisition)
            };
            properties.AddRange(ReadIa2(ia2Pointer, msaaRole, accessibleName));
            return properties;
        }
        catch (Exception ex)
        {
            return
            [
                new("IA2", "Available", "False"),
                new("IA2", "Error", ex.Message)
            ];
        }
        finally
        {
            if (ia2Pointer != IntPtr.Zero)
            {
                Marshal.Release(ia2Pointer);
            }
        }
    }

    private static void GetLocation(
        Accessibility.IAccessible accessible,
        object child,
        out int left,
        out int top,
        out int width,
        out int height)
    {
        left = top = width = height = 0;
        try
        {
            accessible.accLocation(out left, out top, out width, out height, child);
        }
        catch
        {
            // Some virtual objects do not expose a screen rectangle.
        }
    }

    private static bool HasFocusedState(object? value)
    {
        if (!TryGetUnsignedValue(value, out var states))
        {
            return false;
        }

        const uint stateSystemFocused = 0x00000004;
        return (states & stateSystemFocused) != 0;
    }

    private static string GetPropertyValue(
        IEnumerable<AccessibilityProperty> properties,
        string group,
        string name,
        string fallback)
    {
        return properties.FirstOrDefault(property =>
                   string.Equals(property.Group, group, StringComparison.Ordinal)
                   && string.Equals(property.Name, name, StringComparison.Ordinal))?.Value
               ?? fallback;
    }

    private static Ia2InspectionResult Result(IReadOnlyList<AccessibilityProperty> properties) =>
        new(properties, Array.Empty<AccessibilityRelationship>());


    private static List<AccessibilityRelationship> ReadAllIa2Relationships(
        IntPtr ia2,
        ICollection<AccessibilityProperty> properties)
    {
        var relationships = ReadIa2Relationships(ia2, properties);
        var directTargets = ReadIa2_2RelationshipTargets(ia2, properties);

        foreach (var relationship in directTargets)
        {
            var duplicate = relationships.Any(existing =>
                string.Equals(existing.Type, relationship.Type, StringComparison.OrdinalIgnoreCase) &&
                SameRelationshipTarget(existing, relationship));
            if (!duplicate)
            {
                relationships.Add(relationship);
            }
        }

        return relationships;
    }

    private static List<AccessibilityRelationship> ReadIa2_2RelationshipTargets(
        IntPtr ia2,
        ICollection<AccessibilityProperty> properties)
    {
        var relationships = new List<AccessibilityRelationship>();
        IntPtr ia2_2 = IntPtr.Zero;
        var iid = Ia2Interop.IidIAccessible2_2;

        try
        {
            var queryHr = Marshal.QueryInterface(ia2, ref iid, out ia2_2);
            if (queryHr < 0 || ia2_2 == IntPtr.Zero)
            {
                return relationships;
            }

            var relationTargetsCall =
                Ia2Interop.GetVtableDelegate<Ia2Interop.RelationTargetsOfTypeDelegate>(
                    ia2_2,
                    Ia2Interop.Ia2_2RelationTargetsOfTypeSlot);

            foreach (var relationType in Ia2RelationshipTypes)
            {
                IntPtr targetArray = IntPtr.Zero;
                var count = 0;
                try
                {
                    var hr = relationTargetsCall(ia2_2, relationType, 0, out targetArray, out count);
                    if (hr < 0 || targetArray == IntPtr.Zero || count <= 0)
                    {
                        continue;
                    }

                    var descriptions = new List<string>();
                    for (var index = 0; index < count; index++)
                    {
                        var target = Marshal.ReadIntPtr(targetArray, index * IntPtr.Size);
                        if (target == IntPtr.Zero)
                        {
                            continue;
                        }

                        try
                        {
                            var mapped = MapIa2Target(
                                relationType,
                                target,
                                $"IA2_2 relationTargetsOfType({relationType})");
                            if (mapped is null)
                            {
                                continue;
                            }

                            if (!relationships.Any(existing =>
                                string.Equals(existing.Type, mapped.Type, StringComparison.OrdinalIgnoreCase) &&
                                SameRelationshipTarget(existing, mapped)))
                            {
                                relationships.Add(mapped);
                                descriptions.Add(FormatRelationshipTarget(mapped));
                            }
                        }
                        finally
                        {
                            Marshal.Release(target);
                        }
                    }

                    if (descriptions.Count > 0)
                    {
                        properties.Add(new(
                            "IA2 Relationships",
                            FriendlyRelationName(relationType),
                            string.Join("; ", descriptions)));
                    }
                }
                catch (Exception ex)
                {
                    properties.Add(new(
                        "IA2 Relationships",
                        FriendlyRelationName(relationType),
                        $"Unavailable ({ex.Message})"));
                }
                finally
                {
                    if (targetArray != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(targetArray);
                    }
                }
            }
        }
        finally
        {
            if (ia2_2 != IntPtr.Zero)
            {
                Marshal.Release(ia2_2);
            }
        }

        return relationships;
    }

    private static bool SameRelationshipTarget(
        AccessibilityRelationship first,
        AccessibilityRelationship second)
    {
        if (!string.IsNullOrWhiteSpace(first.TargetId) &&
            !string.IsNullOrWhiteSpace(second.TargetId) &&
            string.Equals(first.TargetId, second.TargetId, StringComparison.Ordinal))
        {
            return true;
        }

        const double tolerance = 2;
        return Math.Abs(first.TargetX - second.TargetX) <= tolerance &&
               Math.Abs(first.TargetY - second.TargetY) <= tolerance &&
               Math.Abs(first.TargetWidth - second.TargetWidth) <= tolerance &&
               Math.Abs(first.TargetHeight - second.TargetHeight) <= tolerance &&
               string.Equals(first.TargetName, second.TargetName, StringComparison.Ordinal);
    }

    private static List<AccessibilityRelationship> ReadIa2Relationships(
        IntPtr ia2,
        ICollection<AccessibilityProperty> properties)
    {
        var relationships = new List<AccessibilityRelationship>();
        var countCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(
            ia2,
            Ia2Interop.Ia2NRelationsSlot);
        var countHr = countCall(ia2, out var count);
        if (countHr < 0 || count <= 0)
        {
            properties.Add(new("IA2 Relationships", "Count", "0"));
            return relationships;
        }

        properties.Add(new("IA2 Relationships", "Count", count.ToString()));
        var relationCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IndexedPointerOutDelegate>(ia2, 29);

        for (var relationIndex = 0; relationIndex < count; relationIndex++)
        {
            IntPtr relation = IntPtr.Zero;
            try
            {
                var hr = relationCall(ia2, relationIndex, out relation);
                if (hr < 0 || relation == IntPtr.Zero)
                {
                    continue;
                }

                var type = ReadBstrMethod(relation, 3);
                if (string.IsNullOrWhiteSpace(type) || IsContainingRelation(type))
                {
                    continue;
                }

                var targetCountCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(relation, 5);
                var targetCountHr = targetCountCall(relation, out var targetCount);
                if (targetCountHr < 0 || targetCount <= 0)
                {
                    continue;
                }

                var targetCall = Ia2Interop.GetVtableDelegate<Ia2Interop.IndexedPointerOutDelegate>(relation, 6);
                var descriptions = new List<string>();
                for (var targetIndex = 0; targetIndex < targetCount; targetIndex++)
                {
                    IntPtr target = IntPtr.Zero;
                    try
                    {
                        var targetHr = targetCall(relation, targetIndex, out target);
                        if (targetHr < 0 || target == IntPtr.Zero)
                        {
                            continue;
                        }

                        var mapped = MapIa2Target(type, target);
                        if (mapped is null)
                        {
                            continue;
                        }

                        relationships.Add(mapped);
                        descriptions.Add(FormatRelationshipTarget(mapped));
                    }
                    finally
                    {
                        if (target != IntPtr.Zero)
                        {
                            Marshal.Release(target);
                        }
                    }
                }

                if (descriptions.Count > 0)
                {
                    properties.Add(new("IA2 Relationships", FriendlyRelationName(type), string.Join("; ", descriptions)));
                }
            }
            catch (Exception ex)
            {
                properties.Add(new("IA2 Relationships", $"Relation {relationIndex}", $"Unavailable ({ex.Message})"));
            }
            finally
            {
                if (relation != IntPtr.Zero)
                {
                    Marshal.Release(relation);
                }
            }
        }

        return relationships;
    }

    private static void ReadIa2TableProperties(
        IntPtr ia2,
        ICollection<AccessibilityProperty> properties,
        ICollection<AccessibilityRelationship> relationships)
    {
        ReadIa2Table2Properties(ia2, properties);
        ReadIa2TableCellProperties(ia2, properties, relationships);
    }

    private static void ReadIa2Table2Properties(IntPtr ia2, ICollection<AccessibilityProperty> properties)
    {
        IntPtr table = IntPtr.Zero;
        var iid = Ia2Interop.IidIAccessibleTable2;
        try
        {
            var hr = Marshal.QueryInterface(ia2, ref iid, out table);
            if (hr < 0 || table == IntPtr.Zero)
            {
                return;
            }

            properties.Add(new("IA2 Table", "Interface", "IAccessibleTable2"));
            AddInterfaceInt(properties, "IA2 Table", "Column count", table, 6);
            AddInterfaceInt(properties, "IA2 Table", "Row count", table, 7);
            AddInterfaceInt(properties, "IA2 Table", "Selected cell count", table, 8);
            AddInterfaceInt(properties, "IA2 Table", "Selected column count", table, 9);
            AddInterfaceInt(properties, "IA2 Table", "Selected row count", table, 10);
            AddAccessibleDescription(properties, "IA2 Table", "Caption", table, 4);
            AddAccessibleDescription(properties, "IA2 Table", "Summary", table, 15);
        }
        finally
        {
            if (table != IntPtr.Zero)
            {
                Marshal.Release(table);
            }
        }
    }

    private static void ReadIa2TableCellProperties(
        IntPtr ia2,
        ICollection<AccessibilityProperty> properties,
        ICollection<AccessibilityRelationship> relationships)
    {
        IntPtr cell = IntPtr.Zero;
        var iid = Ia2Interop.IidIAccessibleTableCell;
        try
        {
            var hr = Marshal.QueryInterface(ia2, ref iid, out cell);
            if (hr < 0 || cell == IntPtr.Zero)
            {
                return;
            }

            properties.Add(new("IA2 Table cell", "Interface", "IAccessibleTableCell"));
            AddInterfaceInt(properties, "IA2 Table cell", "Column span", cell, 3);
            AddInterfaceInt(properties, "IA2 Table cell", "Column", cell, 5);
            AddInterfaceInt(properties, "IA2 Table cell", "Row span", cell, 6);
            AddInterfaceInt(properties, "IA2 Table cell", "Row", cell, 8);
            AddInterfaceBool(properties, "IA2 Table cell", "Selected", cell, 9);
            AddCellExtents(properties, cell);
            AddHeaderCells(properties, relationships, cell, 4, "Column header", "IA2 TableCell columnHeaderCells");
            AddHeaderCells(properties, relationships, cell, 7, "Row header", "IA2 TableCell rowHeaderCells");
            AddAccessibleDescription(properties, "IA2 Table cell", "Containing table", cell, 11);
        }
        finally
        {
            if (cell != IntPtr.Zero)
            {
                Marshal.Release(cell);
            }
        }
    }

    private static void AddInterfaceInt(
        ICollection<AccessibilityProperty> properties,
        string group,
        string name,
        IntPtr pointer,
        int slot)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(pointer, slot);
            var hr = call(pointer, out var value);
            if (hr >= 0)
            {
                properties.Add(new(group, name, value.ToString()));
            }
        }
        catch (Exception ex)
        {
            properties.Add(new(group, name, $"Unavailable ({ex.Message})"));
        }
    }

    private static void AddInterfaceBool(
        ICollection<AccessibilityProperty> properties,
        string group,
        string name,
        IntPtr pointer,
        int slot)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.BoolOutDelegate>(pointer, slot);
            var hr = call(pointer, out var value);
            if (hr >= 0)
            {
                properties.Add(new(group, name, value.ToString()));
            }
        }
        catch (Exception ex)
        {
            properties.Add(new(group, name, $"Unavailable ({ex.Message})"));
        }
    }

    private static void AddCellExtents(ICollection<AccessibilityProperty> properties, IntPtr cell)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.RowColumnExtentsDelegate>(cell, 10);
            var hr = call(cell, out var row, out var column, out var rowSpan, out var columnSpan, out var selected);
            if (hr >= 0)
            {
                properties.Add(new(
                    "IA2 Table cell",
                    "Row/column extents",
                    $"row={row}; column={column}; row span={rowSpan}; column span={columnSpan}; selected={selected}"));
            }
        }
        catch
        {
            // This convenience method is optional. The individual properties above remain available.
        }
    }

    private static void AddHeaderCells(
        ICollection<AccessibilityProperty> properties,
        ICollection<AccessibilityRelationship> relationships,
        IntPtr cell,
        int slot,
        string relationshipType,
        string source)
    {
        IntPtr array = IntPtr.Zero;
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.PointerArrayOutDelegate>(cell, slot);
            var hr = call(cell, out array, out var count);
            if (hr < 0 || array == IntPtr.Zero || count <= 0)
            {
                properties.Add(new("IA2 Table cell", $"{relationshipType}s", "None"));
                return;
            }

            var descriptions = new List<string>();
            for (var index = 0; index < count; index++)
            {
                var target = Marshal.ReadIntPtr(array, index * IntPtr.Size);
                if (target == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    var mapped = MapIa2Target(relationshipType, target, source);
                    if (mapped is null)
                    {
                        continue;
                    }

                    relationships.Add(mapped);
                    descriptions.Add(FormatRelationshipTarget(mapped));
                }
                finally
                {
                    Marshal.Release(target);
                }
            }

            properties.Add(new(
                "IA2 Table cell",
                $"{relationshipType}s",
                descriptions.Count == 0 ? "None" : string.Join("; ", descriptions)));
        }
        catch (Exception ex)
        {
            properties.Add(new("IA2 Table cell", $"{relationshipType}s", $"Unavailable ({ex.Message})"));
        }
        finally
        {
            if (array != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(array);
            }
        }
    }

    private static void AddAccessibleDescription(
        ICollection<AccessibilityProperty> properties,
        string group,
        string name,
        IntPtr pointer,
        int slot)
    {
        IntPtr target = IntPtr.Zero;
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntPtrOutDelegate>(pointer, slot);
            var hr = call(pointer, out target);
            if (hr >= 0 && target != IntPtr.Zero)
            {
                var mapped = MapIa2Target(name, target);
                properties.Add(new(group, name, mapped is null ? "Available" : FormatRelationshipTarget(mapped)));
            }
        }
        catch (Exception ex)
        {
            properties.Add(new(group, name, $"Unavailable ({ex.Message})"));
        }
        finally
        {
            if (target != IntPtr.Zero)
            {
                Marshal.Release(target);
            }
        }
    }

    private static AccessibilityRelationship? MapIa2Target(
        string type,
        IntPtr unknown,
        string? source = null)
    {
        IntPtr accessiblePointer = IntPtr.Zero;
        try
        {
            if (!TryAcquireAccessibleFromIa2Target(unknown, out accessiblePointer) ||
                accessiblePointer == IntPtr.Zero)
            {
                return null;
            }

            var accessibleObject = Marshal.GetObjectForIUnknown(accessiblePointer);
            if (accessibleObject is not Accessibility.IAccessible accessible)
            {
                return null;
            }

            var name = Read(() => accessible.get_accName(ChildSelf));
            var roleObject = ReadObject(() => accessible.get_accRole(ChildSelf));
            var role = FormatMsaaRole(roleObject);
            var id = TryGetIa2UniqueId(unknown, out var uniqueId)
                ? uniqueId.ToString()
                : $"{name}|{role}";

            var x = 0;
            var y = 0;
            var width = 0;
            var height = 0;
            try
            {
                accessible.accLocation(out x, out y, out width, out height, ChildSelf);
            }
            catch
            {
                // Some relation targets are virtual or offscreen and have no usable rectangle.
            }

            return new AccessibilityRelationship(
                FriendlyRelationName(type),
                source ?? $"IA2 {type}",
                id,
                name,
                role,
                x,
                y,
                width,
                height);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (accessiblePointer != IntPtr.Zero)
            {
                Marshal.Release(accessiblePointer);
            }
        }
    }

    private static bool TryAcquireAccessibleFromIa2Target(
        IntPtr unknown,
        out IntPtr accessiblePointer)
    {
        accessiblePointer = IntPtr.Zero;

        var accessibleIid = Ia2Interop.IidIAccessible;
        var directHr = Marshal.QueryInterface(unknown, ref accessibleIid, out accessiblePointer);
        if (directHr >= 0 && accessiblePointer != IntPtr.Zero)
        {
            return true;
        }

        IntPtr serviceProvider = IntPtr.Zero;
        try
        {
            var serviceProviderIid = Ia2Interop.IidIServiceProvider;
            var serviceHr = Marshal.QueryInterface(unknown, ref serviceProviderIid, out serviceProvider);
            if (serviceHr < 0 || serviceProvider == IntPtr.Zero)
            {
                return false;
            }

            var queryService = Ia2Interop.GetVtableDelegate<Ia2Interop.QueryServiceDelegate>(
                serviceProvider,
                3);
            var service = Ia2Interop.IidIAccessible;
            accessibleIid = Ia2Interop.IidIAccessible;
            var queryHr = queryService(
                serviceProvider,
                ref service,
                ref accessibleIid,
                out accessiblePointer);
            return queryHr >= 0 && accessiblePointer != IntPtr.Zero;
        }
        finally
        {
            if (serviceProvider != IntPtr.Zero)
            {
                Marshal.Release(serviceProvider);
            }
        }
    }

    private static bool TryGetIa2UniqueId(IntPtr unknown, out int uniqueId)
    {
        uniqueId = 0;
        IntPtr ia2 = IntPtr.Zero;
        var iid = Ia2Interop.IidIAccessible2;
        try
        {
            var hr = Marshal.QueryInterface(unknown, ref iid, out ia2);
            if (hr < 0 || ia2 == IntPtr.Zero)
            {
                return false;
            }

            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(ia2, Ia2Interop.Ia2UniqueIdSlot);
            return call(ia2, out uniqueId) >= 0;
        }
        finally
        {
            if (ia2 != IntPtr.Zero)
            {
                Marshal.Release(ia2);
            }
        }
    }

    private static string ReadBstrMethod(IntPtr pointer, int slot)
    {
        IntPtr bstr = IntPtr.Zero;
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntPtrOutDelegate>(pointer, slot);
            var hr = call(pointer, out bstr);
            return hr >= 0 && bstr != IntPtr.Zero
                ? Marshal.PtrToStringBSTR(bstr) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (bstr != IntPtr.Zero)
            {
                Marshal.FreeBSTR(bstr);
            }
        }
    }

    private static string FormatRelationshipTarget(AccessibilityRelationship relationship)
    {
        var name = string.IsNullOrWhiteSpace(relationship.TargetName) ? "unnamed" : relationship.TargetName;
        return $"{name} ({relationship.TargetControlType})";
    }

    private static bool IsContainingRelation(string type) =>
        type.StartsWith("containing", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "nodeChildOf", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "subwindowOf", StringComparison.OrdinalIgnoreCase);

    private static string FriendlyRelationName(string type) => type switch
    {
        "labelledBy" => "Labeled by",
        "labeledBy" => "Labeled by",
        "labelFor" => "Label for",
        "describedBy" => "Described by",
        "descriptionFor" => "Description for",
        "controllerFor" => "Controller for",
        "controlledBy" => "Controlled by",
        "flowsTo" => "Flows to",
        "flowsFrom" => "Flows from",
        "details" => "Details",
        "detailsFor" => "Details for",
        "error" => "Error message",
        "errorFor" => "Error for",
        "memberOf" => "Member of",
        "nodeParentOf" => "Node parent of",
        "popupFor" => "Popup for",
        _ => type
    };

    private static bool TryAcquireIa2(object candidate, out IntPtr ia2Pointer, out string detail)
    {
        ia2Pointer = IntPtr.Zero;
        detail = "IAccessible2 was not exposed";
        IntPtr unknown = IntPtr.Zero;
        IntPtr serviceProvider = IntPtr.Zero;

        try
        {
            unknown = Marshal.GetIUnknownForObject(candidate);

            var ia2Iid = Ia2Interop.IidIAccessible2;
            var directHr = Marshal.QueryInterface(unknown, ref ia2Iid, out ia2Pointer);
            if (directHr >= 0 && ia2Pointer != IntPtr.Zero)
            {
                detail = "Direct QueryInterface";
                return true;
            }

            var serviceProviderIid = Ia2Interop.IidIServiceProvider;
            var serviceProviderHr = Marshal.QueryInterface(
                unknown,
                ref serviceProviderIid,
                out serviceProvider);

            if (serviceProviderHr < 0 || serviceProvider == IntPtr.Zero)
            {
                detail = $"IA2 QueryInterface: 0x{directHr:X8}; IServiceProvider QueryInterface: 0x{serviceProviderHr:X8}";
                return false;
            }

            var queryService = Ia2Interop.GetVtableDelegate<Ia2Interop.QueryServiceDelegate>(
                serviceProvider,
                3);

            var service = Ia2Interop.IidIAccessible;
            ia2Iid = Ia2Interop.IidIAccessible2;
            var queryServiceHr = queryService(serviceProvider, ref service, ref ia2Iid, out ia2Pointer);

            if (queryServiceHr >= 0 && ia2Pointer != IntPtr.Zero)
            {
                detail = "IServiceProvider.QueryService(IID_IAccessible, IID_IAccessible2)";
                return true;
            }

            // Some providers accept IID_IAccessible2 as the service identifier.
            service = Ia2Interop.IidIAccessible2;
            ia2Iid = Ia2Interop.IidIAccessible2;
            var fallbackHr = queryService(serviceProvider, ref service, ref ia2Iid, out ia2Pointer);
            if (fallbackHr >= 0 && ia2Pointer != IntPtr.Zero)
            {
                detail = "IServiceProvider.QueryService(IID_IAccessible2, IID_IAccessible2)";
                return true;
            }

            detail = $"IA2 QueryInterface: 0x{directHr:X8}; QueryService: 0x{queryServiceHr:X8}; fallback: 0x{fallbackHr:X8}";
            return false;
        }
        catch (Exception ex)
        {
            if (ia2Pointer != IntPtr.Zero)
            {
                Marshal.Release(ia2Pointer);
                ia2Pointer = IntPtr.Zero;
            }

            detail = $"IA2 acquisition error: {ex.Message}";
            return false;
        }
        finally
        {
            if (serviceProvider != IntPtr.Zero)
            {
                Marshal.Release(serviceProvider);
            }

            if (unknown != IntPtr.Zero)
            {
                Marshal.Release(unknown);
            }
        }
    }

    private static IEnumerable<AccessibilityProperty> ReadIa2(
        IntPtr ia2,
        object? msaaRole,
        string accessibleName)
    {
        var properties = new List<AccessibilityProperty>
        {
            new("IA2", "Available", "True"),
            new("IA2", "Name", accessibleName)
        };

        AddIa2Role(properties, ia2, msaaRole);
        AddUInt(properties, "States", ia2, Ia2Interop.Ia2StatesSlot, FormatStates);
        AddBstr(properties, "Attributes", ia2, Ia2Interop.Ia2AttributesSlot);
        AddBstr(properties, "Extended role", ia2, Ia2Interop.Ia2ExtendedRoleSlot);
        AddBstr(properties, "Localized extended role", ia2, Ia2Interop.Ia2LocalizedExtendedRoleSlot);
        AddInt(properties, "Unique ID", ia2, Ia2Interop.Ia2UniqueIdSlot, value => value.ToString());
        AddPointer(properties, "Window handle", ia2, Ia2Interop.Ia2WindowHandleSlot,
            value => value.ToInt64().ToString());
        AddInt(properties, "Index in parent", ia2, Ia2Interop.Ia2IndexInParentSlot, value => value.ToString());
        AddInt(properties, "Relation count", ia2, Ia2Interop.Ia2NRelationsSlot, value => value.ToString());
        AddInt(properties, "Extended state count", ia2, Ia2Interop.Ia2NExtendedStatesSlot, value => value.ToString());
        AddGroupPosition(properties, ia2);
        AddLocale(properties, ia2);

        return properties;
    }

    private static void AddIa2Role(
        ICollection<AccessibilityProperty> properties,
        IntPtr ia2,
        object? msaaRole)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(
                ia2,
                Ia2Interop.Ia2RoleSlot);
            var hr = call(ia2, out var role);
            if (hr >= 0)
            {
                properties.Add(new("IA2", "Role", FormatIa2Role(role)));
                properties.Add(new("IA2", "Role identifier", RoleIdentifier(role)));
                return;
            }

            var fallback = FormatMsaaRole(msaaRole);
            properties.Add(new(
                "IA2",
                "Role",
                string.IsNullOrWhiteSpace(fallback)
                    ? "Unavailable"
                    : $"{fallback} (MSAA fallback)"));
            properties.Add(new(
                "IA2",
                "Role source",
                $"IA2 role unavailable; using MSAA role. HRESULT 0x{hr:X8}"));
        }
        catch (Exception ex)
        {
            var fallback = FormatMsaaRole(msaaRole);
            properties.Add(new(
                "IA2",
                "Role",
                string.IsNullOrWhiteSpace(fallback)
                    ? "Unavailable"
                    : $"{fallback} (MSAA fallback)"));
            properties.Add(new("IA2", "Role source", $"IA2 role read failed: {ex.Message}"));
        }
    }

    private static void AddInt(
        ICollection<AccessibilityProperty> properties,
        string name,
        IntPtr ia2,
        int slot,
        Func<int, string> format)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntOutDelegate>(ia2, slot);
            var hr = call(ia2, out var value);
            properties.Add(new("IA2", name, hr >= 0 ? format(value) : $"Unavailable (0x{hr:X8})"));
        }
        catch (Exception ex)
        {
            properties.Add(new("IA2", name, $"Unavailable ({ex.Message})"));
        }
    }

    private static void AddUInt(
        ICollection<AccessibilityProperty> properties,
        string name,
        IntPtr ia2,
        int slot,
        Func<uint, string> format)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.UIntOutDelegate>(ia2, slot);
            var hr = call(ia2, out var value);
            properties.Add(new("IA2", name, hr >= 0 ? format(value) : $"Unavailable (HRESULT 0x{hr:X8})"));
        }
        catch (Exception ex)
        {
            properties.Add(new("IA2", name, $"Unavailable ({ex.GetType().Name}: {ex.Message})"));
        }
    }

    private static void AddPointer(
        ICollection<AccessibilityProperty> properties,
        string name,
        IntPtr ia2,
        int slot,
        Func<IntPtr, string> format)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntPtrOutDelegate>(ia2, slot);
            var hr = call(ia2, out var value);
            properties.Add(new("IA2", name, hr >= 0 ? format(value) : $"Unavailable (0x{hr:X8})"));
        }
        catch (Exception ex)
        {
            properties.Add(new("IA2", name, $"Unavailable ({ex.Message})"));
        }
    }

    private static void AddBstr(
        ICollection<AccessibilityProperty> properties,
        string name,
        IntPtr ia2,
        int slot)
    {
        IntPtr bstr = IntPtr.Zero;
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.IntPtrOutDelegate>(ia2, slot);
            var hr = call(ia2, out bstr);
            var value = hr >= 0 && bstr != IntPtr.Zero
                ? Marshal.PtrToStringBSTR(bstr) ?? string.Empty
                : $"Unavailable (0x{hr:X8})";
            properties.Add(new("IA2", name, value));
        }
        catch (Exception ex)
        {
            properties.Add(new("IA2", name, $"Unavailable ({ex.Message})"));
        }
        finally
        {
            if (bstr != IntPtr.Zero)
            {
                Marshal.FreeBSTR(bstr);
            }
        }
    }

    private static void AddGroupPosition(ICollection<AccessibilityProperty> properties, IntPtr ia2)
    {
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.GroupPositionDelegate>(
                ia2,
                Ia2Interop.Ia2GroupPositionSlot);
            var hr = call(ia2, out var level, out var count, out var position);
            var value = hr >= 0
                ? $"level={level}; count={count}; position={position}"
                : $"Unavailable (0x{hr:X8})";
            properties.Add(new("IA2", "Group position", value));
        }
        catch (Exception ex)
        {
            properties.Add(new("IA2", "Group position", $"Unavailable ({ex.Message})"));
        }
    }

    private static void AddLocale(ICollection<AccessibilityProperty> properties, IntPtr ia2)
    {
        var locale = default(Ia2Interop.NativeIa2Locale);
        try
        {
            var call = Ia2Interop.GetVtableDelegate<Ia2Interop.LocaleDelegate>(
                ia2,
                Ia2Interop.Ia2LocaleSlot);
            var hr = call(ia2, out locale);
            var value = hr >= 0
                ? string.Join("-", new[]
                {
                    BstrToString(locale.Language),
                    BstrToString(locale.Country),
                    BstrToString(locale.Variant)
                }.Where(part => !string.IsNullOrWhiteSpace(part)))
                : $"Unavailable (0x{hr:X8})";
            properties.Add(new("IA2", "Locale", value));
        }
        catch (Exception ex)
        {
            properties.Add(new("IA2", "Locale", $"Unavailable ({ex.Message})"));
        }
        finally
        {
            FreeBstr(locale.Language);
            FreeBstr(locale.Country);
            FreeBstr(locale.Variant);
        }
    }

    private static string BstrToString(IntPtr value) =>
        value == IntPtr.Zero ? string.Empty : Marshal.PtrToStringBSTR(value) ?? string.Empty;

    private static void FreeBstr(IntPtr value)
    {
        if (value != IntPtr.Zero)
        {
            Marshal.FreeBSTR(value);
        }
    }

    private static string FormatStates(uint states)
    {
        var names = new List<string>();
        AddState(names, states, 0x00000001, "active");
        AddState(names, states, 0x00000002, "armed");
        AddState(names, states, 0x00000004, "defunct");
        AddState(names, states, 0x00000008, "editable");
        AddState(names, states, 0x00000010, "horizontal");
        AddState(names, states, 0x00000020, "iconified");
        AddState(names, states, 0x00000040, "invalid-entry");
        AddState(names, states, 0x00000080, "manages-descendants");
        AddState(names, states, 0x00000100, "modal");
        AddState(names, states, 0x00000200, "multi-line");
        AddState(names, states, 0x00000400, "opaque");
        AddState(names, states, 0x00000800, "required");
        AddState(names, states, 0x00001000, "selectable-text");
        AddState(names, states, 0x00002000, "single-line");
        AddState(names, states, 0x00004000, "stale");
        AddState(names, states, 0x00008000, "supports-autocompletion");
        AddState(names, states, 0x00010000, "transient");
        AddState(names, states, 0x00020000, "vertical");
        AddState(names, states, 0x00040000, "checkable");
        AddState(names, states, 0x00080000, "pinned");
        return names.Count == 0 ? "none" : string.Join(", ", names);
    }

    private static void AddState(ICollection<string> names, uint states, uint flag, string name)
    {
        if ((states & flag) != 0)
        {
            names.Add(name);
        }
    }

    private static object? ResolveChild(Accessibility.IAccessible accessible, object? childId)
    {
        if (childId is not int id || id == 0)
        {
            return accessible;
        }

        try
        {
            return accessible.get_accChild(id);
        }
        catch
        {
            return accessible;
        }
    }

    private static List<AccessibilityProperty> ReadMsaa(
        Accessibility.IAccessible accessible,
        object child)
    {
        return
        [
            new("MSAA", "Name", Read(() => accessible.get_accName(child))),
            new("MSAA", "Role", FormatMsaaRole(ReadObject(() => accessible.get_accRole(child)))),
            new("MSAA", "State", FormatMsaaState(ReadObject(() => accessible.get_accState(child)))),
            new("MSAA", "Value", Read(() => accessible.get_accValue(child))),
            new("MSAA", "Description", Read(() => accessible.get_accDescription(child))),
            new("MSAA", "Help", Read(() => accessible.get_accHelp(child))),
            new("MSAA", "Keyboard shortcut", Read(() => accessible.get_accKeyboardShortcut(child))),
            new("MSAA", "Default action", Read(() => accessible.get_accDefaultAction(child))),
            new("MSAA", "Child count", ReadInt(() => accessible.accChildCount).ToString())
        ];
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

    private static string FormatMsaaRole(object? value)
    {
        if (!TryGetUnsignedValue(value, out var role))
        {
            return value?.ToString() ?? string.Empty;
        }

        return GetOleaccRoleName(role) ?? $"Unknown MSAA role ({role})";
    }

    private static string FormatMsaaState(object? value)
    {
        if (!TryGetUnsignedValue(value, out var states))
        {
            return value?.ToString() ?? string.Empty;
        }

        if (states == 0)
        {
            return "normal";
        }

        var names = new List<string>();
        for (var bit = 0; bit < 32; bit++)
        {
            var flag = 1u << bit;
            if ((states & flag) == 0)
            {
                continue;
            }

            names.Add(GetOleaccStateName(flag) ?? $"unknown state bit {flag}");
        }

        return string.Join(", ", names);
    }

    private static bool TryGetUnsignedValue(object? value, out uint result)
    {
        switch (value)
        {
            case int number:
                result = unchecked((uint)number);
                return true;
            case uint number:
                result = number;
                return true;
            case short number:
                result = unchecked((uint)number);
                return true;
            case ushort number:
                result = number;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static string FormatIa2Role(int role)
    {
        if (role == 0)
        {
            return "unknown";
        }

        if (role < 0x401)
        {
            return GetOleaccRoleName(unchecked((uint)role))
                ?? $"Unknown MSAA role ({role})";
        }

        return Ia2RoleName(role) ?? $"Unknown IA2 role ({role})";
    }


    private static string RoleIdentifier(int role)
    {
        if (role == 0)
        {
            return "IA2_ROLE_UNKNOWN";
        }

        if (role < 0x401)
        {
            return $"MSAA role {role}";
        }

        return role switch
        {
            0x401 => "IA2_ROLE_CANVAS",
            0x402 => "IA2_ROLE_CAPTION",
            0x403 => "IA2_ROLE_CHECK_MENU_ITEM",
            0x404 => "IA2_ROLE_COLOR_CHOOSER",
            0x405 => "IA2_ROLE_DATE_EDITOR",
            0x406 => "IA2_ROLE_DESKTOP_ICON",
            0x407 => "IA2_ROLE_DESKTOP_PANE",
            0x408 => "IA2_ROLE_DIRECTORY_PANE",
            0x409 => "IA2_ROLE_EDITBAR",
            0x40A => "IA2_ROLE_EMBEDDED_OBJECT",
            0x40B => "IA2_ROLE_ENDNOTE",
            0x40C => "IA2_ROLE_FILE_CHOOSER",
            0x40D => "IA2_ROLE_FONT_CHOOSER",
            0x40E => "IA2_ROLE_FOOTER",
            0x40F => "IA2_ROLE_FOOTNOTE",
            0x410 => "IA2_ROLE_FORM",
            0x411 => "IA2_ROLE_FRAME",
            0x412 => "IA2_ROLE_GLASS_PANE",
            0x413 => "IA2_ROLE_HEADER",
            0x414 => "IA2_ROLE_HEADING",
            0x415 => "IA2_ROLE_ICON",
            0x416 => "IA2_ROLE_IMAGE_MAP",
            0x417 => "IA2_ROLE_INPUT_METHOD_WINDOW",
            0x418 => "IA2_ROLE_INTERNAL_FRAME",
            0x419 => "IA2_ROLE_LABEL",
            0x41A => "IA2_ROLE_LAYERED_PANE",
            0x41B => "IA2_ROLE_NOTE",
            0x41C => "IA2_ROLE_OPTION_PANE",
            0x41D => "IA2_ROLE_PAGE",
            0x41E => "IA2_ROLE_PARAGRAPH",
            0x41F => "IA2_ROLE_RADIO_MENU_ITEM",
            0x420 => "IA2_ROLE_REDUNDANT_OBJECT",
            0x421 => "IA2_ROLE_ROOT_PANE",
            0x422 => "IA2_ROLE_RULER",
            0x423 => "IA2_ROLE_SCROLL_PANE",
            0x424 => "IA2_ROLE_SECTION",
            0x425 => "IA2_ROLE_SHAPE",
            0x426 => "IA2_ROLE_SPLIT_PANE",
            0x427 => "IA2_ROLE_TEAR_OFF_MENU",
            0x428 => "IA2_ROLE_TERMINAL",
            0x429 => "IA2_ROLE_TEXT_FRAME",
            0x42A => "IA2_ROLE_TOGGLE_BUTTON",
            0x42B => "IA2_ROLE_VIEW_PORT",
            0x42C => "IA2_ROLE_COMPLEMENTARY_CONTENT",
            0x42D => "IA2_ROLE_LANDMARK",
            0x42E => "IA2_ROLE_LEVEL_BAR",
            0x42F => "IA2_ROLE_CONTENT_DELETION",
            0x430 => "IA2_ROLE_CONTENT_INSERTION",
            0x431 => "IA2_ROLE_BLOCK_QUOTE",
            0x432 => "IA2_ROLE_MARK",
            0x433 => "IA2_ROLE_SUGGESTION",
            0x434 => "IA2_ROLE_COMMENT",
            _ => $"IA2 role {role}"
        };
    }

    private static string? GetOleaccRoleName(uint role)
    {
        var length = Ia2Interop.GetRoleText(role, null, 0);
        if (length == 0)
        {
            return null;
        }

        var buffer = new StringBuilder(checked((int)length + 1));
        return Ia2Interop.GetRoleText(role, buffer, length + 1) == 0
            ? null
            : buffer.ToString();
    }

    private static string? GetOleaccStateName(uint state)
    {
        var length = Ia2Interop.GetStateText(state, null, 0);
        if (length == 0)
        {
            return null;
        }

        var buffer = new StringBuilder(checked((int)length + 1));
        return Ia2Interop.GetStateText(state, buffer, length + 1) == 0
            ? null
            : buffer.ToString();
    }

    private static string? Ia2RoleName(int role) => role switch
    {
        0x401 => "canvas",
        0x402 => "caption",
        0x403 => "check menu item",
        0x404 => "colour chooser",
        0x405 => "date editor",
        0x406 => "desktop icon",
        0x407 => "desktop pane",
        0x408 => "directory pane",
        0x409 => "edit bar",
        0x40A => "embedded object",
        0x40B => "endnote",
        0x40C => "file chooser",
        0x40D => "font chooser",
        0x40E => "footer",
        0x40F => "footnote",
        0x410 => "form",
        0x411 => "frame",
        0x412 => "glass pane",
        0x413 => "header",
        0x414 => "heading",
        0x415 => "icon",
        0x416 => "image map",
        0x417 => "input method window",
        0x418 => "internal frame",
        0x419 => "label",
        0x41A => "layered pane",
        0x41B => "note",
        0x41C => "option pane",
        0x41D => "page",
        0x41E => "paragraph",
        0x41F => "radio menu item",
        0x420 => "redundant object",
        0x421 => "root pane",
        0x422 => "ruler",
        0x423 => "scroll pane",
        0x424 => "section",
        0x425 => "shape",
        0x426 => "split pane",
        0x427 => "tear-off menu",
        0x428 => "terminal",
        0x429 => "text frame",
        0x42A => "toggle button",
        0x42B => "viewport",
        0x42C => "complementary content",
        0x42D => "landmark",
        0x42E => "level bar",
        0x42F => "content deletion",
        0x430 => "content insertion",
        0x431 => "block quote",
        0x432 => "mark",
        0x433 => "suggestion",
        0x434 => "comment",
        _ => null
    };
}
