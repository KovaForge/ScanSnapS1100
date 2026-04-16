namespace ScanSnapS1100.Core.Protocol;

public readonly record struct EpjitsuStatusFlags(byte Raw)
{
    public bool UsbPower => (Raw & 0x01) != 0;

    public bool FirmwareLoaded => (Raw & 0x10) != 0;

    public override string ToString()
    {
        return $"0x{Raw:X2} UsbPower={UsbPower} FirmwareLoaded={FirmwareLoaded}";
    }
}
