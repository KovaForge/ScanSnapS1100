using System.Buffers.Binary;

namespace ScanSnapS1100.Core.Scanning;

public sealed class S1100ScanStatus
{
    public S1100ScanStatus(byte[] rawBytes)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);
        RawBytes = rawBytes;
    }

    public byte[] RawBytes { get; }

    public int? ReportedRawHeightLines =>
        RawBytes.Length >= 8
            ? BinaryPrimitives.ReadUInt16BigEndian(RawBytes.AsSpan(6, 2))
            : null;

    public override string ToString()
    {
        return BitConverter.ToString(RawBytes).Replace('-', ' ');
    }
}
