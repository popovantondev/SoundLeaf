# Local verification — 2026-10-01

SoundLeaf 3.0.2 candidate, Windows x64, Windows PowerShell 5.1.

## 3.0.2 candidate — partial verification; installation deferred

- One full MegaProg 2.0.18 attempt passed 73 core checks, 105 icon checks, 42,226 UI assertions and 252 renders, but its first genuine loopback session received no PCM packets from an idle system output and correctly refused an empty result. Overall runner status was failure, not a completed release; no timeout.
- A second runner invocation explicitly used `Run-Checks.ps1 -NoLive`: the same 73/105/42,226 checks and 252 renders passed, `ok=true`, no timeout. This result is partial and does not certify the genuine capture lifecycle.
- Current-level tests cover PCM16/24/32, float32, signed extremes, nonfinite values, silent flags, missing-packet expiry, pause reset, valid-frame boundaries, unmodified bytes and retained historical signal during silence.
- UI additions cover real-level rendering, 95% expressiveness, quiet-line/timer behavior, current-session pagination, unchanged card identity on telemetry, pause, equal glyph widths, fixed window bounds during motion, open/close/reversal resource cleanup and native shell notification acceptance.
- Eight embedded EXE icon resources match `SoundLeaf.ico` byte-for-byte. A real Windows notification screen capture shows the leaf in both the small source header and main body; visible recording state icons are untouched. Current UI renders were inspected separately from native compositor captures.
- A test-only digital-silence renderer has been added to keep the shared audio engine producing deterministic packets. It is not application code and changes no system volume. The full runner refuses to start that fixture while the installed recorder's mutex exists. It compiles under Windows PowerShell 5.1; its three genuine cycles are still pending.
- `Install-Verified.ps1` refuses partial/no-live verification and requires a matching full-check SHA-256 receipt before replacing anything. The refusal was tested. The installed 3.0.1 recorder was left running; no EXE/recordings/state/registry replacement has been performed for this candidate.

The configured runner is restored to its complete command (without `-NoLive`). Stop/save/exit the installed recorder before the final complete runner and installation. The earlier installed-version baseline below is historical evidence, not a 3.0.2 full-pass claim.

## Previous installed 3.0.1 baseline

- Full `Run-Checks.ps1` executed through the local MegaProg verification runner: exit code 0, `ok=true`, no timeout.
- Build and embedded leaf emblem: passed; installed encoder matches the encoder used for verification.
- 66 core storage/session/recovery/preflight/profile/text checks: passed, retaining all earlier audio cases. UTF-8 encoder diagnostics retain Cyrillic, German accents and a Unicode filename; translated labels never alter `mono` or other codec words.
- 97 icon checks: passed, including 24 distinct saving frames, fixed centered triangle and clear outer borders. Other tray states remain unchanged.
- 28,405 UI assertions and 180 client-panel renders: German/Russian/English, light/dark, emulated 100/150/200% scales, 520/260 logical heights, plus layout checks at 350. Every visible child is checked against its parent and root, interactive siblings for overlap, text containers for line height, and all pages for absence of scrollbars. Short settings groups, long paths/warnings, explicit WAV availability, pagination, conversion and lowercase selectors are covered. Client-only bitmap capture excludes the synthetic non-client caption that could cover text during growth. Representative renders were visually inspected.
- Real native checks on the current desktop: shell lookup of a newly registered notification icon, monitor DPI agreement, first/repeated programmatic opening, work-area containment, dropdown focus restoration, Enter/Space/Tab/arrows, accessible popup/child links, upward opening, auto-closed-popup disposal, Escape hierarchy, actual focus transfer to a separate test window and animation completion/cancellation. Windows Forms tests now run a real message loop through shutdown.
- Two real compositor captures on a black demonstration backdrop, light/dark: corners contain intermediate blended pixels, no window-region mask, no caption or taskbar button. These are screen captures, not DrawToBitmap simulations.
- Genuine system loopback MKV and Opus: record → pause → resume → save → restart → save → exit; exit code 0.
- Genuine explicit WAV fallback: failed preflight opens no capture; WAV selection records and saves verified parts.
- Stopped UI checks: final exists, open command enabled, test folder has no pending session and saving animation has stopped.

## Codec and durability coverage

Each of MKV/AAC, OGG/Opus, MP3, M4A/AAC and AAC/ADTS passes a real FFmpeg preflight, live synthetic encoding across multiple WAV parts, extension/codec check, full decoding, duration tolerance, direct PCM rebuild, collision refusal and receipt-based indexing. Opus 24 is confirmed mono/48 kHz and its temporary output grows before stop.

Forced live-encoder failure successfully rebuilds Opus from WAV. Queue overflow stays bounded and returns without blocking capture. Missing encoder plus failed rebuild retains every WAV. Profile/settings persistence, immutable snapshots, corrupt-settings fallback and corrupt-profile recovery refusal are checked. Conversion creates a decoded MP3 while preserving original bytes; in-place conversion is refused.

Custom-folder intentional WAV completion is not treated as a crash. Library indexing excludes partial files and backup audio, recognizes nested export metadata and never invents historical duration. Startup command matching accepts only the exact installation without extra arguments.

Earlier checks cover unavailable folder, low/unknown free space, missing/invalid/hung FFmpeg, decoding failure, active writers, abrupt process termination, incomplete WAV headers, missing/duplicate parts, receipt failures and publication collisions. Recovery archives original bytes only after successful validation.

## Evidence and limits

Machine-generated runner evidence is stored locally in ignored `.ai-dev/verification.json` and `.ai-dev/verify-0.log`; private synthetic/live artifacts and panel renders are under ignored `Verification`. They are not included in public source packages. This report is local verification, not a public release or clean-machine certification.

Physical power loss, a full real disk, clean-machine deployment, many-hour endurance, Explorer restart and real multi-monitor DPI switching remain untested. Scale/placement tests simulate combinations; they do not claim every physical monitor or expanded hidden-tray configuration was exercised. During simulated rendering, automatic focus hiding is suppressed to avoid unrelated desktop activity invalidating a bitmap; real focus checks use an ordinary panel. The manifest is verified as Per-Monitor V2; mixed-monitor changes are implemented but not physically exercised here. Tray-click handling is checked through its toggle path, not a physical mouse test. The disabled-animation path is exercised without changing the user's Windows setting. Full FFmpeg decoding cannot recover sound that was never captured.
