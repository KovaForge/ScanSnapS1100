using ScanSnapS1100.Core.Diagnostics;
using ScanSnapS1100.Core.Scanning;
using ScanSnapS1100.Core.Transport;
using ScanSnapS1100.Windows.DeviceDiscovery;
using ScanSnapS1100.Windows.Transport;

namespace ScanSnapS1100.Windows.Imaging;

public sealed record WindowsScanCaptureResult(
    string DeviceName,
    string InterfacePath,
    string OutputPath,
    string? TracePath,
    int WidthPixels,
    int HeightPixels,
    int Dpi);

public static class WindowsScanSnapImageCapture
{
    public static async Task<WindowsScanCaptureResult> ScanToPpmAsync(
        int dpi,
        string outputPath,
        string? tracePath = null,
        CancellationToken cancellationToken = default)
    {
        var devices = WindowsScanSnapDiscovery.FindSupportedDevices();
        var scanner = devices.FirstOrDefault(static candidate => candidate.InterfacePaths.Length > 0);
        if (scanner is null)
        {
            throw new InvalidOperationException("No image-class interface path was discovered for the attached S1100/S1100i device.");
        }

        await using var transport = WindowsUsbScannerTransport.Open(scanner.InterfacePaths[0]);

        IScannerTransport effectiveTransport = transport;
        TransportTrace? trace = null;
        if (!string.IsNullOrWhiteSpace(tracePath))
        {
            trace = new TransportTrace();
            effectiveTransport = new RecordingScannerTransport(transport, trace);
        }

        try
        {
            var scanSettings = new S1100ScanSettings(dpi);
            var result = await new S1100Scanner().ScanColorAsync(effectiveTransport, scanSettings, cancellationToken).ConfigureAwait(false);
            await PortablePixmapWriter.WriteAsync(result.Page, outputPath, cancellationToken).ConfigureAwait(false);

            return new WindowsScanCaptureResult(
                DeviceName: scanner.Name,
                InterfacePath: scanner.InterfacePaths[0],
                OutputPath: Path.GetFullPath(outputPath),
                TracePath: string.IsNullOrWhiteSpace(tracePath) ? null : Path.GetFullPath(tracePath),
                WidthPixels: result.Page.WidthPixels,
                HeightPixels: result.Page.HeightPixels,
                Dpi: dpi);
        }
        finally
        {
            if (trace is not null)
            {
                await trace.WriteJsonAsync(tracePath!, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
