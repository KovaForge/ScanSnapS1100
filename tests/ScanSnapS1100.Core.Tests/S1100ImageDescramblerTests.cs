using ScanSnapS1100.Core.Protocol;
using ScanSnapS1100.Core.Scanning;

namespace ScanSnapS1100.Core.Tests;

public sealed class S1100ImageDescramblerTests
{
    [Fact]
    public void DescrambleColorBlockMapsBlueRedGreenPlanesIntoRgbPixels()
    {
        var profile = new S1100Profile(
            Dpi: 300,
            LineStride: 12,
            PlaneStride: 4,
            PlaneWidth: 2,
            BlockHeight: 1,
            CoarseCalibrationData: [],
            SetWindowCoarseCalibration: [],
            SetWindowFineCalibration: [],
            SetWindowSendCalibration: [],
            SendCalibrationHeader1: [],
            SendCalibrationHeader2: [],
            SetWindowScan: []);

        byte[] raw =
        [
            0x31, 0x32, 0x00, 0x00,
            0x11, 0x12, 0x00, 0x00,
            0x21, 0x22, 0x00, 0x00,
        ];

        var descrambled = S1100ImageDescrambler.DescrambleColorBlock(raw, profile, rawLines: 1);

        Assert.Equal(new byte[] { 0x11, 0x21, 0x31, 0x12, 0x22, 0x32 }, descrambled);
    }
}
