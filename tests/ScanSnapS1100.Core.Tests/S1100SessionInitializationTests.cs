using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Core.Scanning;
using ScanSnapS1100.Core.Transport;

namespace ScanSnapS1100.Core.Tests;

public sealed class S1100SessionInitializationTests
{
    [Fact]
    public async Task ScanColorInitializesTheS1100BeforeStartingTheScan()
    {
        var profile = S1100Profiles.At300Dpi;
        var transport = new ScriptedTransport(
            [0x06], // d1 ack
            [0x06], // coarse window payload ack
            [0x06], // c6 ack
            [0x06], // coarse payload ack
            [0x06], // c5 ack
            [0x06], // lut payload ack
            [0x06], // d0 ack
            [0x06], // lamp payload ack
            [0x06], // d1 ack
            [0x06], // scan window payload ack
            [0x50, 0x50, 0x00, 0x00], // ghs payload
            [0x06], // d4 ack
            [0x06], // ingest payload ack
            [0x06], // d6 ack
            [0x06], // d3 ack
            [0x00], // no payload returned
            [0x06], // d4 ack during cleanup
            [0x06], // eject payload ack
            [0x06]  // 65 ack
        );

        var scanner = new S1100Scanner();

        await Assert.ThrowsAnyAsync<IOException>(async () =>
            await scanner.ScanColorAsync(transport, new S1100ScanSettings(300)));

        Assert.Contains("1BC6", transport.Writes);
        Assert.Contains("1BC5", transport.Writes);
        Assert.Contains("1BD0", transport.Writes);
        Assert.Contains("1B33", transport.Writes);
        Assert.Contains(profile.CoarseCalibrationData, transport.WrittenPayloads, ByteArrayComparer.Instance);
        Assert.Contains(BuildExpectedIdentityLut(), transport.WrittenPayloads, ByteArrayComparer.Instance);
    }

    private static byte[] BuildExpectedIdentityLut()
    {
        var payload = new byte[512];
        for (var value = 0; value < 256; value++)
        {
            payload[value * 2] = 0x00;
            payload[(value * 2) + 1] = (byte)value;
        }

        return payload;
    }

    private sealed class ScriptedTransport : IScannerTransport
    {
        private readonly Queue<byte[]> _reads;

        public ScriptedTransport(params byte[][] reads)
        {
            _reads = new Queue<byte[]>(reads);
        }

        public List<string> Writes { get; } = [];

        public List<byte[]> WrittenPayloads { get; } = [];

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytes = buffer.ToArray();
            WrittenPayloads.Add(bytes);
            Writes.Add(Convert.ToHexString(bytes));
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_reads.Count == 0)
            {
                return ValueTask.FromResult(0);
            }

            var next = _reads.Dequeue();
            next.CopyTo(buffer);
            return ValueTask.FromResult(next.Length);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static ByteArrayComparer Instance { get; } = new();

        public bool Equals(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null || x.Length != y.Length)
            {
                return false;
            }

            return x.AsSpan().SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            return obj.Length;
        }
    }
}
