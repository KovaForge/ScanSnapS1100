using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Windows.DeviceDiscovery;
using ScanSnapS1100.Windows.Transport;

namespace ScanSnapS1100.Windows.Baseline;

public sealed record BaselineExportResult(
    string ExportDirectory,
    string ManifestPath,
    bool TransportProbeSucceeded,
    string? TransportProbeSummary);

public static class BaselineExporter
{
    private static readonly string[] LiveSystemFileNames =
    [
        "1100_0B00.nal",
        "1100i_0000.nal",
        "s1100u-x64.dll",
        "ippi5s1100-x64.dll",
        "ijl5s1100-x64.dll",
    ];

    public static async Task<BaselineExportResult> ExportAsync(
        string exportDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);

        var exportRoot = Path.GetFullPath(exportDirectory);
        Directory.CreateDirectory(exportRoot);

        var devices = WindowsScanSnapDiscovery.FindSupportedDevices().ToArray();
        var commandCaptures = new List<CapturedCommand>();

        var driverEnum = await RunProcessAsync("pnputil", ["/enum-drivers", "/class", "Image"], cancellationToken).ConfigureAwait(false);
        var driverEnumPath = Path.Combine(exportRoot, "pnputil-enum-drivers.txt");
        await File.WriteAllTextAsync(driverEnumPath, driverEnum.CombinedOutput, cancellationToken).ConfigureAwait(false);
        commandCaptures.Add(new CapturedCommand("pnputil enum-drivers", driverEnum.ExitCode, Path.GetFileName(driverEnumPath)));

        foreach (var device in devices)
        {
            var deviceDriverReport = await RunProcessAsync(
                    "pnputil",
                    ["/enum-devices", "/instanceid", device.PnpDeviceId, "/drivers"],
                    cancellationToken)
                .ConfigureAwait(false);

            var safeFileName = SanitizeFileName(device.Name);
            var reportPath = Path.Combine(exportRoot, $"pnputil-device-drivers-{safeFileName}.txt");
            await File.WriteAllTextAsync(reportPath, deviceDriverReport.CombinedOutput, cancellationToken).ConfigureAwait(false);
            commandCaptures.Add(new CapturedCommand($"pnputil enum-devices {device.PnpDeviceId}", deviceDriverReport.ExitCode, Path.GetFileName(reportPath)));
        }

        var driverStoreArtifacts = new List<ExportedArtifact>();
        var exportedInfs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var infName in devices
                     .Select(static device => device.InfName)
                     .Where(static infName => !string.IsNullOrWhiteSpace(infName))
                     .Cast<string>())
        {
            if (!exportedInfs.Add(infName))
            {
                continue;
            }

            var targetDirectory = Path.Combine(exportRoot, "driver-store", infName);
            Directory.CreateDirectory(targetDirectory);

            var exportResult = await RunProcessAsync(
                    "pnputil",
                    ["/export-driver", infName, targetDirectory],
                    cancellationToken)
                .ConfigureAwait(false);

            var exportLogPath = Path.Combine(exportRoot, $"pnputil-export-{infName}.txt");
            await File.WriteAllTextAsync(exportLogPath, exportResult.CombinedOutput, cancellationToken).ConfigureAwait(false);
            commandCaptures.Add(new CapturedCommand($"pnputil export-driver {infName}", exportResult.ExitCode, Path.GetFileName(exportLogPath)));

            driverStoreArtifacts.AddRange(EnumerateArtifacts(targetDirectory, exportRoot));
        }

        var liveSystemDirectory = Path.Combine(exportRoot, "live-system32");
        Directory.CreateDirectory(liveSystemDirectory);
        var liveArtifacts = new List<ExportedArtifact>();
        var systemDirectory = Environment.SystemDirectory;

        foreach (var fileName in LiveSystemFileNames)
        {
            var sourcePath = Path.Combine(systemDirectory, fileName);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(liveSystemDirectory, fileName);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            liveArtifacts.Add(CreateArtifact(destinationPath, exportRoot));
        }

        var transportProbe = await ProbeRawTransportAsync(devices, cancellationToken).ConfigureAwait(false);

        var snapshot = new BaselineSnapshot
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            MachineName = Environment.MachineName,
            OsDescription = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Devices = devices,
            DriverStoreArtifacts = driverStoreArtifacts.OrderBy(static artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            LiveSystemArtifacts = liveArtifacts.OrderBy(static artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            CapturedCommands = commandCaptures.ToArray(),
            RawTransportProbe = transportProbe,
        };

        var manifestPath = Path.Combine(exportRoot, "baseline-manifest.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        await File.WriteAllTextAsync(manifestPath, json, cancellationToken).ConfigureAwait(false);

        return new BaselineExportResult(
            ExportDirectory: exportRoot,
            ManifestPath: manifestPath,
            TransportProbeSucceeded: transportProbe.Succeeded,
            TransportProbeSummary: transportProbe.Succeeded ? transportProbe.Status : transportProbe.Error);
    }

    private static async Task<RawTransportProbe> ProbeRawTransportAsync(
        IReadOnlyList<WindowsAttachedScanner> devices,
        CancellationToken cancellationToken)
    {
        var scanner = devices.FirstOrDefault(static candidate => candidate.InterfacePaths.Length > 0);
        if (scanner is null)
        {
            return new RawTransportProbe
            {
                Succeeded = false,
                Error = "No image-class device interface path was discovered for the attached S1100/S1100i device.",
            };
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            await using var transport = WindowsUsbScannerTransport.Open(scanner.InterfacePaths[0]);
            var session = new S1100SessionEngine();
            var status = await session.GetStatusAsync(transport, timeout.Token).ConfigureAwait(false);
            var identifiers = await session.GetIdentifiersAsync(transport, timeout.Token).ConfigureAwait(false);
            var sensors = await session.GetSensorFlagsAsync(transport, timeout.Token).ConfigureAwait(false);

            return new RawTransportProbe
            {
                Succeeded = true,
                DeviceName = scanner.Name,
                InterfacePath = scanner.InterfacePaths[0],
                Status = status.ToString(),
                Identifiers = $"{identifiers.Manufacturer} {identifiers.ProductName}".Trim(),
                Sensors = sensors.ToString(),
            };
        }
        catch (OperationCanceledException)
        {
            return new RawTransportProbe
            {
                Succeeded = false,
                DeviceName = scanner.Name,
                InterfacePath = scanner.InterfacePaths[0],
                Error = "The raw transport probe timed out while waiting for the scanner to answer a status request.",
            };
        }
        catch (Exception ex)
        {
            return new RawTransportProbe
            {
                Succeeded = false,
                DeviceName = scanner.Name,
                InterfacePath = scanner.InterfacePaths[0],
                Error = ex.ToString(),
            };
        }
    }

    private static IEnumerable<ExportedArtifact> EnumerateArtifacts(string rootDirectory, string exportRoot)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => CreateArtifact(path, exportRoot))
            .ToArray();
    }

    private static ExportedArtifact CreateArtifact(string fullPath, string exportRoot)
    {
        using var stream = File.OpenRead(fullPath);
        var hash = SHA256.HashData(stream);
        var relativePath = Path.GetRelativePath(exportRoot, fullPath);

        return new ExportedArtifact
        {
            RelativePath = relativePath,
            SourcePath = fullPath,
            Size = stream.Length,
            Sha256 = Convert.ToHexString(hash),
        };
    }

    private static async Task<ProcessCaptureResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var combinedOutput = string.IsNullOrWhiteSpace(stderr)
            ? stdout
            : $"{stdout}{Environment.NewLine}{stderr}".Trim();

        return new ProcessCaptureResult(process.ExitCode, combinedOutput);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "scanner" : sanitized;
    }
}

public sealed class BaselineSnapshot
{
    public required DateTimeOffset CapturedAtUtc { get; init; }

    public required string MachineName { get; init; }

    public required string OsDescription { get; init; }

    public required string ProcessArchitecture { get; init; }

    public required WindowsAttachedScanner[] Devices { get; init; }

    public required ExportedArtifact[] DriverStoreArtifacts { get; init; }

    public required ExportedArtifact[] LiveSystemArtifacts { get; init; }

    public required CapturedCommand[] CapturedCommands { get; init; }

    public required RawTransportProbe RawTransportProbe { get; init; }
}

public sealed class ExportedArtifact
{
    public required string RelativePath { get; init; }

    public required string SourcePath { get; init; }

    public required long Size { get; init; }

    public required string Sha256 { get; init; }
}

public sealed class CapturedCommand
{
    public CapturedCommand(string command, int exitCode, string outputFile)
    {
        Command = command;
        ExitCode = exitCode;
        OutputFile = outputFile;
    }

    public string Command { get; }

    public int ExitCode { get; }

    public string OutputFile { get; }
}

public sealed class RawTransportProbe
{
    public bool Succeeded { get; init; }

    public string? DeviceName { get; init; }

    public string? InterfacePath { get; init; }

    public string? Status { get; init; }

    public string? Identifiers { get; init; }

    public string? Sensors { get; init; }

    public string? Error { get; init; }
}

internal sealed record ProcessCaptureResult(int ExitCode, string CombinedOutput);
