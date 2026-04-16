# ARM64 Test Status

## Ready Now

- `ScanSnapS1100.Tool` publishes successfully for `win-arm64`
- the managed Windows transport can open the S1100 device path through the in-box `usbscan` stack
- status, identifier, and sensor commands work on the live x64 machine
- the repo can emit protocol traces as JSON
- the repo includes a raw color scan pipeline and PPM writer

## Not Ready Yet

- a live paper-fed scan has not been validated end to end
- USBPcap / Wireshark capture tools are not installed on the current x64 machine
- there is no direct `.pcapng` importer yet
- there is no packaged WIA / STI / COM imaging driver layer yet

## Decision

The repo is not ready yet for meaningful ARM64 scanner testing.

It will be ready for the first ARM64 handoff when these three checks are green:

1. `transport scan-color 300 <file.ppm>` succeeds with a real sheet inserted on x64.
2. USBPcap captures from the vendor x64 path are available for comparison.
3. The intended ARM64 test target is defined:
   - managed raw-scan CLI
   - or a formal Windows imaging driver package
