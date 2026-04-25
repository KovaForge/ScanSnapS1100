using ScanSnapS1100.Core.Protocol;

namespace ScanSnapS1100.Core.Tests;

public sealed class S1100FineCalibrationTests
{
    [Fact]
    public void BuildFixedFineCalibrationPayloadBuildsS1100RawCalibrationLayout()
    {
        var profile = new S1100Profile(
            Dpi: 300,
            LineStride: 12,
            PlaneStride: 4,
            PlaneWidth: 2,
            BlockHeight: 2,
            CoarseCalibrationData: [],
            SetWindowCoarseCalibration: [],
            SetWindowFineCalibration: [],
            SetWindowSendCalibration: [],
            SendCalibrationHeader1: [],
            SendCalibrationHeader2: [],
            SetWindowScan: []);

        var payload = S1100SessionEngine.BuildFixedFineCalibrationPayload(profile);

        Assert.Equal(24, payload.Length);
        Assert.Equal([0x00, 0xFF, 0x00, 0xFF], payload[0..4]);
        Assert.Equal([0x00, 0xFF, 0x00, 0xFF], payload[8..12]);
        Assert.Equal([0x00, 0xFF, 0x00, 0xFF], payload[16..20]);
    }
}
