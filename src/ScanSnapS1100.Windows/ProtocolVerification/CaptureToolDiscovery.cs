namespace ScanSnapS1100.Windows.ProtocolVerification;

public sealed record CaptureToolStatus(
    string Name,
    string? Path,
    bool Available);

public static class CaptureToolDiscovery
{
    private static readonly (string Name, string FileName, string[] ExtraDirectories)[] ToolDefinitions =
    [
        ("USBPcapCMD", "USBPcapCMD.exe", ["USBPcap"]),
        ("tshark", "tshark.exe", ["Wireshark"]),
        ("dumpcap", "dumpcap.exe", ["Wireshark"]),
        ("Wireshark", "Wireshark.exe", ["Wireshark"]),
    ];

    public static IReadOnlyList<CaptureToolStatus> Inspect()
    {
        return ToolDefinitions
            .Select(static definition =>
            {
                var path = FindExecutable(definition.FileName, definition.ExtraDirectories);
                return new CaptureToolStatus(
                    Name: definition.Name,
                    Path: path,
                    Available: path is not null);
            })
            .ToArray();
    }

    private static string? FindExecutable(string fileName, IReadOnlyList<string> extraDirectories)
    {
        foreach (var directory in EnumerateSearchDirectories(extraDirectories))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchDirectories(IReadOnlyList<string> extraDirectories)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pathEntry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (seen.Add(pathEntry))
            {
                yield return pathEntry;
            }
        }

        foreach (var baseDirectory in EnumerateProgramFileRoots())
        {
            foreach (var extraDirectory in extraDirectories)
            {
                var candidate = Path.Combine(baseDirectory, extraDirectory);
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateProgramFileRoots()
    {
        var directories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        };

        return directories
            .Where(static directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
