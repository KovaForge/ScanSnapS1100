namespace ScanSnapS1100.Core.Transport;

public interface IScannerTransport : IAsyncDisposable
{
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
