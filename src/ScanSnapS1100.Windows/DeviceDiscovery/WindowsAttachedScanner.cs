namespace ScanSnapS1100.Windows.DeviceDiscovery;

public sealed record WindowsAttachedScanner(
    string Name,
    string Manufacturer,
    string PnpDeviceId,
    string? PnpClass,
    string? ClassGuid,
    int VendorId,
    int ProductId,
    int? ConfigManagerErrorCode,
    string[] HardwareIds,
    string[] CompatibleIds,
    string? Service,
    string? DriverProviderName,
    DateTime? DriverDate,
    string? DriverVersion,
    string? DriverName,
    string? InfName,
    string? Status);
