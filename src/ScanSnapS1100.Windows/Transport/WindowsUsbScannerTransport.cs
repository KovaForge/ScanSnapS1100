using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using ScanSnapS1100.Core.Transport;
using ScanSnapS1100.Windows.Interop;

namespace ScanSnapS1100.Windows.Transport;

public sealed class WindowsUsbScannerTransport : IScannerTransport
{
    private readonly FileStream _stream;

    private WindowsUsbScannerTransport(string devicePath, SafeFileHandle handle)
    {
        DevicePath = devicePath;
        _stream = new FileStream(handle, FileAccess.ReadWrite, bufferSize: 4096, isAsync: true);
    }

    public string DevicePath { get; }

    public static WindowsUsbScannerTransport Open(string devicePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(devicePath);

        var handle = Kernel32Native.CreateFileW(
            fileName: devicePath,
            desiredAccess: Kernel32Native.GenericRead | Kernel32Native.GenericWrite,
            shareMode: Kernel32Native.FileShareRead | Kernel32Native.FileShareWrite,
            securityAttributes: IntPtr.Zero,
            creationDisposition: Kernel32Native.OpenExisting,
            flagsAndAttributes: Kernel32Native.FileAttributeNormal | Kernel32Native.FileFlagOverlapped,
            templateFile: IntPtr.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new Win32Exception(Kernel32Native.GetLastError(), $"Failed to open scanner interface '{devicePath}'.");
        }

        return new WindowsUsbScannerTransport(devicePath, handle);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _stream.ReadAsync(buffer, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _stream.DisposeAsync();
    }
}
