using System.Runtime.InteropServices;
using System.Text;

namespace AViewer.Core.Services;

internal static class Ia2Interop
{
    internal static readonly Guid IidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");
    internal static readonly Guid IidIAccessible2 = new("E89F726E-C4F4-4C19-BB19-B647D7FA8478");
    internal static readonly Guid IidIAccessible2_2 = new("6C9430E9-299D-4E6F-BD01-A82A1E88D3FF");
    internal static readonly Guid IidIAccessibleTableCell = new("594116B1-C99F-4847-AD06-0A7A86ECE645");
    internal static readonly Guid IidIServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");

    internal const int Ia2NRelationsSlot = 28;
    internal const int Ia2RelationSlot = 29;
    internal const int Ia2RoleSlot = 31;
    internal const int Ia2StatesSlot = 35;
    internal const int Ia2ExtendedRoleSlot = 36;
    internal const int Ia2LocalizedExtendedRoleSlot = 37;
    internal const int Ia2UniqueIdSlot = 41;
    internal const int Ia2AttributesSlot = 45;
    internal const int Ia2_2RelationTargetsOfTypeSlot = 48;
    internal const int TableCellColumnExtentSlot = 3;
    internal const int TableCellColumnHeaderCellsSlot = 4;
    internal const int TableCellColumnIndexSlot = 5;
    internal const int TableCellRowExtentSlot = 6;
    internal const int TableCellRowHeaderCellsSlot = 7;
    internal const int TableCellRowIndexSlot = 8;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct NativePoint(int x, int y)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
    }

    [DllImport("oleacc.dll")]
    internal static extern int AccessibleObjectFromPoint(
        NativePoint point,
        [MarshalAs(UnmanagedType.Interface)] out object accessible,
        [MarshalAs(UnmanagedType.Struct)] out object childId);

    [DllImport("oleacc.dll", CharSet = CharSet.Unicode, EntryPoint = "GetRoleTextW")]
    internal static extern uint GetRoleText(uint role, StringBuilder? text, uint maxLength);

    [DllImport("oleacc.dll", CharSet = CharSet.Unicode, EntryPoint = "GetStateTextW")]
    internal static extern uint GetStateText(uint state, StringBuilder? text, uint maxLength);

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
    internal delegate int IndexedPointerOutDelegate(IntPtr self, int index, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int PointerArrayOutDelegate(
        IntPtr self,
        out IntPtr values,
        out int count);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int RelationTargetsOfTypeDelegate(
        IntPtr self,
        [MarshalAs(UnmanagedType.BStr)] string relationType,
        int maxTargets,
        out IntPtr targets,
        out int count);

    internal static T GetVtableDelegate<T>(IntPtr interfacePointer, int slot)
        where T : Delegate
    {
        var vtable = Marshal.ReadIntPtr(interfacePointer);
        var method = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }
}
