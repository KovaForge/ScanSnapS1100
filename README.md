# ScanSnap S1100

Windows ARM64 driver and tooling effort for the Fujitsu ScanSnap S1100.

## Current Focus

The first implementation milestone is not a full imaging driver. It is a buildable protocol and diagnostics stack that can:

- inspect Fujitsu `.nal` firmware containers
- model the S1100 `epjitsu` command flow
- discover locally attached S1100-class devices on Windows
- enumerate the live `usbscan` image-class interface path
- open the scanner over a raw Windows device handle and issue status/identity probes
- export a reproducible x64 baseline snapshot from the working Windows machine
- provide a CLI foundation for future raw USB transport and scan execution

## Repository Layout

- `src/ScanSnapS1100.Core`
  - protocol models
  - firmware parsing
  - S1100 scan profiles and calibration payloads
  - transport abstractions
- `src/ScanSnapS1100.Windows`
  - Windows-specific discovery and transport integration
- `src/ScanSnapS1100.Tool`
  - diagnostics CLI
- `tests/ScanSnapS1100.Core.Tests`
  - unit tests for container parsing and protocol metadata
- `windows-arm64-s1100-plan.md`
  - implementation plan
- `linux-efforts-deep-analysis.md`
  - Linux reverse-engineering analysis and porting guidance

## Commands

List locally attached S1100 devices:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- devices list
dotnet run --project .\src\ScanSnapS1100.Tool -- devices inspect
```

Inspect a Fujitsu firmware container:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- firmware inspect C:\Windows\System32\1100_0B00.nal
```

Show the embedded S1100 protocol profile:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- profiles show 300
dotnet run --project .\src\ScanSnapS1100.Tool -- profiles show 600
```

List the Windows device interface path and issue a raw transport probe:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- transport interfaces
dotnet run --project .\src\ScanSnapS1100.Tool -- transport status
dotnet run --project .\src\ScanSnapS1100.Tool -- transport probe
```

Export the current x64 baseline snapshot:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- baseline export
```

## Next Milestones

1. Add USBPcap-driven protocol verification against the working Windows x64 stack.
2. Expand the raw transport from status/identity probes into firmware load and scan setup.
3. Reproduce firmware load and status polling without the Fujitsu x64 DLL.
4. Add scan block acquisition and image descrambling.
5. Wrap the core in a Windows imaging integration layer for ARM64.
