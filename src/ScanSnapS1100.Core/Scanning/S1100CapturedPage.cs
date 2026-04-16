namespace ScanSnapS1100.Core.Scanning;

public sealed record S1100CapturedPage(
    int WidthPixels,
    int HeightPixels,
    int Dpi,
    byte[] PixelData)
{
    public int Stride => WidthPixels * 3;
}
