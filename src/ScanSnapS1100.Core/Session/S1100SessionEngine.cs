using System.Buffers.Binary;
using System.Text;
using ScanSnapS1100.Core.Firmware;
using ScanSnapS1100.Core.Transport;

namespace ScanSnapS1100.Core.Protocol;

public sealed class S1100SessionEngine
{
    private const byte Ack = 0x06;
    private static readonly byte[] PaperIngestPayload = [0x01];
    private static readonly byte[] PaperEjectPayload = [0x00];
    private static readonly byte[] ResetButtonPayload = [0x80];

    public async ValueTask<EpjitsuStatusFlags> GetStatusAsync(
        IScannerTransport transport,
        CancellationToken cancellationToken = default)
    {
        await WriteCommandAsync(transport, EpjitsuCommandCode.GetStatus, cancellationToken).ConfigureAwait(false);
        var raw = await ReadExactAsync(transport, 2, cancellationToken).ConfigureAwait(false);
        return new EpjitsuStatusFlags(raw[0]);
    }

    public async ValueTask<S1100Identifiers> GetIdentifiersAsync(
        IScannerTransport transport,
        CancellationToken cancellationToken = default)
    {
        await WriteCommandAsync(transport, EpjitsuCommandCode.GetIdentifiers, cancellationToken).ConfigureAwait(false);
        var raw = await ReadExactAsync(transport, 0x20, cancellationToken).ConfigureAwait(false);

        var text = Encoding.ASCII.GetString(raw);
        var manufacturer = text[..8].Trim();
        var product = text[8..32].Trim();

        return new S1100Identifiers(manufacturer, product);
    }

    public async ValueTask<EpjitsuSensorFlags> GetSensorFlagsAsync(
        IScannerTransport transport,
        CancellationToken cancellationToken = default)
    {
        await WriteCommandAsync(transport, EpjitsuCommandCode.GetSensorFlags, cancellationToken).ConfigureAwait(false);
        var raw = await ReadExactAsync(transport, 4, cancellationToken).ConfigureAwait(false);
        return new EpjitsuSensorFlags(BinaryPrimitives.ReadUInt32LittleEndian(raw));
    }

    public async ValueTask UploadFirmwareAsync(
        IScannerTransport transport,
        NalFirmwareImage firmware,
        CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(transport, cancellationToken).ConfigureAwait(false);
        if (status.FirmwareLoaded)
        {
            return;
        }

        await ExpectAckAsync(transport, EpjitsuCommandCode.LoadFirmware, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(firmware.UploadLengthPrefixBytes, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(firmware.Payload, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(new byte[] { firmware.ComputePayloadChecksum() }, cancellationToken).ConfigureAwait(false);
        await ExpectSingleByteAsync(transport, Ack, cancellationToken).ConfigureAwait(false);

        await ExpectAckAsync(transport, EpjitsuCommandCode.ReinitializeFirmware, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(ResetButtonPayload, cancellationToken).ConfigureAwait(false);
        await ExpectSingleByteAsync(transport, Ack, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetLampAsync(
        IScannerTransport transport,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await ExpectAckAsync(transport, EpjitsuCommandCode.SetLamp, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(new byte[] { enabled ? (byte)0x01 : (byte)0x00 }, cancellationToken).ConfigureAwait(false);
        await ExpectSingleByteAsync(transport, Ack, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetWindowAsync(
        IScannerTransport transport,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        await ExpectAckAsync(transport, EpjitsuCommandCode.SetWindow, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await ExpectSingleByteAsync(transport, Ack, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetCoarseCalibrationAsync(
        IScannerTransport transport,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        await ExpectAckAsync(transport, EpjitsuCommandCode.SetCoarseCalibration, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await ExpectSingleByteAsync(transport, Ack, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SetFineCalibrationAsync(
        IScannerTransport transport,
        EpjitsuCommandCode commandCode,
        ReadOnlyMemory<byte> header,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        await ExpectAckAsync(transport, commandCode, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await ExpectSingleByteAsync(transport, Ack, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask PositionPaperAsync(
        IScannerTransport transport,
        bool ingest,
        CancellationToken cancellationToken = default)
    {
        await ExpectAckAsync(transport, EpjitsuCommandCode.SetPaperFeed, cancellationToken).ConfigureAwait(false);
        await transport.WriteAsync(ingest ? PaperIngestPayload : PaperEjectPayload, cancellationToken).ConfigureAwait(false);
        await ExpectSingleByteAsync(transport, Ack, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask StartScanAsync(
        IScannerTransport transport,
        CancellationToken cancellationToken = default)
    {
        await ExpectAckAsync(transport, EpjitsuCommandCode.StartScan, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ResetButtonAsync(
        IScannerTransport transport,
        CancellationToken cancellationToken = default)
    {
        await ExpectAckAsync(transport, EpjitsuCommandCode.ResetButton, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ExpectAckAsync(
        IScannerTransport transport,
        EpjitsuCommandCode commandCode,
        CancellationToken cancellationToken)
    {
        await WriteCommandAsync(transport, commandCode, cancellationToken).ConfigureAwait(false);
        await ExpectSingleByteAsync(transport, Ack, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask WriteCommandAsync(
        IScannerTransport transport,
        EpjitsuCommandCode commandCode,
        CancellationToken cancellationToken)
    {
        await transport.WriteAsync(new byte[] { 0x1B, (byte)commandCode }, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask ExpectSingleByteAsync(
        IScannerTransport transport,
        byte expected,
        CancellationToken cancellationToken)
    {
        var raw = await ReadExactAsync(transport, 1, cancellationToken).ConfigureAwait(false);
        if (raw[0] != expected)
        {
            throw new IOException($"Unexpected scanner response byte 0x{raw[0]:X2}. Expected 0x{expected:X2}.");
        }
    }

    private static async ValueTask<byte[]> ReadExactAsync(
        IScannerTransport transport,
        int count,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var totalRead = 0;

        while (totalRead < count)
        {
            var bytesRead = await transport.ReadAsync(buffer.AsMemory(totalRead), cancellationToken).ConfigureAwait(false);
            if (bytesRead <= 0)
            {
                throw new EndOfStreamException($"Expected {count} bytes from the scanner, received {totalRead}.");
            }

            totalRead += bytesRead;
        }

        return buffer;
    }
}
