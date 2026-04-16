using System.Management;
using System.Text.RegularExpressions;
using ScanSnapS1100.Core.Models;

namespace ScanSnapS1100.Windows.DeviceDiscovery;

public static partial class WindowsScanSnapDiscovery
{
    private static readonly Regex UsbIdPattern = UsbIdRegex();

    public static IReadOnlyList<WindowsAttachedScanner> FindSupportedDevices()
    {
        var entitiesById = new Dictionary<string, WindowsAttachedScanner>(StringComparer.OrdinalIgnoreCase);

        using var entitySearcher = new ManagementObjectSearcher(
            "SELECT Name, Manufacturer, PNPDeviceID, PNPClass, ClassGuid, ConfigManagerErrorCode, HardwareID, CompatibleID, Service, Status FROM Win32_PnPEntity");

        foreach (ManagementObject entity in entitySearcher.Get())
        {
            var pnpDeviceId = entity["PNPDeviceID"]?.ToString();
            if (string.IsNullOrWhiteSpace(pnpDeviceId))
            {
                continue;
            }

            if (!TryParseUsbIds(pnpDeviceId, out var vendorId, out var productId))
            {
                continue;
            }

            if (vendorId != ScanSnapUsbIds.VendorId || !ScanSnapUsbIds.IsSupportedProductId(productId))
            {
                continue;
            }

            entitiesById[pnpDeviceId] = new WindowsAttachedScanner(
                Name: entity["Name"]?.ToString() ?? "Unknown",
                Manufacturer: entity["Manufacturer"]?.ToString() ?? "Unknown",
                PnpDeviceId: pnpDeviceId,
                PnpClass: entity["PNPClass"]?.ToString(),
                ClassGuid: entity["ClassGuid"]?.ToString(),
                VendorId: vendorId,
                ProductId: productId,
                ConfigManagerErrorCode: TryReadInt32(entity["ConfigManagerErrorCode"]),
                HardwareIds: ReadStringArray(entity["HardwareID"]),
                CompatibleIds: ReadStringArray(entity["CompatibleID"]),
                Service: entity["Service"]?.ToString(),
                DriverProviderName: null,
                DriverDate: null,
                DriverVersion: null,
                DriverName: null,
                InfName: null,
                Status: entity["Status"]?.ToString());
        }

        using var driverSearcher = new ManagementObjectSearcher(
            "SELECT DeviceID, DriverProviderName, DriverDate, DriverVersion, DriverName, InfName FROM Win32_PnPSignedDriver");

        foreach (ManagementObject driver in driverSearcher.Get())
        {
            var deviceId = driver["DeviceID"]?.ToString();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            if (!entitiesById.TryGetValue(deviceId, out var entity))
            {
                continue;
            }

            entitiesById[deviceId] = entity with
            {
                DriverProviderName = driver["DriverProviderName"]?.ToString(),
                DriverDate = TryReadDateTime(driver["DriverDate"]),
                DriverVersion = driver["DriverVersion"]?.ToString(),
                DriverName = driver["DriverName"]?.ToString(),
                InfName = driver["InfName"]?.ToString(),
            };
        }

        return entitiesById.Values
            .OrderBy(scanner => scanner.ProductId)
            .ThenBy(scanner => scanner.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool TryParseUsbIds(string pnpDeviceId, out int vendorId, out int productId)
    {
        vendorId = 0;
        productId = 0;

        var match = UsbIdPattern.Match(pnpDeviceId);
        if (!match.Success)
        {
            return false;
        }

        vendorId = Convert.ToInt32(match.Groups["vid"].Value, 16);
        productId = Convert.ToInt32(match.Groups["pid"].Value, 16);
        return true;
    }

    private static string[] ReadStringArray(object? value)
    {
        return value switch
        {
            null => [],
            string[] strings => strings,
            object[] objects => objects.Select(static item => item?.ToString())
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray(),
            _ => [value.ToString() ?? string.Empty],
        };
    }

    private static DateTime? TryReadDateTime(object? value)
    {
        if (value is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return ManagementDateTimeConverter.ToDateTime(raw);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static int? TryReadInt32(object? value)
    {
        return value switch
        {
            null => null,
            int intValue => intValue,
            uint uintValue when uintValue <= int.MaxValue => (int)uintValue,
            short shortValue => shortValue,
            ushort ushortValue => ushortValue,
            byte byteValue => byteValue,
            sbyte sbyteValue => sbyteValue,
            long longValue when longValue is >= int.MinValue and <= int.MaxValue => (int)longValue,
            ulong ulongValue when ulongValue <= int.MaxValue => (int)ulongValue,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : null,
        };
    }

    [GeneratedRegex(@"VID_(?<vid>[0-9A-Fa-f]{4})&PID_(?<pid>[0-9A-Fa-f]{4})", RegexOptions.Compiled)]
    private static partial Regex UsbIdRegex();
}
