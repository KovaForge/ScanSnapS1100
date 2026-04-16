using System.Text;
using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Core.Scanning;
using ScanSnapS1100.Core.Transport;

namespace ScanSnapS1100.Core.Tests;

public sealed class S1100SessionEngineTests
{
    [Fact]
    public async Task GetStatusWritesTheExpectedCommand()
    {
        var transport = new FakeScannerTransport([0x19, 0x00]);
        var session = new S1100SessionEngine();

        var status = await session.GetStatusAsync(transport);

        Assert.Equal(new byte[] { 0x1B, (byte)EpjitsuCommandCode.GetStatus }, transport.Written.ToArray());
        Assert.True(status.UsbPower);
        Assert.True(status.FirmwareLoaded);
    }

    [Fact]
    public async Task GetIdentifiersTrimsNullPaddingFromTheScannerResponse()
    {
        var raw = new byte[0x20];
        Encoding.ASCII.GetBytes("FUJITSU ").CopyTo(raw, 0);
        Encoding.ASCII.GetBytes("ScanSnap S1100  0B00").CopyTo(raw, 8);

        var transport = new FakeScannerTransport(raw);
        var session = new S1100SessionEngine();

        var identifiers = await session.GetIdentifiersAsync(transport);

        Assert.Equal(new byte[] { 0x1B, (byte)EpjitsuCommandCode.GetIdentifiers }, transport.Written.ToArray());
        Assert.Equal("FUJITSU", identifiers.Manufacturer);
        Assert.Equal("ScanSnap S1100  0B00", identifiers.ProductName);
    }

    [Fact]
    public async Task GetScanStatusReadsTheTenByteResponseBody()
    {
        byte[] raw = [0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x2C, 0xAA, 0x55];
        var transport = new FakeScannerTransport(raw);
        var session = new S1100SessionEngine();

        var status = await session.GetScanStatusAsync(transport);

        Assert.Equal(new byte[] { 0x1B, (byte)EpjitsuCommandCode.GetScanStatus }, transport.Written.ToArray());
        Assert.Equal(300, status.ReportedRawHeightLines);
        Assert.Equal("06 00 00 00 00 00 01 2C AA 55", status.ToString());
    }

    private sealed class FakeScannerTransport : IScannerTransport
    {
        private readonly Queue<byte> _readQueue;

        public FakeScannerTransport(byte[] bytesToRead)
        {
            _readQueue = new Queue<byte>(bytesToRead);
        }

        public List<byte> Written { get; } = [];

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Written.AddRange(buffer.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = Math.Min(buffer.Length, _readQueue.Count);
            for (var index = 0; index < count; index++)
            {
                buffer.Span[index] = _readQueue.Dequeue();
            }

            return ValueTask.FromResult(count);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
