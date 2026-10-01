# Local verification — 2026-10-01

Player 2.3.0 educational preview, Windows x64, Windows PowerShell 5.1.

- Build: passed.
- 48 storage/session/recovery/preflight/WAV checks: passed.
- 97 icon checks, including 24 distinct animation frames, a fixed centered triangle and clear outer borders: passed. Preview visually inspected on light/dark backgrounds at 16, 24 and 32 pixels.
- Real loopback lifecycle: record → pause → resume → save → restart → save → exit; exit code 0.
- Stopped UI verifies a final file exists, its open command is enabled, no pending sessions remain in the test folder and the animation timer is stopped.
- Seven-part synthetic recovery verifies duration, one final MKV, unchanged archived originals and stage notifications.
- Collision, missing-part, active-writer, partial-header and missing-encoder recovery failures preserve original data.
- Preflight checks cover an unavailable folder, free-space boundaries, unknown space, missing/invalid/hung FFmpeg, decoder failure and a real successful AAC/MKV roundtrip; only the private probe is cleaned.
- Live explicit WAV fallback: failed FFmpeg check produces no automatic recording; user-equivalent WAV selection records, saves and stops the animation with verified files and no pending crash session.
- Single/multi-part WAV results, repeated restart discovery, malformed/incomplete/escaping-path metadata, modified audio, marker collisions, failed marker storage and a process crash before marker publication: passed.

The full `Run-Checks.ps1` command was also run through the local MegaProg verification runner: exit code 0, no timeout. This is local verification, not a published release.

Tests write private artifacts to ignored `Verification` and `Backups` folders. The real loopback check genuinely records the system output; other checks use synthetic data. No private recordings/logs are included in this report.

Not covered: physical power loss, a full real disk, clean-machine deployment, extended endurance, all DPI/theme combinations, and Explorer restart. A passed short test does not claim those scenarios are validated.
