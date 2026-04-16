namespace ScanSnapS1100.Core.Scanning;

public sealed record S1100ScanSettings(
    int Dpi,
    double PageWidthInches = 8.5,
    double PageHeightInches = 11.5,
    bool IngestPaper = true,
    bool EjectPaper = true,
    bool ResetButtonWhenDone = true);
