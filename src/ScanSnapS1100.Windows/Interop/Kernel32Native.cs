using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ScanSnapS1100.Windows.Interop;

internal static class Kernel32Native
{
    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    internal const uint FileShareRead = 0x00000001;
    internal const uint FileShareWrite = 0x00000002;
    internal const uint OpenExisting = 3;
    internal const uint FileAttributeNormal = 0x00000080;
    internal const uint FileFlagOverlapped = 0x40000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    internal static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    internal static int GetLastError()
    {
        return Marshal.GetLastPInvokeError();
    }
}
