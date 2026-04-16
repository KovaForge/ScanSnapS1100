namespace ScanSnapS1100.Core.Protocol;

public readonly record struct EpjitsuSensorFlags(uint Raw)
{
    // These bit assignments are inferred from Ben Challenor's analyzer and logs.
    public bool AdfOpen => IsBitSet(5);

    public bool Hopper => !IsBitSet(6);

    public bool Top => IsBitSet(7);

    public bool ScanButton => IsBitSet(8);

    public bool Sleep => IsBitSet(15);

    private bool IsBitSet(int bitIndex)
    {
        return (Raw & (1u << bitIndex)) != 0;
    }

    public override string ToString()
    {
        return $"0x{Raw:X8} AdfOpen={AdfOpen} Hopper={Hopper} Top={Top} ScanButton={ScanButton} Sleep={Sleep}";
    }
}
