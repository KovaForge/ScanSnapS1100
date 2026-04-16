using System.Globalization;
using System.Text;
using ScanSnapS1100.Core.Firmware;
using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Windows.DeviceDiscovery;

return await ProgramEntry.RunAsync(args).ConfigureAwait(false);

internal static class ProgramEntry
{
    public static Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return Task.FromResult(1);
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "devices":
                    return Task.FromResult(HandleDevices(args));
                case "firmware":
                    return Task.FromResult(HandleFirmware(args));
                case "profiles":
                    return Task.FromResult(HandleProfiles(args));
                case "flags":
                    return Task.FromResult(HandleFlags(args));
                case "help":
                case "--help":
                case "-h":
                    PrintUsage();
                    return Task.FromResult(0);
                default:
                    Console.Error.WriteLine($"Unknown command '{args[0]}'.");
                    PrintUsage();
                    return Task.FromResult(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Task.FromResult(1);
        }
    }

    private static int HandleDevices(string[] args)
    {
        if (args.Length < 2)
        {
            throw new InvalidOperationException("Usage: devices list | devices inspect");
        }

        var devices = WindowsScanSnapDiscovery.FindSupportedDevices();
        if (devices.Count == 0)
        {
            Console.WriteLine("No supported ScanSnap S1100/S1100i devices found.");
            return 0;
        }

        switch (args[1].ToLowerInvariant())
        {
            case "list":
                foreach (var device in devices)
                {
                    Console.WriteLine(device.Name);
                    Console.WriteLine($"  PNP ID:     {device.PnpDeviceId}");
                    Console.WriteLine($"  VID/PID:    {device.VendorId:X4}:{device.ProductId:X4}");
                    Console.WriteLine($"  Service:    {device.Service ?? "(unknown)"}");
                    Console.WriteLine($"  Driver:     {device.DriverVersion ?? "(unknown)"}");
                    Console.WriteLine($"  INF:        {device.InfName ?? "(unknown)"}");
                    Console.WriteLine($"  Status:     {device.Status ?? "(unknown)"}");
                }

                return 0;

            case "inspect":
                foreach (var device in devices)
                {
                    PrintDeviceInspection(device);
                }

                return 0;

            default:
                throw new InvalidOperationException("Usage: devices list | devices inspect");
        }
    }

    private static int HandleFirmware(string[] args)
    {
        if (args.Length < 3 || !args[1].Equals("inspect", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Usage: firmware inspect <path>");
        }

        var image = NalFirmwareImage.FromFile(args[2]);
        Console.WriteLine($"Path:                {Path.GetFullPath(args[2])}");
        Console.WriteLine($"Header bytes:        {image.Header.Length}");
        Console.WriteLine($"Payload bytes:       {image.Payload.Length}");
        Console.WriteLine($"Trailer byte:        0x{image.Trailer:X2}");
        Console.WriteLine($"Upload length:       {image.UploadLengthPrefix}");
        Console.WriteLine($"Upload length bytes: {FormatHex(image.UploadLengthPrefixBytes)}");
        Console.WriteLine($"Payload checksum:    0x{image.ComputePayloadChecksum():X2}");
        Console.WriteLine($"Header preview:      {FormatHex(image.Header.AsSpan(0, 16).ToArray())}");
        Console.WriteLine($"Payload preview:     {FormatHex(image.Payload.AsSpan(0, 16).ToArray())}");
        return 0;
    }

    private static int HandleProfiles(string[] args)
    {
        if (args.Length < 2)
        {
            throw new InvalidOperationException("Usage: profiles list | profiles show <300|600>");
        }

        switch (args[1].ToLowerInvariant())
        {
            case "list":
                foreach (var profile in S1100Profiles.All)
                {
                    Console.WriteLine($"{profile.Dpi} dpi");
                }

                return 0;

            case "show":
                if (args.Length < 3 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dpi))
                {
                    throw new InvalidOperationException("Usage: profiles show <300|600>");
                }

                var profileToShow = S1100Profiles.GetForDpi(dpi);
                PrintProfile(profileToShow);
                return 0;

            default:
                throw new InvalidOperationException("Usage: profiles list | profiles show <300|600>");
        }
    }

    private static int HandleFlags(string[] args)
    {
        if (args.Length < 3)
        {
            throw new InvalidOperationException("Usage: flags status <hex-byte> | flags sensor <hex-dword>");
        }

        switch (args[1].ToLowerInvariant())
        {
            case "status":
                var rawStatus = ParseHexByte(args[2]);
                Console.WriteLine(new EpjitsuStatusFlags(rawStatus));
                return 0;

            case "sensor":
                var rawSensor = ParseHexUInt32(args[2]);
                Console.WriteLine(new EpjitsuSensorFlags(rawSensor));
                return 0;

            default:
                throw new InvalidOperationException("Usage: flags status <hex-byte> | flags sensor <hex-dword>");
        }
    }

    private static void PrintProfile(S1100Profile profile)
    {
        Console.WriteLine($"Profile: {profile.Dpi} dpi");
        Console.WriteLine($"  Coarse calibration bytes:   {profile.CoarseCalibrationData.Length}");
        Console.WriteLine($"  SetWindow coarse bytes:     {profile.SetWindowCoarseCalibration.Length}");
        Console.WriteLine($"  SetWindow fine bytes:       {profile.SetWindowFineCalibration.Length}");
        Console.WriteLine($"  SetWindow send-cal bytes:   {profile.SetWindowSendCalibration.Length}");
        Console.WriteLine($"  SendCal #1 header bytes:    {profile.SendCalibrationHeader1.Length}");
        Console.WriteLine($"  SendCal #2 header bytes:    {profile.SendCalibrationHeader2.Length}");
        Console.WriteLine($"  SetWindow scan bytes:       {profile.SetWindowScan.Length}");
        Console.WriteLine();
        Console.WriteLine($"SetWindow scan: {FormatHexMultiline(profile.SetWindowScan)}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("ScanSnapS1100.Tool");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  devices list");
        Console.WriteLine("  devices inspect");
        Console.WriteLine("  firmware inspect <path>");
        Console.WriteLine("  profiles list");
        Console.WriteLine("  profiles show <300|600>");
        Console.WriteLine("  flags status <hex-byte>");
        Console.WriteLine("  flags sensor <hex-dword>");
    }

    private static byte ParseHexByte(string input)
    {
        var normalized = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input[2..] : input;
        return byte.Parse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static uint ParseHexUInt32(string input)
    {
        var normalized = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input[2..] : input;
        return uint.Parse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static string FormatHex(byte[] bytes)
    {
        return string.Join(' ', bytes.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static string FormatHexMultiline(byte[] bytes)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < bytes.Length; i += 16)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            var chunk = bytes.Skip(i).Take(16).ToArray();
            builder.Append("  ");
            builder.Append(FormatHex(chunk));
        }

        return builder.ToString();
    }

    private static void PrintDeviceInspection(WindowsAttachedScanner device)
    {
        Console.WriteLine(device.Name);
        Console.WriteLine($"  Manufacturer:       {device.Manufacturer}");
        Console.WriteLine($"  PNP ID:             {device.PnpDeviceId}");
        Console.WriteLine($"  VID/PID:            {device.VendorId:X4}:{device.ProductId:X4}");
        Console.WriteLine($"  PNP class:          {device.PnpClass ?? "(unknown)"}");
        Console.WriteLine($"  Class GUID:         {device.ClassGuid ?? "(unknown)"}");
        Console.WriteLine($"  Status:             {device.Status ?? "(unknown)"}");
        Console.WriteLine($"  CM error code:      {(device.ConfigManagerErrorCode?.ToString(CultureInfo.InvariantCulture) ?? "(unknown)")}");
        Console.WriteLine($"  Service:            {device.Service ?? "(unknown)"}");
        Console.WriteLine($"  Driver provider:    {device.DriverProviderName ?? "(unknown)"}");
        Console.WriteLine($"  Driver version:     {device.DriverVersion ?? "(unknown)"}");
        Console.WriteLine($"  Driver date:        {(device.DriverDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "(unknown)")}");
        Console.WriteLine($"  Driver name:        {device.DriverName ?? "(unknown)"}");
        Console.WriteLine($"  INF:                {device.InfName ?? "(unknown)"}");
        PrintLabeledValues("Hardware IDs", device.HardwareIds);
        PrintLabeledValues("Compatible IDs", device.CompatibleIds);
    }

    private static void PrintLabeledValues(string label, string[] values)
    {
        if (values.Length == 0)
        {
            Console.WriteLine($"  {label,-18} (none)");
            return;
        }

        if (values.Length == 1)
        {
            Console.WriteLine($"  {label,-18} {values[0]}");
            return;
        }

        Console.WriteLine($"  {label,-18}");
        foreach (var value in values)
        {
            Console.WriteLine($"    {value}");
        }
    }
}
