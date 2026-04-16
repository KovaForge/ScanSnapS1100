namespace ScanSnapS1100.Core.Scanning;

public sealed record S1100ScanResult(
    S1100CapturedPage Page,
    IReadOnlyList<S1100ScanStatus> ScanStatuses,
    int RawLinesReceived);
