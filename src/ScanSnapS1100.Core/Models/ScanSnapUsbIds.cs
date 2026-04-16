namespace ScanSnapS1100.Core.Models;

public static class ScanSnapUsbIds
{
    public const int VendorId = 0x04C5;
    public const int S1100ProductId = 0x1200;
    public const int S1100iProductId = 0x1447;

    public static bool IsSupportedProductId(int productId)
    {
        return productId is S1100ProductId or S1100iProductId;
    }
}
