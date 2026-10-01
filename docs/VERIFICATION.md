# Local verification — 2026-10-01

SoundLeaf 3.0.1 educational preview, Windows x64, Windows PowerShell 5.1.

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
