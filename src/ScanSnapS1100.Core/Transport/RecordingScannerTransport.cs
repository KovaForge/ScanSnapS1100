using ScanSnapS1100.Core.Diagnostics;

namespace ScanSnapS1100.Core.Transport;

public sealed class RecordingScannerTransport : IScannerTransport
{
    private readonly IScannerTransport _inner;

    public RecordingScannerTransport(IScannerTransport inner, TransportTrace trace)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    public TransportTrace Trace { get; }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Trace.Add(TransportDirection.Write, buffer.Span);
        await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (bytesRead > 0)
        {
            Trace.Add(TransportDirection.Read, buffer.Span[..bytesRead]);
        }

        return bytesRead;
    }

    public ValueTask DisposeAsync()
    {
        return _inner.DisposeAsync();
    }
}
