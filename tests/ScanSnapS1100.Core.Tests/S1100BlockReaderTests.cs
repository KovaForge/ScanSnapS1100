using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Core.Scanning;
using ScanSnapS1100.Core.Transport;

namespace ScanSnapS1100.Core.Tests;

public sealed class S1100BlockReaderTests
{
    [Fact]
    public async Task ReadAsyncReturnsFullPayloadWhenScannerReturnsTheWholeBlock()
    {
        var profile = CreateTestProfile();
        byte[] block = [0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7];
        var transport = new ChunkedScannerTransport(block);

        var payload = await S1100BlockReader.ReadAsync(transport, profile, requestedLines: 2);

        Assert.Equal(12, payload.Length);
        Assert.Equal(block[..12], payload);
    }

    [Fact]
    public async Task ReadAsyncTreatsAShortReadAsTheEndOfTheFinalBlock()
    {
        var profile = CreateTestProfile();
        byte[] shortFinalRead = [0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7];
        var transport = new ChunkedScannerTransport(shortFinalRead);

        var payload = await S1100BlockReader.ReadAsync(transport, profile, requestedLines: 2);

        Assert.Equal(12, payload.Length);
        Assert.Equal(shortFinalRead[..12], payload);
    }

    [Fact]
    public async Task ReadAsyncAcceptsPayloadWithoutTrailer()
    {
        var profile = CreateTestProfile();
        byte[] payloadOnly = [0x30, 0x31, 0x32, 0x33, 0x34, 0x35];
        var transport = new ChunkedScannerTransport(payloadOnly);

        var payload = await S1100BlockReader.ReadAsync(transport, profile, requestedLines: 2);

        Assert.Equal(payloadOnly, payload);
    }

    [Fact]
    public async Task ReadAsyncAcceptsAPartialTrailerAfterAFullPayload()
    {
        var profile = CreateTestProfile();
        byte[] payloadWithPartialTrailer = [0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0xF0];
        var transport = new ChunkedScannerTransport(payloadWithPartialTrailer);

        var payload = await S1100BlockReader.ReadAsync(transport, profile, requestedLines: 2);

        Assert.Equal(payloadWithPartialTrailer[..12], payload);
    }

    private static S1100Profile CreateTestProfile()
    {
        return new S1100Profile(
            Dpi: 300,
            LineStride: 6,
            PlaneStride: 2,
            PlaneWidth: 2,
            BlockHeight: 2,
            CoarseCalibrationData: [],
            SetWindowCoarseCalibration: [],
            SetWindowFineCalibration: [],
            SetWindowSendCalibration: [],
            SendCalibrationHeader1: [],
            SendCalibrationHeader2: [],
            SetWindowScan: []);
    }

    private sealed class ChunkedScannerTransport : IScannerTransport
    {
        private readonly Queue<byte[]> _chunks;

        public ChunkedScannerTransport(params byte[][] chunks)
        {
            _chunks = new Queue<byte[]>(chunks);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_chunks.Count == 0)
            {
                return ValueTask.FromResult(0);
            }

            var chunk = _chunks.Dequeue();
            chunk.CopyTo(buffer);
            return ValueTask.FromResult(chunk.Length);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
