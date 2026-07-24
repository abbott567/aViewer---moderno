using System.Runtime.InteropServices;
using System.Text;

namespace AViewer.Core.Services;

internal static class Ia2Interop
{
    internal static readonly Guid IidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");
    internal static readonly Guid IidIAccessible2 = new("E89F726E-C4F4-4C19-BB19-B647D7FA8478");
    internal static readonly Guid IidIServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    internal static readonly Guid IidIAccessibleRelation = new("7CDF86EE-C3DA-496A-BDA4-281B336E1FDC");
    internal static readonly Guid IidIAccessibleTable2 = new("6167F295-06F0-4CDD-A1FA-02E25153D869");
    internal static readonly Guid IidIAccessibleTableCell = new("594116B1-C99F-4847-AD06-0A7A86ECE645");

    // IAccessible2 extends IAccessible. IAccessible2's first method therefore
    // follows IUnknown (3), IDispatch (4), and IAccessible (21): slot 28.
    internal const int Ia2NRelationsSlot = 28;
    internal const int Ia2RoleSlot = 31;
    internal const int Ia2GroupPositionSlot = 34;
    internal const int Ia2StatesSlot = 35;
    internal const int Ia2ExtendedRoleSlot = 36;
    internal const int Ia2LocalizedExtendedRoleSlot = 37;
    internal const int Ia2NExtendedStatesSlot = 38;
    internal const int Ia2UniqueIdSlot = 41;
    internal const int Ia2WindowHandleSlot = 42;
    internal const int Ia2IndexInParentSlot = 43;
    internal const int Ia2LocaleSlot = 44;
    internal const int Ia2AttributesSlot = 45;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativePoint(int x, int y)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeIa2Locale
    {
        internal IntPtr Language;
        internal IntPtr Country;
        internal IntPtr Variant;
    }

    [DllImport("oleacc.dll")]
    internal static extern int AccessibleObjectFromPoint(
        NativePoint point,
        [MarshalAs(UnmanagedType.Interface)] out object accessible,
        [MarshalAs(UnmanagedType.Struct)] out object childId);


    [DllImport("oleacc.dll", CharSet = CharSet.Unicode, EntryPoint = "GetRoleTextW")]
    internal static extern uint GetRoleText(
        uint role,
        StringBuilder? roleText,
        uint roleTextMax);

    [DllImport("oleacc.dll", CharSet = CharSet.Unicode, EntryPoint = "GetStateTextW")]
    internal static extern uint GetStateText(
        uint stateBit,
        StringBuilder? stateText,
        uint stateTextMax);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int QueryServiceDelegate(
        IntPtr self,
        ref Guid service,
        ref Guid requestedInterface,
        out IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int IntOutDelegate(IntPtr self, out int value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int UIntOutDelegate(IntPtr self, out uint value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int IntPtrOutDelegate(IntPtr self, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GroupPositionDelegate(
        IntPtr self,
        out int groupLevel,
        out int similarItemsInGroup,
        out int positionInGroup);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int LocaleDelegate(IntPtr self, out NativeIa2Locale locale);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int IndexedPointerOutDelegate(IntPtr self, int index, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int PointerArrayOutDelegate(IntPtr self, out IntPtr values, out int count);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int BoolOutDelegate(IntPtr self, [MarshalAs(UnmanagedType.U1)] out bool value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int RowColumnExtentsDelegate(
        IntPtr self,
        out int row,
        out int column,
        out int rowExtent,
        out int columnExtent,
        [MarshalAs(UnmanagedType.U1)] out bool selected);

    internal static T GetVtableDelegate<T>(IntPtr interfacePointer, int slot)
        where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(interfacePointer);
        var method = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }
}
