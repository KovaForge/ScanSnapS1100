using System.Text.Json;

namespace ScanSnapS1100.Core.Diagnostics;

public enum TransportDirection
{
    Write,
    Read,
}

public sealed class TransportTrace
{
    private readonly List<TransportTraceEvent> _events = [];

    public IReadOnlyList<TransportTraceEvent> Events => _events;

    public void Add(TransportDirection direction, ReadOnlySpan<byte> bytes)
    {
        _events.Add(new TransportTraceEvent(
            Index: _events.Count,
            TimestampUtc: DateTimeOffset.UtcNow,
            Direction: direction,
            Length: bytes.Length,
            Hex: Convert.ToHexString(bytes)));
    }

    public async Task WriteJsonAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(
                stream,
                _events,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed record TransportTraceEvent(
    int Index,
    DateTimeOffset TimestampUtc,
    TransportDirection Direction,
    int Length,
    string Hex);
