using System.Buffers.Binary;

namespace ScanSnapS1100.Core.Firmware;

public sealed class NalFirmwareImage
{
    public const int HeaderLength = 0x100;
    public const int PayloadLength = 0x10000;
    public const int TrailerLength = 1;
    public const int TotalLength = HeaderLength + PayloadLength + TrailerLength;

    public NalFirmwareImage(byte[] header, byte[] payload, byte trailer)
    {
        Header = header;
        Payload = payload;
        Trailer = trailer;
    }

    public byte[] Header { get; }

    public byte[] Payload { get; }

    public byte Trailer { get; }

    public int UploadLengthPrefix => Payload.Length + TrailerLength;

    public byte[] UploadLengthPrefixBytes
    {
        get
        {
            var bytes = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, UploadLengthPrefix);
            return bytes;
        }
    }

    public byte ComputePayloadChecksum()
    {
        var sum = 0;

        foreach (var b in Payload)
        {
            sum = (sum + b) & 0xFF;
        }

        return (byte)sum;
    }

    public static NalFirmwareImage FromFile(string path)
    {
        return FromBytes(File.ReadAllBytes(path));
    }

    public static NalFirmwareImage FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length != TotalLength)
        {
            throw new InvalidDataException(
                $"Unexpected .nal length {bytes.Length}. Expected {TotalLength} bytes.");
        }

        var header = bytes[..HeaderLength];
        var payload = bytes[HeaderLength..(HeaderLength + PayloadLength)];
        var trailer = bytes[^1];

        return new NalFirmwareImage(header, payload, trailer);
    }
}
