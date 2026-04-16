# ScanSnap S1100 Linux Efforts Deep Analysis

## Scope

This document focuses on Linux-side repositories and artifacts that materially help a Windows ARM64 ScanSnap S1100 driver effort.

I limited the cloned set to repositories that are directly relevant to one of these areas:

- low-level `epjitsu` protocol implementation
- S1100-specific patches or history
- firmware blob provenance and packaging
- USB trace decoding and protocol reconstruction
- Linux install or packaging layers that reveal operational assumptions

I did not treat generic Linux scanning applications as primary sources unless they contained ScanSnap-specific protocol knowledge.

## Local Clone Set

Cloned under:

`C:\Users\liveu\source\repos\KovaForge\ScanSnapS1100\third_party\linux-research`

Directly relevant repositories:

- `scansnap-linux`
- `scansnap-firmware`
- `lexruee-scansnap-firmware`
- `scansnap-firmware-git`
- `epjitsu-analyzer`
- `epjitsu-analyzer-logs`
- `sane-project-backends`
- `miurahr-sane-backends.git`
- `general-vendor-products.git`

Notes:

- `miurahr-sane-backends` and `general_vendor_products` had Windows-invalid paths, so they were cloned as bare repositories and analyzed with `git show` and `git diff`.
- Historical repos referenced by older READMEs, including `ckunte/scansnap-firmware` and `ckunte/sfware`, no longer exist on GitHub.

## Executive Assessment

The Linux effort that matters is the `epjitsu` backend in SANE. Everything else is either:

- firmware redistribution
- packaging around SANE
- historical staging before upstream integration
- tooling to decode Windows USB traces

That is good news for Windows ARM64.

It means the main deliverable is not a brand-new kernel driver. The useful Linux work is already user-space protocol logic with calibration, scan control, and image post-processing. That aligns with the current Windows x64 Fujitsu package, which also uses the Windows USB scanner stack plus a user-mode mini-driver DLL.

## Repository Analysis

## 1. `sane-project/backends`

Role:

- Authoritative Linux implementation
- Current best source of protocol, calibration, scan flow, and image pipeline behavior

Why it matters:

- `backend/epjitsu.c`, `backend/epjitsu.h`, and `backend/epjitsu-cmd.h` contain the real protocol and S1100-specific constants
- the current tree already includes S1100 and S1100i support
- the backend is user-space and opens the scanner via `sanei_usb_open`, not a custom kernel USB driver

Key findings:

1. Firmware upload is implemented in user space.
   - `load_fw()` checks device status, skips the first `0x100` bytes of the `.nal` file, uploads `0x10000` bytes, computes its own checksum, and then sends a re-init sequence.
2. The S1100 has its own model selection, calibration constants, scan windows, and image layout.
   - The code distinguishes S1100 from S300/S1300 behavior rather than treating it as a trivial variant.
3. The scan pipeline is explicit and portable.
   - connect device
   - load firmware
   - identify device
   - query sensors/status
   - position paper
   - send calibration windows and calibration data
   - start scan
   - fetch blocks
   - descramble triplex raw data
   - crop padding and page offsets
   - optionally convert to grayscale or binary in software
4. The S1100 uses command behavior that differs from flatbed and duplex models.
   - `scan()` switches S1100 to `0xd6`
   - `sane_read()` uses `0xd3` fetches and `0x43` status reads for S1100
   - `object_position()` drives ingest/eject via `0xd4`
   - `six5()` sends command `0x65` to clear the S1100 button state after scan end
5. The S1100 is physically simple but logically not simple.
   - hardware exposes simplex only
   - hardware supports 300 and 600 dpi color
   - software synthesizes grayscale, lineart, and intermediate output behavior
   - the scanner sends triplex color data that must be descrambled and post-processed

Windows ARM64 implication:

- This repo is the primary source to port.
- The most efficient approach is to translate the `epjitsu` engine into a Windows-native ARM64 user-mode library and then wrap it in Windows imaging integration.

## 2. `miurahr/sane-backends`

Role:

- Historical staging area for S1100 support before clean upstream integration

Why it matters:

- the repo still contains an `epjitsu-s1100` branch
- that branch is likely the branch referenced by the SANE maintainer in old mailing-list discussions

What it contributes:

1. A clear historical diff from unsupported S1100 to working S1100 support.
2. The introduction of S1100-specific calibration payloads and window tables.
3. Early fixes around:
   - buffer overruns
   - trailer padding
   - removal of ADF padding
   - page width and TL_Y handling
   - S1100-specific descrambling and scan flow
4. The introduction of `object_position()` and `six5()`-style logic in the S1100 branch.

What it does not contribute:

- a better modern implementation than current upstream SANE

Assessment:

- `miurahr` is valuable as archaeology, not as the codebase to port directly.
- It helps explain why the current upstream backend looks the way it does.
- Its biggest practical value is the staged patch history, which is useful when you want to understand the minimum set of S1100-specific changes that were necessary.

Windows ARM64 implication:

- use this repo to understand evolution and risk areas
- do not use it as the primary implementation base

## 3. `epjitsu-analyzer`

Role:

- protocol analysis tool for USB packet captures

Why it matters:

- this repo directly addresses the same reverse-engineering problem we have on Windows
- it can parse Windows USBPcap captures as well as Linux packet captures

What it contributes:

1. A parser for the command stream:
   - `0x03` get status flags
   - `0x06` load firmware
   - `0x13` get identifiers
   - `0x16` re-init firmware
   - `0x33` get sensor flags
   - `0x43` get scan status
   - `0xc3`, `0xc4`, `0xc5`, `0xc6`
   - `0xd0`, `0xd1`, `0xd2`, `0xd3`, `0xd4`, `0xd6`
2. A `WindowsUsbPcapPacket` decoder.
   - this is directly useful because our working reference machine is Windows x64
3. A partial interpretation of status and sensor bits.
4. A mechanism to log unknown commands separately.

Assessment:

- This repo does not implement scanning.
- It is still extremely valuable because it shortens the reverse-engineering loop for Windows traffic.

Windows ARM64 implication:

- We should use its command model as a reference when decoding fresh S1100 captures from the local x64 machine.
- Even if we do not reuse the Scala code directly, its structure is a good blueprint for a small trace decoder in C#, Python, or C++.

## 4. `epjitsu-analyzer-logs`

Role:

- decoded example logs generated from Windows driver traffic

Why it matters:

- it contains high-level traces of a real vendor driver session

What it contributes:

1. Concrete initialization sequences.
2. Concrete scan sequences with readable command order.
3. Visibility into commands that the analyzer still labels unknown:
   - `0x24`
   - `0xb0`
   - `0xb2`
   - `0xb3`
   - `0xb4`
   - `0xb5`
   - `0xb6`
   - `0xd8`
   - `0xe1`
4. Evidence that the vendor driver does more than the minimum documented SANE path, at least for S1300i-family captures.

Assessment:

- This repo is not S1100-specific, but it is highly relevant because it demonstrates how to interpret Windows USB traces and what extra command families may exist in vendor traffic.

Windows ARM64 implication:

- We should expect that a clean Windows ARM64 implementation may not need every vendor command if current upstream SANE already achieves stable scanning without them.
- But fresh S1100 traces from our own x64 machine should be checked against this log style so we can identify any S1100-only init or cleanup commands.

## 5. `stevleibelt/scansnap-firmware`

Role:

- firmware blob collection

Why it matters:

- it includes both `1100_0A00.nal` and `1100_0B00.nal`
- the `1100_0B00.nal` hash matches the Windows x64 package installed on the local machine

Important observations:

1. The repo is useful for firmware availability, not for protocol knowledge.
2. The README is outdated and still points users to dead historical repos.
3. The blob set is helpful as a cross-check against local Windows driver packages.

Windows ARM64 implication:

- useful for testing and for verifying blob identity
- not sufficient as a driver source
- firmware redistribution remains a legal question and should not be assumed safe

## 6. `lexruee/scansnap-firmware`

Role:

- firmware blob mirror plus small install Makefile

Why it matters:

- it installs both `1100_0A00.nal` and `1100_0B00.nal` for the S1100
- that suggests practical uncertainty in the Linux community about which firmware revision should be present for a given unit or setup

Assessment:

- this repo is another firmware packaging layer, not protocol logic
- the Makefile is operationally useful because it shows how Linux packagers expected firmware deployment to work

Windows ARM64 implication:

- keep support for local firmware discovery flexible
- do not hard-wire only one firmware filename without validating the exact device behavior

## 7. `bjoern-vh/scansnap-linux`

Role:

- Debian/Ubuntu helper installer around SANE plus firmware/config copy

Why it matters:

- it shows how end users operationalized the already-existing SANE backend

Problems:

1. The README warns that the repo is not fully tested.
2. The S1100 path is inconsistent.
   - `settings/1100.conf` points at `1100_0B00.nal`
   - `drivers/` only contains `1100_0A00.nal`
3. `install.sh` has a visible S1100 mapping typo:
   - `SCANNERS[1100]="1000_0A00"`
4. `install.sh` also contains a malformed `setting=` line.

Assessment:

- this repo should not be treated as a source of truth
- it is useful only as evidence that the Linux install problem was mostly packaging, permissions, and config once `epjitsu` support existed

Windows ARM64 implication:

- do not port anything from this repo except the high-level idea that firmware provisioning and device configuration should be automated for users

## 8. `lexruee/scansnap-firmware-git`

Role:

- Arch PKGBUILD wrapper for firmware packaging

Assessment:

- not useful for protocol
- mildly useful for packaging expectations on Linux

Windows ARM64 implication:

- none beyond confirming that Linux users treated firmware as static data to copy into a known search path

## 9. `general_vendor_products`

Role:

- newer documentation repository referenced by `stevleibelt`

Assessment:

- it is mostly operational documentation
- the focused `fujitsu/scansnap_s1300i` page leans on `sane-airscan`, `ipp-usb`, and service setup
- that is useful as Linux usage guidance, not as driver engineering material

Windows ARM64 implication:

- no direct protocol value
- indirect value only as evidence that users sometimes solve compatibility with service layers rather than low-level driver work

## Technical Findings That Matter For Windows ARM64

## 1. The Linux solution is user-space

This is the single most important architectural takeaway.

The core Linux effort is not a custom kernel USB driver. It is a user-space backend that:

- opens the USB device
- uploads firmware
- speaks the scanner protocol
- decodes image data
- exposes options and images to frontends

That matches the structure of the existing Windows x64 Fujitsu package much more closely than a kernel driver project would.

Conclusion:

- target a Windows ARM64 user-mode mini-driver or service layer first
- do not begin with KMDF or a custom USB bus/filter driver

## 2. Firmware handling is well understood

The Linux code establishes a concrete firmware load procedure:

1. query status
2. if firmware not loaded, open `.nal`
3. skip first `0x100` bytes
4. upload `0x10000` payload bytes with command `0x06`
5. compute checksum in software
6. send re-init using `0x16` then payload `0x80`
7. re-check firmware-loaded bit

Important nuance:

- `.nal` files are `65793` bytes long
- SANE only consumes `256 + 65536` bytes from that container and computes its own checksum from the payload

Conclusion:

- the Windows ARM64 port should treat `.nal` as a container format, not as a raw firmware blob
- firmware upload logic can be ported directly from `load_fw()`

## 3. The S1100 is not just a USB ID addition

The S1100 required:

- dedicated coarse calibration data
- dedicated set-window payloads for 300 and 600 dpi
- model-specific scan geometry
- model-specific color handling
- model-specific page padding rules
- model-specific end-of-scan cleanup

Conclusion:

- the Windows ARM64 implementation should keep an explicit model table and S1100-specific constants
- avoid trying to treat S1100 as a generic S300 variant

## 4. The image pipeline is a major part of the work

The Linux backend does much more than send commands.

It also:

- descrambles raw triplex data into packed RGB
- removes per-line and per-block padding
- removes header/trailer artifacts
- applies y-offset skipping
- scales from hardware resolution to exposed output resolution
- converts RGB to grayscale
- binarizes lineart in software

Conclusion:

- if we only port command I/O and firmware load, the scanner will still not be truly usable
- the real Windows ARM64 deliverable needs the image-processing path too

## 5. Windows trace decoding is already partially solved

`epjitsu-analyzer` and its logs are the strongest bridge from Linux reverse engineering to Windows implementation.

They already provide:

- USBPcap parsing for Windows traces
- command recognition
- human-readable logs
- partial sensor/status decoding
- unknown-command extraction

Conclusion:

- fresh captures from the local x64 S1100 should be collected and decoded next
- this will let us verify whether the Fujitsu Windows DLL does anything SANE does not already model for S1100

## 6. Some vendor commands remain unidentified

The logs repo shows additional commands in vendor traffic, especially for S1300i-family sessions.

That means two things:

1. The vendor stack may perform extra setup that SANE either does not need or has replaced with simpler logic.
2. We should not assume that all vendor-only commands are required for basic S1100 function.

Conclusion:

- trust current upstream SANE as the baseline functional path
- use x64 Windows traces to identify only the extra commands that are actually required for S1100 parity

## What To Port

Port directly or near-directly:

- S1100 command flow from `epjitsu.c`
- model and image structures from `epjitsu.h`
- calibration and scan window tables from `epjitsu-cmd.h`
- software image processing behavior
- trace-decoding ideas from `epjitsu-analyzer`

Reuse as data only:

- firmware blobs from `scansnap-firmware` or local Windows package

Use only as operational inspiration:

- Linux install scripts and packaging repos

## What Not To Port

Do not port:

- Linux packaging scripts
- udev rules
- Linux group membership setup
- SANE frontend-facing option plumbing as-is
- Arch or Debian package metadata

Those solve distribution problems on Linux, not scanning problems on Windows.

## Recommended Windows ARM64 Architecture

1. Build a native scanner core library in C or C++.
2. Implement:
   - USB transport
   - firmware loading
   - sensor/status handling
   - S1100 calibration
   - scan execution
   - block read and descramble
   - grayscale and lineart post-processing
3. Validate with a standalone diagnostic CLI on x64 first.
4. Bring the same core library to ARM64.
5. Add Windows imaging integration on top of that core.
6. Keep firmware external and user-supplied unless licensing is cleared.

## Immediate Next Driver Tasks

1. Capture USBPcap traces from the working x64 machine while scanning with the Fujitsu package.
2. Decode those traces with the command model from `epjitsu-analyzer`.
3. Write a command-by-command comparison between:
   - local Fujitsu x64 traffic
   - current upstream `epjitsu`
4. Implement a minimal x64 diagnostic harness that:
   - opens `USB\\VID_04C5&PID_1200`
   - uploads `1100_0B00.nal`
   - starts a 300 dpi color scan
   - dumps raw blocks to disk
5. Port the harness to ARM64 only after the x64 replacement path works.

## Bottom Line

The Linux-side work already answers the hardest strategic question.

The S1100 does not need a new kernel driver design. It needs a Windows-native user-mode port of the same kind of protocol engine that SANE already implements, plus Windows imaging integration and careful firmware handling.

The highest-value code to study is:

- official `sane-project/backends` `epjitsu`
- `miurahr` `epjitsu-s1100` branch for history
- `epjitsu-analyzer` and `epjitsu-analyzer-logs` for Windows trace decoding

The lowest-value code to study is:

- Linux installer scripts
- package wrappers
- generic Linux service setup
