using ScanSnapS1100.Core.Firmware;

namespace ScanSnapS1100.Core.Tests;

public sealed class NalFirmwareImageTests
{
    [Fact]
    public void ParsesExpectedContainerLayout()
    {
        var bytes = new byte[NalFirmwareImage.TotalLength];

        for (var i = 0; i < NalFirmwareImage.HeaderLength; i++)
        {
            bytes[i] = 0xAA;
        }

        for (var i = 0; i < NalFirmwareImage.PayloadLength; i++)
        {
            bytes[NalFirmwareImage.HeaderLength + i] = (byte)(i & 0xFF);
        }

        bytes[^1] = 0x5A;

        var image = NalFirmwareImage.FromBytes(bytes);

        Assert.Equal(NalFirmwareImage.HeaderLength, image.Header.Length);
        Assert.Equal(NalFirmwareImage.PayloadLength, image.Payload.Length);
        Assert.Equal((byte)0x5A, image.Trailer);
        Assert.Equal(NalFirmwareImage.PayloadLength + 1, image.UploadLengthPrefix);
    }

    [Fact]
    public void ComputesPayloadChecksumModulo256()
    {
        var bytes = new byte[NalFirmwareImage.TotalLength];
        bytes[NalFirmwareImage.HeaderLength] = 0x10;
        bytes[NalFirmwareImage.HeaderLength + 1] = 0x20;
        bytes[NalFirmwareImage.HeaderLength + 2] = 0x30;

        var image = NalFirmwareImage.FromBytes(bytes);

        Assert.Equal((byte)0x60, image.ComputePayloadChecksum());
    }

    [Fact]
    public void RejectsUnexpectedContainerLength()
    {
        Assert.Throws<InvalidDataException>(() => NalFirmwareImage.FromBytes(new byte[32]));
    }
}
