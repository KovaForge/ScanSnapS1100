# ScanSnap S1100 Session Resume

## Purpose

Resume the Windows x64 to Windows ARM64 S1100 bring-up after the required reboot for USBPcap.

## Current Machine State

- repo root: `C:\Users\liveu\source\repos\KovaForge\ScanSnapS1100`
- current date: `2026-04-17`
- scanner model: `ScanSnap S1100`
- scanner is connected directly to the laptop, not through the Surface Dock
- current PnP instance: `USB\VID_04C5&PID_1200\5&2CDBCAF7&0&2`
- current interface path: `\\?\usb#vid_04c5&pid_1200#5&2cdbcaf7&0&2#{6bdd1fc6-810f-11d0-bec7-08002be2092f}`
- current direct-port topology: `PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(2)` / `HS02`
- current root hub: `USB\ROOT_HUB30\4&338DE6A&0&0`
- current driver binding: `usbscan`
- current vendor driver package: `oem17.inf`
- current Fujitsu driver version: `2.0.0.7`

## Verified Working Before Reboot

- `dotnet run --project src\ScanSnapS1100.Tool -- devices inspect`
  - detected the direct-port S1100 instance and image-class interface path
- `dotnet run --project src\ScanSnapS1100.Tool -- transport probe`
  - succeeded on the direct-port connection
  - status: `0x19 UsbPower=True FirmwareLoaded=True`
  - identifiers: `FUJITSU ScanSnap S1100  0B00`
  - sensors: `0x0000D050 AdfOpen=False Hopper=False Top=False ScanButton=False Sleep=True`
- `dotnet run --project src\ScanSnapS1100.Tool -- transport scan-color 300 .\baseline\direct-port-test.ppm .\baseline\direct-port-test-trace.json`
  - transport opened correctly
  - failed at paper ingest with `The scanner did not detect a sheet in the feed path.`
  - this means the current blocker for the raw-scan gate is physical paper, not transport bring-up
- `dotnet run --project src\ScanSnapS1100.Tool -- verify capture-tools`
  - `USBPcapCMD.exe`, `tshark.exe`, `dumpcap.exe`, and `Wireshark.exe` are installed

## USBPcap State

- USBPcap installation succeeded
- USBPcap reported:
  - `Failed to invoke DIF_PROPERTYCHANGE! Please reboot.`
- conclusion:
  - do not attempt the authoritative vendor capture until after reboot

## ARM64 Target Decision

The first ARM64 handoff target is the managed raw-scan CLI, not a packaged WIA/STI driver.

That means the first ARM64 validation pass is:

1. publish `ScanSnapS1100.Tool` for `win-arm64`
2. verify transport probe on ARM64
3. verify firmware upload path on ARM64
4. verify paper-fed raw color scan on ARM64

The Windows imaging driver layer remains a later milestone.

## First Commands To Run After Reboot

```powershell
cd C:\Users\liveu\source\repos\KovaForge\ScanSnapS1100
dotnet run --project src\ScanSnapS1100.Tool -- verify capture-tools
dotnet run --project src\ScanSnapS1100.Tool -- devices inspect
dotnet run --project src\ScanSnapS1100.Tool -- transport probe
```

## Gate 1: Raw Scan Validation

Insert one plain sheet into the S1100 and run:

```powershell
dotnet run --project src\ScanSnapS1100.Tool -- transport scan-color 300 .\baseline\post-reboot-test.ppm .\baseline\post-reboot-test-trace.json
```

Expected outcome:

- a successful `.ppm` file
- a successful JSON transport trace

If this still fails at paper ingest, recheck that the sheet is actually seated in the feed path.

## Gate 2: Vendor x64 USBPcap Capture

After reboot, capture the vendor stack while the scanner remains on the direct laptop port.

Important selection hint:

- choose the USBPcap filter for the controller rooted at `PCIROOT(0)#PCI(1400)#USBROOT(0)`
- do not use the controller rooted at `PCIROOT(0)#PCI(0D00)#USBROOT(0)`

Capture target:

- a working vendor-backed single-sheet 300 DPI scan

Artifacts to keep:

- the USBPcap `.pcap` or `.pcapng`
- the vendor-produced image output
- the managed JSON trace from `transport trace-probe`
- the managed JSON trace from `transport scan-color`

## Gate 3: ARM64 Readiness Check

The repo is ready for the first ARM64 machine test when both are true:

1. the x64 managed raw scan succeeds with a real sheet
2. a rebooted USBPcap vendor capture exists for comparison

## Suggested Resume Prompt

Use this after reboot:

`Resume from C:\Users\liveu\source\repos\KovaForge\ScanSnapS1100\session-resume-2026-04-17.md and continue the ScanSnap S1100 x64 validation and ARM64 handoff work.`
