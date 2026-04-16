# ScanSnap S1100

Windows ARM64 driver and tooling effort for the Fujitsu ScanSnap S1100.

## Current Focus

The first implementation milestone is not a full imaging driver. It is a buildable protocol and diagnostics stack that can:

- inspect Fujitsu `.nal` firmware containers
- model the S1100 `epjitsu` command flow
- discover locally attached S1100-class devices on Windows
- enumerate the live `usbscan` image-class interface path
- open the scanner over a raw Windows device handle and issue status/identity probes
- upload firmware from a local `.nal` image when the device is not already initialized
- record raw transport traces as JSON for later comparison against USBPcap captures
- execute the S1100 single-sheet raw scan path into a portable pixmap (`.ppm`) image
- descramble S1100 raw color blocks into RGB scanlines and crop the padded margins
- export a reproducible x64 baseline snapshot from the working Windows machine
- publish a managed diagnostics build for `win-arm64`

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
dotnet run --project .\src\ScanSnapS1100.Tool -- transport trace-probe .\baseline\trace-probe.json
```

Upload firmware from the local Windows install:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- transport upload-firmware C:\Windows\System32\1100_0B00.nal
```

Run the raw color scan path into a `.ppm` file:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- transport scan-color 300 .\baseline\s1100-test.ppm .\baseline\s1100-scan-trace.json
```

Export the current x64 baseline snapshot:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- baseline export
```

Check whether USBPcap / Wireshark tooling is installed:

```powershell
dotnet run --project .\src\ScanSnapS1100.Tool -- verify capture-tools
```

Publish the diagnostics CLI for an ARM64 Windows machine:

```powershell
dotnet publish .\src\ScanSnapS1100.Tool\ScanSnapS1100.Tool.csproj -c Release -r win-arm64 --self-contained false
```

## ARM64 Readiness

Current status:

- `win-arm64` publish for the managed diagnostics CLI succeeds
- live raw status/identity/sensor probes succeed against the attached x64-hosted S1100
- the firmware upload code path runs without the Fujitsu x64 DLL when firmware is already loaded
- the scan block acquisition and descrambling pipeline is implemented, but it still needs a live sheet-fed scan validation
- USBPcap and Wireshark are installed, but USBPcap still needs a reboot before filter control is reliable
- the scanner is now connected directly to the laptop root hub at `PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(2)` / `HS02`
- the first ARM64 milestone is the managed raw-scan CLI, not a packaged WIA/STI driver
- a real Windows imaging driver layer (WIA/STI/COM packaging) is still pending

Before handing the repo to an ARM64 test machine, the remaining gates are:

1. Validate `transport scan-color` end to end with a sheet inserted.
2. Reboot so USBPcap filter control is active, then capture a working vendor x64 Windows scan for protocol comparison.
3. Compare that vendor capture against the managed raw trace and keep the managed raw-scan CLI as the first ARM64 handoff target.

## Next Milestones

1. Validate the raw scan path with a real sheet and capture a successful `.ppm`.
2. Turn USBPcap / Wireshark capture output into a repo-native comparison format.
3. Compare the working Fujitsu x64 scan transcript with the managed raw scan transcript.
4. Add a first Windows imaging integration surface on top of the managed core.
5. Replace the remaining manual calibration assumptions with trace-backed behavior.
