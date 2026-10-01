# Local verification — 2026-10-01

SoundLeaf 3.0.0 educational preview, Windows x64, Windows PowerShell 5.1.

- Full `Run-Checks.ps1` executed through the local MegaProg verification runner: exit code 0, `ok=true`, no timeout.
- Build and embedded leaf emblem: passed; installed encoder matches the encoder used for verification.
- 64 core storage/session/recovery/preflight/profile checks: passed, retaining the earlier 48 cases.
- 97 icon checks: passed, including 24 distinct saving frames, fixed centered triangle and clear outer borders. Other tray states remain unchanged.
- 138 panel assertions and 54 renders: German/Russian/English, light/dark, emulated 100/150/200% font/control scales, all three pages. Checks include nonblank output, root bounds, keyboard-focusable controls, Escape/focus-loss hiding, no taskbar button and placement near four edges in three work areas including negative coordinates and a small screen. Representative renders were visually inspected.
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

Physical power loss, a full real disk, clean-machine deployment, many-hour endurance, Explorer restart and real multi-monitor DPI switching remain untested. Scale/placement tests simulate combinations; they do not claim every physical monitor was exercised. The application is system-DPI-aware; a changed Windows scaling configuration can require restarting it. Full FFmpeg decoding cannot recover sound that was never captured.
