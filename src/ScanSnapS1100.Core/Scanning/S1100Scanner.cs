using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Core.Transport;

namespace ScanSnapS1100.Core.Scanning;

public sealed class S1100Scanner
{
    private readonly S1100SessionEngine _session;

    public S1100Scanner(S1100SessionEngine? session = null)
    {
        _session = session ?? new S1100SessionEngine();
    }

    public async ValueTask<S1100ScanResult> ScanColorAsync(
        IScannerTransport transport,
        S1100ScanSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(settings);

        var profile = S1100Profiles.GetForDpi(settings.Dpi);
        var geometry = S1100ScanGeometry.FromProfile(profile, settings.PageWidthInches, settings.PageHeightInches);
        var pageAssembler = new S1100PageAssembler(geometry);
        var scanStatuses = new List<S1100ScanStatus>();

        var expectedRawLines = geometry.RawHeightPixels;
        var rawLinesReceived = 0;

        await _session.SetWindowAsync(transport, profile.SetWindowCoarseCalibration, cancellationToken).ConfigureAwait(false);
        await _session.SetCoarseCalibrationAsync(transport, profile.CoarseCalibrationData, cancellationToken).ConfigureAwait(false);
        await _session.SendIdentityLutAsync(transport, cancellationToken).ConfigureAwait(false);
        await _session.SetLampAsync(transport, enabled: true, cancellationToken).ConfigureAwait(false);
        await _session.SetWindowAsync(transport, profile.SetWindowScan, cancellationToken).ConfigureAwait(false);

        if (settings.IngestPaper)
        {
            await _session.PositionPaperAsync(transport, ingest: true, cancellationToken).ConfigureAwait(false);
        }

        await _session.StartScanAsync(transport, cancellationToken).ConfigureAwait(false);

        try
        {
            while (rawLinesReceived < expectedRawLines)
            {
                var blockLines = Math.Min(profile.BlockHeight, expectedRawLines - rawLinesReceived);

                await _session.RequestNextScanBlockAsync(transport, cancellationToken).ConfigureAwait(false);
                var rawBlock = await S1100BlockReader.ReadAsync(transport, profile, blockLines, cancellationToken).ConfigureAwait(false);
                if (rawBlock.Length == 0)
                {
                    break;
                }

                var actualRawLines = rawBlock.Length / profile.LineStride;
                if (actualRawLines <= 0)
                {
                    throw new IOException("The scanner returned a partial block that does not align to the S1100 raw line stride.");
                }

                var descrambled = S1100ImageDescrambler.DescrambleColorBlock(rawBlock, profile, actualRawLines);
                pageAssembler.AppendBlock(descrambled, actualRawLines, rawLinesReceived);
                rawLinesReceived += actualRawLines;

                var scanStatus = await _session.GetScanStatusAsync(transport, cancellationToken).ConfigureAwait(false);
                scanStatuses.Add(scanStatus);
                pageAssembler.ApplyReportedRawHeight(scanStatus.ReportedRawHeightLines);

                if (scanStatus.ReportedRawHeightLines is int reportedRawHeight && reportedRawHeight > 0)
                {
                    expectedRawLines = Math.Min(expectedRawLines, RoundUpToBlockHeight(reportedRawHeight, profile.BlockHeight));
                }

                if (actualRawLines < blockLines)
                {
                    break;
                }
            }
        }
        finally
        {
            if (settings.EjectPaper)
            {
                await TryBestEffortAsync(
                        () => _session.PositionPaperAsync(transport, ingest: false, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (settings.ResetButtonWhenDone)
            {
                await TryBestEffortAsync(
                        () => _session.ResetButtonAsync(transport, cancellationToken),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var page = pageAssembler.ToCapturedPage(settings.Dpi);
        if (page.HeightPixels <= 0 || page.PixelData.Length == 0)
        {
            throw new IOException("The S1100 scan pipeline completed without any captured pixel data.");
        }

        return new S1100ScanResult(page, scanStatuses, rawLinesReceived);
    }

    private static int RoundUpToBlockHeight(int rawHeightLines, int blockHeight)
    {
        var remainder = rawHeightLines % blockHeight;
        return remainder == 0 ? rawHeightLines : rawHeightLines + (blockHeight - remainder);
    }

    private static async ValueTask TryBestEffortAsync(
        Func<ValueTask> callback,
        CancellationToken cancellationToken)
    {
        try
        {
            await callback().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
        }
    }
}
