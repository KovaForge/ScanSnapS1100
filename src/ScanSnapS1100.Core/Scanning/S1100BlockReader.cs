using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Core.Transport;

namespace ScanSnapS1100.Core.Scanning;

internal static class S1100BlockReader
{
    private const int BlockTrailerBytes = 8;
    private const int MaxReadChunk = 64 * 1024;
    private static readonly TimeSpan ReadIdleTimeout = TimeSpan.FromMilliseconds(750);

    public static async ValueTask<byte[]> ReadAsync(
        IScannerTransport transport,
        S1100Profile profile,
        int requestedLines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(profile);

        var rawBytes = checked(requestedLines * profile.LineStride);
        var totalBytesToRead = checked(rawBytes + BlockTrailerBytes);
        var buffer = new byte[totalBytesToRead];
        var totalRead = 0;

        while (totalRead < totalBytesToRead)
        {
            var readSize = Math.Min(MaxReadChunk, totalBytesToRead - totalRead);
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTimeout.CancelAfter(ReadIdleTimeout);

            int bytesRead;
            try
            {
                bytesRead = await transport.ReadAsync(buffer.AsMemory(totalRead, readSize), readTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (bytesRead <= 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        if (totalRead <= 0)
        {
            return [];
        }

        int payloadBytes;
        if (totalRead >= totalBytesToRead)
        {
            payloadBytes = rawBytes;
        }
        else if (totalRead > rawBytes)
        {
            // Accept a partial trailer after the full payload arrives.
            payloadBytes = rawBytes;
        }
        else if (totalRead >= BlockTrailerBytes && (totalRead - BlockTrailerBytes) % profile.LineStride == 0)
        {
            // Short final block with a complete trailer.
            payloadBytes = totalRead - BlockTrailerBytes;
        }
        else if (totalRead % profile.LineStride == 0)
        {
            // Some Windows usbscan reads appear to stop after the payload without delivering the trailer.
            payloadBytes = totalRead;
        }
        else
        {
            throw new EndOfStreamException(
                $"Scanner block ended with {totalRead} bytes, which does not align to payload stride {profile.LineStride} or payload-plus-trailer framing.");
        }

        if (payloadBytes > rawBytes)
        {
            payloadBytes = rawBytes;
        }

        if (payloadBytes % profile.LineStride != 0)
        {
            throw new IOException(
                $"Scanner block payload was {payloadBytes} bytes, which is not aligned to the expected S1100 line stride of {profile.LineStride}.");
        }

        var payload = new byte[payloadBytes];
        Array.Copy(buffer, payload, payloadBytes);
        return payload;
    }
}
