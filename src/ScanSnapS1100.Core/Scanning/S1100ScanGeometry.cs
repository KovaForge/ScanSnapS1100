using ScanSnapS1100.Core.Protocol;

namespace ScanSnapS1100.Core.Scanning;

public sealed record S1100ScanGeometry(
    int Dpi,
    int RawWidthPixels,
    int RawHeightPixels,
    int PageWidthPixels,
    int PageHeightPixels,
    int XStartOffsetPixels,
    int YSkipOffsetLines)
{
    private const int ScannerUnitsPerInch = 1200;
    private const int DefaultPageWidthUnits = 10200;
    private const int DefaultPageHeightUnits = 13800;
    private const int AdfHeightPaddingUnits = 450;

    public static S1100ScanGeometry FromProfile(
        S1100Profile profile,
        double pageWidthInches = 8.5,
        double pageHeightInches = 11.5)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var pageWidthUnits = InchesToScannerUnits(pageWidthInches, nameof(pageWidthInches));
        var pageHeightUnits = InchesToScannerUnits(pageHeightInches, nameof(pageHeightInches));

        var pageWidthPixels = ScannerUnitsToPixels(pageWidthUnits, profile.Dpi);
        var pageHeightPixels = ScannerUnitsToPixels(pageHeightUnits, profile.Dpi);
        var ySkipOffsetLines = ScannerUnitsToPixels(AdfHeightPaddingUnits, profile.Dpi);
        var rawHeightPixels = ScannerUnitsToPixels(pageHeightUnits + AdfHeightPaddingUnits, profile.Dpi);
        var xStartOffsetPixels = Math.Max(0, (profile.PlaneWidth - pageWidthPixels) / 2);

        return new S1100ScanGeometry(
            Dpi: profile.Dpi,
            RawWidthPixels: profile.PlaneWidth,
            RawHeightPixels: rawHeightPixels,
            PageWidthPixels: pageWidthPixels,
            PageHeightPixels: pageHeightPixels,
            XStartOffsetPixels: xStartOffsetPixels,
            YSkipOffsetLines: ySkipOffsetLines);
    }

    public static int GetDefaultPageWidthPixels(int dpi)
    {
        return ScannerUnitsToPixels(DefaultPageWidthUnits, dpi);
    }

    public static int GetDefaultPageHeightPixels(int dpi)
    {
        return ScannerUnitsToPixels(DefaultPageHeightUnits, dpi);
    }

    private static int InchesToScannerUnits(double inches, string paramName)
    {
        if (double.IsNaN(inches) || double.IsInfinity(inches) || inches <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, inches, "Paper dimensions must be positive finite values.");
        }

        return checked((int)Math.Round(inches * ScannerUnitsPerInch, MidpointRounding.AwayFromZero));
    }

    private static int ScannerUnitsToPixels(int scannerUnits, int dpi)
    {
        return checked(scannerUnits * dpi / ScannerUnitsPerInch);
    }
}
