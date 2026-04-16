namespace ScanSnapS1100.Core.Scanning;

public sealed class S1100PageAssembler
{
    private readonly S1100ScanGeometry _geometry;
    private readonly MemoryStream _pixelData = new();
    private int _maxOutputRows;
    private int _writtenRows;

    public S1100PageAssembler(S1100ScanGeometry geometry)
    {
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _maxOutputRows = geometry.PageHeightPixels;
    }

    public int WrittenRows => _writtenRows;

    public void ApplyReportedRawHeight(int? rawHeightLines)
    {
        if (rawHeightLines is null || rawHeightLines <= 0)
        {
            return;
        }

        var croppedHeight = Math.Max(0, rawHeightLines.Value - _geometry.YSkipOffsetLines);
        _maxOutputRows = Math.Min(_maxOutputRows, croppedHeight);
    }

    public void AppendBlock(ReadOnlySpan<byte> descrambledBlock, int rawLineCount, int globalRawRowStart)
    {
        if (rawLineCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawLineCount), rawLineCount, "Raw line count must be non-negative.");
        }

        var rowWidthBytes = _geometry.RawWidthPixels * 3;
        var requiredBytes = checked(rawLineCount * rowWidthBytes);
        if (descrambledBlock.Length < requiredBytes)
        {
            throw new ArgumentException(
                $"Expected at least {requiredBytes} descrambled bytes for {rawLineCount} lines, received {descrambledBlock.Length}.",
                nameof(descrambledBlock));
        }

        var cropOffsetBytes = _geometry.XStartOffsetPixels * 3;
        var cropWidthBytes = _geometry.PageWidthPixels * 3;

        for (var row = 0; row < rawLineCount; row++)
        {
            var rawRow = globalRawRowStart + row;
            var outputRow = rawRow - _geometry.YSkipOffsetLines;

            if (outputRow < 0 || outputRow < _writtenRows)
            {
                continue;
            }

            if (outputRow >= _maxOutputRows)
            {
                break;
            }

            var rowOffset = (row * rowWidthBytes) + cropOffsetBytes;
            _pixelData.Write(descrambledBlock[rowOffset..(rowOffset + cropWidthBytes)]);
            _writtenRows++;
        }
    }

    public S1100CapturedPage ToCapturedPage(int dpi)
    {
        return new S1100CapturedPage(
            WidthPixels: _geometry.PageWidthPixels,
            HeightPixels: _writtenRows,
            Dpi: dpi,
            PixelData: _pixelData.ToArray());
    }
}
