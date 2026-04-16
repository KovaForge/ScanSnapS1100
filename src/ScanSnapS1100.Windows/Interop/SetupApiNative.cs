using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ScanSnapS1100.Windows.Interop;

[Flags]
internal enum SetupDiGetClassDevsFlags : uint
{
    Present = 0x00000002,
    DeviceInterface = 0x00000010,
}

[StructLayout(LayoutKind.Sequential)]
internal struct SP_DEVICE_INTERFACE_DATA
{
    public int cbSize;
    public Guid InterfaceClassGuid;
    public int Flags;
    public IntPtr Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SP_DEVINFO_DATA
{
    public int cbSize;
    public Guid ClassGuid;
    public uint DevInst;
    public IntPtr Reserved;
}

internal sealed class SafeDeviceInfoSetHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeDeviceInfoSetHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        return SetupApiNative.SetupDiDestroyDeviceInfoList(handle);
    }
}

internal static class SetupApiNative
{
    public const int ErrorInsufficientBuffer = 122;
    public const int ErrorNoMoreItems = 259;

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetClassDevsW")]
    internal static extern SafeDeviceInfoSetHandle SetupDiGetClassDevsW(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        SetupDiGetClassDevsFlags flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInterfaces(
        SafeDeviceInfoSetHandle deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        int memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceInterfaceDetailW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInterfaceDetailW(
        SafeDeviceInfoSetHandle deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        int deviceInterfaceDetailDataSize,
        out int requiredSize,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceInstanceIdW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInstanceIdW(
        SafeDeviceInfoSetHandle deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        StringBuilder deviceInstanceId,
        int deviceInstanceIdSize,
        out int requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    internal static int GetLastError()
    {
        return Marshal.GetLastPInvokeError();
    }
}
