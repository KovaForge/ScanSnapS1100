using ScanSnapS1100.Core.Protocol;

namespace ScanSnapS1100.Core.Tests;

public sealed class S1100ProfilesTests
{
    [Fact]
    public void ExposesTheExpectedProfiles()
    {
        Assert.Collection(
            S1100Profiles.All.OrderBy(static profile => profile.Dpi),
            profile => Assert.Equal(300, profile.Dpi),
            profile => Assert.Equal(600, profile.Dpi));
    }

    [Theory]
    [InlineData(300)]
    [InlineData(600)]
    public void SetWindowPayloadsMatchExpectedLength(int dpi)
    {
        var profile = S1100Profiles.GetForDpi(dpi);

        Assert.Equal(72, profile.SetWindowCoarseCalibration.Length);
        Assert.Equal(72, profile.SetWindowFineCalibration.Length);
        Assert.Equal(72, profile.SetWindowSendCalibration.Length);
        Assert.Equal(72, profile.SetWindowScan.Length);
        Assert.Equal(28, profile.CoarseCalibrationData.Length);
    }

    [Theory]
    [InlineData(300, 8912, 3160, 2592, 58)]
    [InlineData(600, 15904, 5360, 5184, 32)]
    public void ExposesTheExpectedRawTransferGeometry(int dpi, int lineStride, int planeStride, int planeWidth, int blockHeight)
    {
        var profile = S1100Profiles.GetForDpi(dpi);

        Assert.Equal(lineStride, profile.LineStride);
        Assert.Equal(planeStride, profile.PlaneStride);
        Assert.Equal(planeWidth, profile.PlaneWidth);
        Assert.Equal(blockHeight, profile.BlockHeight);
    }
}
