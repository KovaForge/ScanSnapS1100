using System.Globalization;
using System.Text;
using ScanSnapS1100.Core.Diagnostics;
using ScanSnapS1100.Core.Firmware;
using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Core.Scanning;
using ScanSnapS1100.Core.Transport;
using ScanSnapS1100.Windows.Baseline;
using ScanSnapS1100.Windows.DeviceDiscovery;
using ScanSnapS1100.Windows.Imaging;
using ScanSnapS1100.Windows.ProtocolVerification;
using ScanSnapS1100.Windows.Transport;

return await ProgramEntry.RunAsync(args).ConfigureAwait(false);

internal static class ProgramEntry
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        try
        {
            return await (args[0].ToLowerInvariant() switch
            {
                "devices" => Task.FromResult(HandleDevices(args)),
                "firmware" => Task.FromResult(HandleFirmware(args)),
                "profiles" => Task.FromResult(HandleProfiles(args)),
                "flags" => Task.FromResult(HandleFlags(args)),
                "transport" => HandleTransportAsync(args),
                "baseline" => HandleBaselineAsync(args),
                "verify" => Task.FromResult(HandleVerify(args)),
                "help" or "--help" or "-h" => Task.FromResult(0),
                _ => Task.FromException<int>(new InvalidOperationException($"Unknown command '{args[0]}'.")),
            }).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Unknown command", StringComparison.Ordinal))
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            if (args[0].Equals("help", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("--help", StringComparison.OrdinalIgnoreCase)
                || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
            }
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
                    Console.WriteLine($"  Interfaces: {device.InterfacePaths.Length}");
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
        Console.WriteLine("  transport interfaces");
        Console.WriteLine("  transport status");
        Console.WriteLine("  transport probe");
        Console.WriteLine("  transport trace-probe <output-json>");
        Console.WriteLine("  transport upload-firmware <path>");
        Console.WriteLine("  transport scan-color <300|600> <output-ppm> [trace-json]");
        Console.WriteLine("  baseline export [directory]");
        Console.WriteLine("  verify capture-tools");
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
        PrintLabeledValues("Interfaces", device.InterfacePaths);
    }

    private static async Task<int> HandleTransportAsync(string[] args)
    {
        if (args.Length < 2)
        {
            throw new InvalidOperationException(
                "Usage: transport interfaces | transport status | transport probe | transport trace-probe <output-json> | transport upload-firmware <path> | transport scan-color <300|600> <output-ppm> [trace-json]");
        }

        var devices = WindowsScanSnapDiscovery.FindSupportedDevices();
        if (devices.Count == 0)
        {
            Console.WriteLine("No supported ScanSnap S1100/S1100i devices found.");
            return 0;
        }

        switch (args[1].ToLowerInvariant())
        {
            case "interfaces":
                foreach (var device in devices)
                {
                    Console.WriteLine(device.Name);
                    if (device.InterfacePaths.Length == 0)
                    {
                        Console.WriteLine("  (no image-class interface paths discovered)");
                        continue;
                    }

                    foreach (var interfacePath in device.InterfacePaths)
                    {
                        Console.WriteLine($"  {interfacePath}");
                    }
                }

                return 0;

            case "status":
                var scanner = devices.FirstOrDefault(static candidate => candidate.InterfacePaths.Length > 0);
                if (scanner is null)
                {
                    throw new InvalidOperationException("No image-class interface path was discovered for the attached S1100/S1100i device.");
                }

                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                {
                    await using IScannerTransport transport = WindowsUsbScannerTransport.Open(scanner.InterfacePaths[0]);
                    var session = new S1100SessionEngine();
                    var status = await session.GetStatusAsync(transport, timeout.Token).ConfigureAwait(false);

                    Console.WriteLine($"Device:       {scanner.Name}");
                    Console.WriteLine($"Interface:    {scanner.InterfacePaths[0]}");
                    Console.WriteLine($"Raw status:   {status}");
                    Console.WriteLine($"Usb powered:  {status.UsbPower}");
                    Console.WriteLine($"Firmware:     {(status.FirmwareLoaded ? "loaded" : "not loaded")}");
                }

                return 0;

            case "probe":
                var probeScanner = devices.FirstOrDefault(static candidate => candidate.InterfacePaths.Length > 0);
                if (probeScanner is null)
                {
                    throw new InvalidOperationException("No image-class interface path was discovered for the attached S1100/S1100i device.");
                }

                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                {
                    await using IScannerTransport transport = WindowsUsbScannerTransport.Open(probeScanner.InterfacePaths[0]);
                    var session = new S1100SessionEngine();
                    var status = await session.GetStatusAsync(transport, timeout.Token).ConfigureAwait(false);
                    var identifiers = await session.GetIdentifiersAsync(transport, timeout.Token).ConfigureAwait(false);
                    var sensors = await session.GetSensorFlagsAsync(transport, timeout.Token).ConfigureAwait(false);

                    Console.WriteLine($"Device:       {probeScanner.Name}");
                    Console.WriteLine($"Interface:    {probeScanner.InterfacePaths[0]}");
                    Console.WriteLine($"Status:       {status}");
                    Console.WriteLine($"Identifiers:  {identifiers.Manufacturer} {identifiers.ProductName}".Trim());
                    Console.WriteLine($"Sensors:      {sensors}");
                }

                return 0;

            case "trace-probe":
                if (args.Length < 3)
                {
                    throw new InvalidOperationException("Usage: transport trace-probe <output-json>");
                }

                var tracedScanner = devices.FirstOrDefault(static candidate => candidate.InterfacePaths.Length > 0);
                if (tracedScanner is null)
                {
                    throw new InvalidOperationException("No image-class interface path was discovered for the attached S1100/S1100i device.");
                }

                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var trace = new TransportTrace();
                    await using IScannerTransport transport = new RecordingScannerTransport(
                        WindowsUsbScannerTransport.Open(tracedScanner.InterfacePaths[0]),
                        trace);

                    var session = new S1100SessionEngine();
                    var status = await session.GetStatusAsync(transport, timeout.Token).ConfigureAwait(false);
                    var identifiers = await session.GetIdentifiersAsync(transport, timeout.Token).ConfigureAwait(false);
                    var sensors = await session.GetSensorFlagsAsync(transport, timeout.Token).ConfigureAwait(false);

                    var tracePath = Path.GetFullPath(args[2]);
                    await trace.WriteJsonAsync(tracePath, timeout.Token).ConfigureAwait(false);

                    Console.WriteLine($"Device:       {tracedScanner.Name}");
                    Console.WriteLine($"Interface:    {tracedScanner.InterfacePaths[0]}");
                    Console.WriteLine($"Status:       {status}");
                    Console.WriteLine($"Identifiers:  {identifiers.Manufacturer} {identifiers.ProductName}".Trim());
                    Console.WriteLine($"Sensors:      {sensors}");
                    Console.WriteLine($"Trace:        {tracePath}");
                }

                return 0;

            case "upload-firmware":
                if (args.Length < 3)
                {
                    throw new InvalidOperationException("Usage: transport upload-firmware <path>");
                }

                var firmwareScanner = devices.FirstOrDefault(static candidate => candidate.InterfacePaths.Length > 0);
                if (firmwareScanner is null)
                {
                    throw new InvalidOperationException("No image-class interface path was discovered for the attached S1100/S1100i device.");
                }

                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    var firmware = NalFirmwareImage.FromFile(args[2]);
                    await using IScannerTransport transport = WindowsUsbScannerTransport.Open(firmwareScanner.InterfacePaths[0]);
                    var session = new S1100SessionEngine();
                    await session.UploadFirmwareAsync(transport, firmware, timeout.Token).ConfigureAwait(false);
                    var status = await session.GetStatusAsync(transport, timeout.Token).ConfigureAwait(false);

                    Console.WriteLine($"Device:       {firmwareScanner.Name}");
                    Console.WriteLine($"Interface:    {firmwareScanner.InterfacePaths[0]}");
                    Console.WriteLine($"Firmware:     {Path.GetFullPath(args[2])}");
                    Console.WriteLine($"Status:       {status}");
                }

                return 0;

            case "scan-color":
                if (args.Length < 4 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var scanDpi))
                {
                    throw new InvalidOperationException("Usage: transport scan-color <300|600> <output-ppm> [trace-json]");
                }

                using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
                {
                    var capture = await WindowsScanSnapImageCapture.ScanToPpmAsync(
                            scanDpi,
                            args[3],
                            args.Length >= 5 ? args[4] : null,
                            timeout.Token)
                        .ConfigureAwait(false);

                    Console.WriteLine($"Device:       {capture.DeviceName}");
                    Console.WriteLine($"Interface:    {capture.InterfacePath}");
                    Console.WriteLine($"Output:       {capture.OutputPath}");
                    Console.WriteLine($"Dimensions:   {capture.WidthPixels} x {capture.HeightPixels}");
                    Console.WriteLine($"DPI:          {capture.Dpi}");
                    if (!string.IsNullOrWhiteSpace(capture.TracePath))
                    {
                        Console.WriteLine($"Trace:        {capture.TracePath}");
                    }
                }

                return 0;

            default:
                throw new InvalidOperationException(
                    "Usage: transport interfaces | transport status | transport probe | transport trace-probe <output-json> | transport upload-firmware <path> | transport scan-color <300|600> <output-ppm> [trace-json]");
        }
    }

    private static async Task<int> HandleBaselineAsync(string[] args)
    {
        if (args.Length < 2 || !args[1].Equals("export", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Usage: baseline export [directory]");
        }

        var exportDirectory = args.Length >= 3
            ? Path.GetFullPath(args[2])
            : GetDefaultBaselineExportDirectory();

        var result = await BaselineExporter.ExportAsync(exportDirectory).ConfigureAwait(false);
        Console.WriteLine($"Export root:  {result.ExportDirectory}");
        Console.WriteLine($"Manifest:     {result.ManifestPath}");
        Console.WriteLine($"Probe:        {(result.TransportProbeSucceeded ? "succeeded" : "failed")}");
        Console.WriteLine($"Probe detail: {result.TransportProbeSummary ?? "(none)"}");
        return 0;
    }

    private static int HandleVerify(string[] args)
    {
        if (args.Length < 2)
        {
            throw new InvalidOperationException("Usage: verify capture-tools");
        }

        switch (args[1].ToLowerInvariant())
        {
            case "capture-tools":
                foreach (var tool in CaptureToolDiscovery.Inspect())
                {
                    Console.WriteLine(tool.Name);
                    Console.WriteLine($"  Available: {tool.Available}");
                    Console.WriteLine($"  Path:      {tool.Path ?? "(not found)"}");
                }

                return 0;

            default:
                throw new InvalidOperationException("Usage: verify capture-tools");
        }
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

    private static string GetDefaultBaselineExportDirectory()
    {
        return Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "baseline",
            $"snapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss}"));
    }
}
