namespace ScanSnapS1100.Core.Protocol;

public enum EpjitsuCommandCode : byte
{
    GetStatus = 0x03,
    LoadFirmware = 0x06,
    GetIdentifiers = 0x13,
    ReinitializeFirmware = 0x16,
    GetSensorFlags = 0x33,
    GetScanStatus = 0x43,
    SetFineCalibration1 = 0xC3,
    SetFineCalibration2 = 0xC4,
    SetLut = 0xC5,
    SetCoarseCalibration = 0xC6,
    SetLamp = 0xD0,
    SetWindow = 0xD1,
    ReadScanDataD2 = 0xD2,
    ReadScanDataD3 = 0xD3,
    SetPaperFeed = 0xD4,
    ResetButton = 0x65,
    StartScan = 0xD6,
}
