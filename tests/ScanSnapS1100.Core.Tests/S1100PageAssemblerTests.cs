using ScanSnapS1100.Core.Scanning;

namespace ScanSnapS1100.Core.Tests;

public sealed class S1100PageAssemblerTests
{
    [Fact]
    public void AppendsOnlyRowsPastTheSkipOffsetAndCropsHorizontalPadding()
    {
        var geometry = new S1100ScanGeometry(
            Dpi: 300,
            RawWidthPixels: 4,
            RawHeightPixels: 3,
            PageWidthPixels: 2,
            PageHeightPixels: 2,
            XStartOffsetPixels: 1,
            YSkipOffsetLines: 1);

        var assembler = new S1100PageAssembler(geometry);

        byte[] block =
        [
            0x01, 0x01, 0x01, 0x02, 0x02, 0x02, 0x03, 0x03, 0x03, 0x04, 0x04, 0x04,
            0x11, 0x11, 0x11, 0x12, 0x12, 0x12, 0x13, 0x13, 0x13, 0x14, 0x14, 0x14,
            0x21, 0x21, 0x21, 0x22, 0x22, 0x22, 0x23, 0x23, 0x23, 0x24, 0x24, 0x24,
        ];

        assembler.AppendBlock(block, rawLineCount: 3, globalRawRowStart: 0);
        var page = assembler.ToCapturedPage(dpi: 300);

        Assert.Equal(2, page.WidthPixels);
        Assert.Equal(2, page.HeightPixels);
        Assert.Equal(
            new byte[]
            {
                0x12, 0x12, 0x12, 0x13, 0x13, 0x13,
                0x22, 0x22, 0x22, 0x23, 0x23, 0x23,
            },
            page.PixelData);
    }
}
