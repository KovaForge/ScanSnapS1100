using ScanSnapS1100.Core.Protocol;

namespace ScanSnapS1100.Core.Scanning;

public static class S1100ImageDescrambler
{
    public static byte[] DescrambleColorBlock(
        ReadOnlySpan<byte> rawData,
        S1100Profile profile,
        int rawLines)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (rawLines < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawLines), rawLines, "Raw line count must be non-negative.");
        }

        var requiredBytes = checked(rawLines * profile.LineStride);
        if (rawData.Length < requiredBytes)
        {
            throw new ArgumentException(
                $"Expected at least {requiredBytes} bytes for {rawLines} raw lines, received {rawData.Length}.",
                nameof(rawData));
        }

        var output = new byte[checked(rawLines * profile.PlaneWidth * 3)];

        for (var row = 0; row < rawLines; row++)
        {
            var rawRowOffset = row * profile.LineStride;
            var outputRowOffset = row * profile.PlaneWidth * 3;

            for (var column = 0; column < profile.PlaneWidth; column++)
            {
                var blue = rawData[rawRowOffset + column];
                var red = rawData[rawRowOffset + profile.PlaneStride + column];
                var green = rawData[rawRowOffset + (profile.PlaneStride * 2) + column];

                var outputOffset = outputRowOffset + (column * 3);
                output[outputOffset] = red;
                output[outputOffset + 1] = green;
                output[outputOffset + 2] = blue;
            }
        }

        return output;
    }
}
