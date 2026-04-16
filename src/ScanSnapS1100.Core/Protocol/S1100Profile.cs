namespace ScanSnapS1100.Core.Protocol;

public sealed record S1100Profile(
    int Dpi,
    byte[] CoarseCalibrationData,
    byte[] SetWindowCoarseCalibration,
    byte[] SetWindowFineCalibration,
    byte[] SetWindowSendCalibration,
    byte[] SendCalibrationHeader1,
    byte[] SendCalibrationHeader2,
    byte[] SetWindowScan);
