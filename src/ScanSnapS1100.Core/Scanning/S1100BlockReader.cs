using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Core.Transport;

namespace ScanSnapS1100.Core.Scanning;

internal static class S1100BlockReader
{
    private const int BlockTrailerBytes = 8;
    private const int MaxReadChunk = 64 * 1024;

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
            var bytesRead = await transport.ReadAsync(buffer.AsMemory(totalRead, readSize), cancellationToken).ConfigureAwait(false);
            if (bytesRead <= 0)
            {
                break;
            }

            totalRead += bytesRead;

            // The scanner can terminate the final block with a short read that already includes the 8-byte trailer.
            // Treat that as the end of the block instead of waiting for bytes that will never arrive.
            if (bytesRead < readSize)
            {
                break;
            }
        }

        if (totalRead <= 0)
        {
            return [];
        }

        if (totalRead < BlockTrailerBytes)
        {
            throw new EndOfStreamException(
                $"Expected at least {BlockTrailerBytes} bytes from the scanner block trailer, received {totalRead}.");
        }

        var payloadBytes = totalRead >= totalBytesToRead
            ? totalRead - BlockTrailerBytes
            : totalRead - BlockTrailerBytes;

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
