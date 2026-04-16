using ScanSnapS1100.Core.Protocol;

namespace ScanSnapS1100.Core.Tests;

public sealed class ProtocolFlagTests
{
    [Fact]
    public void DecodesStatusFlags()
    {
        var flags = new EpjitsuStatusFlags(0x11);

        Assert.True(flags.UsbPower);
        Assert.True(flags.FirmwareLoaded);
    }

    [Fact]
    public void DecodesInferredSensorFlags()
    {
        var flags = new EpjitsuSensorFlags(0x000081A0);

        Assert.True(flags.AdfOpen);
        Assert.True(flags.Sleep);
    }
}
