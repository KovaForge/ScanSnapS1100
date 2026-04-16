# USBPcap Verification

This repo now emits raw transport traces from the managed S1100 stack. The intent is to compare those traces against captures taken from the working Fujitsu x64 Windows stack.

## What Exists

- `transport trace-probe <output-json>`
  - runs `GetStatus`, `GetIdentifiers`, and `GetSensorFlags`
  - writes a timestamped read/write transcript as JSON
- `transport scan-color <300|600> <output-ppm> [trace-json]`
  - runs the current raw scan path
  - writes the transport trace even when the scan fails early, for example when no sheet is present
- `verify capture-tools`
  - reports whether `USBPcapCMD.exe`, `tshark.exe`, `dumpcap.exe`, and `Wireshark.exe` are available on the local machine

## Current Limitation

The repo does not yet import `.pcap` or `.pcapng` files directly. The current verification workflow is trace-to-capture comparison, not full parser-driven USBPcap ingestion.

## Intended Workflow

1. Install USBPcap and Wireshark or `tshark`.
2. Confirm the tools are visible:

   ```powershell
   dotnet run --project .\src\ScanSnapS1100.Tool -- verify capture-tools
   ```

3. Capture a working Fujitsu x64 driver session with USBPcap while performing:
   - a probe sequence
   - a firmware-init sequence if available
   - a single-sheet scan at 300 DPI

4. Produce a managed trace from this repo:

   ```powershell
   dotnet run --project .\src\ScanSnapS1100.Tool -- transport trace-probe .\baseline\trace-probe.json
   dotnet run --project .\src\ScanSnapS1100.Tool -- transport scan-color 300 .\baseline\s1100-test.ppm .\baseline\s1100-scan-trace.json
   ```

5. Compare command ordering and payloads:
   - `1B03` for status
   - `1B13` for identifiers
   - `1B33` for sensor flags
   - `1BD4` for paper feed
   - `1BD1` for scan window
   - `1BD6` for scan start
   - `1BD3` for block-read request
   - `1B43` for post-block scan status

## What Still Needs To Be Added

- a repo-native USBPcap import/parser path
- an automated comparator between USBPcap captures and `TransportTrace` JSON
- trace-backed validation of the current scan setup sequence against the vendor stack
