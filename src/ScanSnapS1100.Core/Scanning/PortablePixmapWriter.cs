using System.Text;

namespace ScanSnapS1100.Core.Scanning;

public static class PortablePixmapWriter
{
    public static async Task WriteAsync(
        S1100CapturedPage page,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var header = Encoding.ASCII.GetBytes($"P6\n{page.WidthPixels} {page.HeightPixels}\n255\n");

        await using var stream = File.Create(fullPath);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(page.PixelData, cancellationToken).ConfigureAwait(false);
    }
}
