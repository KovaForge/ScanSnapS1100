# Fujitsu ScanSnap S1100 Windows ARM64 Port Plan

## Objective

Make the ScanSnap S1100 functional on Windows ARM64 by porting the known Linux reverse-engineering work into a Windows-native user-mode scanner driver stack, starting from proven protocol behavior on a working Windows x64 machine.

## What We Know

- The connected scanner on the current machine is `USB\VID_04C5&PID_1200`, which is the ScanSnap S1100.
- On Windows x64, the device is already bound to the in-box `usbscan` stack under the `Image` class.
- Fujitsu's shipped package is not a kernel USB driver. It is an INF plus a user-mode Still Image mini-driver DLL and helper DLLs.
- The local Windows package contains `1100_0B00.nal`, which matches the firmware blob published in the Linux firmware repository by SHA-256.
- Linux support exists in the SANE `epjitsu` backend. That code is the main protocol reference. The installer-style GitHub repos are secondary packaging references only.

## Working Assumption

Do not start with a new kernel-mode ARM64 USB driver.

Start with a user-mode ARM64 scanner mini-driver layered on top of the Windows USB scanner stack, because that matches how the working x64 Fujitsu package is structured and reduces scope significantly.

## Recommended Architecture

### Primary path

- Keep the device bound to `usbscan`.
- Implement an ARM64 user-mode scanner mini-driver with the scan protocol and image pipeline.
- Surface the scanner through the Windows imaging stack so normal scanning apps can use it.

### Secondary path

- Build a standalone ARM64 diagnostic CLI or test harness first.
- Use it to validate firmware upload, status polling, scan start, and image reads before integrating with WIA/STI.

This reduces risk. The protocol can be proven independently before UI or OS integration complicates debugging.

## Plan of Attack

## Phase 1: Capture a Clean Baseline From the Working x64 System

Goals:

- Preserve all artifacts from the working Fujitsu x64 install.
- Record the exact Windows binding, registry state, and local binaries.
- Capture USB traffic for at least one successful scan.

Tasks:

1. Export the installed driver package metadata.
2. Copy and hash the local files:
   - `s1100u-x64.dll`
   - `ippi5s1100-x64.dll`
   - `ijl5s1100-x64.dll`
   - `1100_0B00.nal`
   - `oem17.inf`
3. Export the scanner-related registry keys:
   - device instance properties
   - Still Image / USD registration
   - scanner event bindings if present
4. Capture USB descriptors and endpoint layout.
5. Record one or more successful scans under x64 with a USB sniffer.

Deliverables:

- A `baseline/` folder with exported INF, hashes, registry exports, and notes.
- A `traces/` folder with USB captures for:
  - device connect
  - firmware upload
  - idle/status polling
  - scan start
  - image transfer
  - cancel or end-of-page

## Phase 2: Reconstruct the Protocol

Goals:

- Translate the Linux `epjitsu` logic into an explicit protocol document for the S1100.
- Identify what is generic to `epjitsu` and what is S1100-specific.

Tasks:

1. Read the SANE `epjitsu` source and map the major command flow:
   - open/connect
   - firmware load
   - get status
   - identify device
   - configure window and scan mode
   - start scan
   - read blocks
   - post-process image
   - cancel / close
2. Align those steps against the Windows USB traces.
3. Document:
   - command bytes
   - expected responses
   - timeouts
   - transfer sizes
   - sensor/button status bits
4. Confirm whether the S1100 needs any behavior not already cleanly represented in current `epjitsu`.

Deliverables:

- `protocol/s1100-protocol.md`
- `protocol/command-map.md`
- A table of Linux function names to Windows implementation units

## Phase 3: Build a Minimal Cross-Platform Test Harness

Goals:

- Prove the transport and protocol outside the Windows imaging stack.
- Keep debugging focused on the scanner itself.

Tasks:

1. Implement a small native C++ harness that can:
   - enumerate the S1100
   - open the device
   - upload firmware
   - query status
   - start a scan
   - dump raw image bytes to disk
2. Make the harness build for:
   - x64
   - ARM64
3. Validate first on the current x64 PC.

Deliverables:

- `tools/s1100diag/`
- Commands such as:
  - `s1100diag enumerate`
  - `s1100diag load-fw`
  - `s1100diag status`
  - `s1100diag scan --dpi 300 --color`

Exit criteria:

- The harness can complete a real scan on x64 without using Fujitsu's DLL.

## Phase 4: Implement the Core Scanner Library

Goals:

- Create the reusable ARM64-compatible engine that replaces the vendor DLL behavior.

Tasks:

1. Build a scanner core library with modules for:
   - transport
   - firmware upload
   - status and sensors
   - parameter negotiation
   - scan execution
   - image decoding/post-processing
2. Port the image handling logic that Linux performs in software:
   - triplex color handling
   - grayscale conversion
   - binary mode generation
   - resolution normalization
   - padding behavior
3. Add structured logging around every protocol stage.

Deliverables:

- `src/s1100core/`
- Raw and processed scan fixtures under `tests/fixtures/`

## Phase 5: Windows Imaging Integration

Goals:

- Expose the scanner to normal Windows applications on ARM64.

Tasks:

1. Decide the exact integration surface:
   - WIA mini-driver / user-mode driver path preferred
   - STI compatibility only if necessary for older application behavior
2. Create a new ARM64 INF that binds the scanner to the Windows USB scanner stack and registers the new ARM64 mini-driver DLL.
3. Implement the COM-facing layer that translates imaging API requests into core library calls.
4. Support at minimum:
   - device enumeration
   - 300 dpi color
   - 600 dpi color
   - page-present detection
   - cancel
5. Add grayscale and binary modes after color scanning is stable.

Deliverables:

- `driver/arm64/`
- ARM64 INF
- ARM64 mini-driver DLL

## Phase 6: ARM64 Bring-Up and Validation

Goals:

- Prove the driver on a real Windows ARM64 machine.

Tasks:

1. Install the driver package on Windows ARM64.
2. Validate:
   - device enumeration
   - firmware upload
   - first scan
   - repeated scans
   - unplug/replug recovery
   - low-power / resume behavior
3. Compare output against:
   - x64 vendor driver output
   - Linux `epjitsu` output
4. Measure throughput, memory use, and failure cases.

Exit criteria:

- Stable repeated scanning on ARM64 at 300 dpi and 600 dpi color.
- No dependency on x64 emulation for scanner function.

## Phase 7: Packaging, Signing, and Distribution

Goals:

- Make the package installable on ARM64 systems with minimal manual steps.

Tasks:

1. Prepare a driver package layout.
2. Use test signing during development.
3. Evaluate production signing path later.
4. Do not redistribute Fujitsu firmware unless licensing is explicitly cleared.
5. Prefer a local firmware discovery or extraction step if redistribution remains legally unsafe.

Deliverables:

- Install instructions
- Signed test package
- Firmware handling policy

## Immediate Next Actions

1. Create a `baseline/` directory and export all local x64 package artifacts.
2. Capture a USB trace of one known-good scan on this x64 machine.
3. Write the first protocol notes by aligning the trace to `epjitsu`.
4. Scaffold the native diagnostic harness.
5. Reproduce a full scan on x64 without Fujitsu's DLL.
6. Only then start WIA/STI integration work.

## Key Risks

### Protocol ambiguity

The Linux backend is useful, but some S1100 behavior may only be obvious from the working Windows trace.

Mitigation:

- Treat USB captures from the x64 Fujitsu stack as the ground truth.

### Firmware licensing

The firmware blob is copyrighted Fujitsu material.

Mitigation:

- Do not assume redistribution is safe.
- Build the project so firmware can be supplied locally by the user.

### Imaging stack complexity

Windows imaging integration may introduce COM and driver packaging issues unrelated to scanner protocol.

Mitigation:

- Keep the protocol engine separate from the Windows imaging layer.
- Validate the engine with a CLI harness first.

### ARM64-specific surprises

A user-mode port is lower risk than kernel work, but ARM64 still requires native builds, packaging, and test coverage.

Mitigation:

- Keep the transport/core library clean and architecture-neutral.
- Stand up x64 and ARM64 builds from the start.

## Success Criteria

- The S1100 enumerates and scans on Windows ARM64.
- The implementation does not depend on Fujitsu x64 binaries.
- The driver stack stays user-mode where possible.
- Firmware handling is legally defensible.
- The codebase is testable with a standalone diagnostic tool and a Windows imaging integration layer.
