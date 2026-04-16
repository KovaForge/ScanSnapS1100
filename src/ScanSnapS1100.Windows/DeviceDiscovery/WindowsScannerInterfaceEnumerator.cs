using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using ScanSnapS1100.Windows.Interop;

namespace ScanSnapS1100.Windows.DeviceDiscovery;

public static class WindowsScannerInterfaceEnumerator
{
    private static readonly Guid ImageClassGuid = new("6bdd1fc6-810f-11d0-bec7-08002be2092f");

    public static IReadOnlyDictionary<string, string[]> EnumerateImageInterfacesByInstanceId()
    {
        var interfaceClassGuid = ImageClassGuid;

        using var deviceInfoSet = SetupApiNative.SetupDiGetClassDevsW(
            ref interfaceClassGuid,
            enumerator: null,
            hwndParent: IntPtr.Zero,
            SetupDiGetClassDevsFlags.Present | SetupDiGetClassDevsFlags.DeviceInterface);

        if (deviceInfoSet.IsInvalid)
        {
            throw new Win32Exception(SetupApiNative.GetLastError(), "Failed to enumerate image-class device interfaces.");
        }

        var results = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; ; index++)
        {
            var interfaceData = new SP_DEVICE_INTERFACE_DATA
            {
                cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>(),
            };

            if (!SetupApiNative.SetupDiEnumDeviceInterfaces(
                deviceInfoSet,
                IntPtr.Zero,
                ref interfaceClassGuid,
                index,
                ref interfaceData))
            {
                var error = SetupApiNative.GetLastError();
                if (error == SetupApiNative.ErrorNoMoreItems)
                {
                    break;
                }

                throw new Win32Exception(error, $"Failed to enumerate device interface index {index}.");
            }

            var deviceInfoData = new SP_DEVINFO_DATA
            {
                cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>(),
            };

            SetupApiNative.SetupDiGetDeviceInterfaceDetailW(
                deviceInfoSet,
                ref interfaceData,
                IntPtr.Zero,
                0,
                out var requiredSize,
                ref deviceInfoData);

            var detailError = SetupApiNative.GetLastError();
            if (requiredSize <= 0 || detailError != SetupApiNative.ErrorInsufficientBuffer)
            {
                throw new Win32Exception(detailError, "Failed to size the device interface detail buffer.");
            }

            var detailBuffer = Marshal.AllocHGlobal(requiredSize);
            try
            {
                Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);

                if (!SetupApiNative.SetupDiGetDeviceInterfaceDetailW(
                        deviceInfoSet,
                        ref interfaceData,
                        detailBuffer,
                        requiredSize,
                        out _,
                        ref deviceInfoData))
                {
                    throw new Win32Exception(SetupApiNative.GetLastError(), "Failed to resolve the image-class device path.");
                }

                var devicePath = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, sizeof(int)));
                if (string.IsNullOrWhiteSpace(devicePath))
                {
                    continue;
                }

                var instanceId = ReadDeviceInstanceId(deviceInfoSet, ref deviceInfoData);
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    continue;
                }

                if (!results.TryGetValue(instanceId, out var paths))
                {
                    paths = [];
                    results[instanceId] = paths;
                }

                paths.Add(devicePath);
            }
            finally
            {
                Marshal.FreeHGlobal(detailBuffer);
            }
        }

        return results.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadDeviceInstanceId(
        SafeDeviceInfoSetHandle deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData)
    {
        var buffer = new StringBuilder(512);
        if (SetupApiNative.SetupDiGetDeviceInstanceIdW(
                deviceInfoSet,
                ref deviceInfoData,
                buffer,
                buffer.Capacity,
                out _))
        {
            return buffer.ToString();
        }

        throw new Win32Exception(SetupApiNative.GetLastError(), "Failed to read the device instance ID for an image-class interface.");
    }
}
