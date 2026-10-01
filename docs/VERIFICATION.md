# Local verification — 2026-10-01

Player 2.2.0 educational preview, Windows x64, Windows PowerShell 5.1.

- Build: passed.
- 33 storage/session/recovery checks: passed.
- 72 icon checks, including 24 distinct animation frames: passed.
- Real loopback lifecycle: record → pause → resume → save → restart → save → exit; exit code 0.
- Stopped UI verifies a final file exists, its open command is enabled, no pending sessions remain in the test folder and the animation timer is stopped.
- Seven-part synthetic recovery verifies duration, one final MKV, unchanged archived originals and stage notifications.
- Collision, missing-part, active-writer, partial-header and missing-encoder recovery failures preserve original data.

Tests write private artifacts to ignored `Verification` and `Backups` folders. The real loopback check genuinely records the system output; other checks use synthetic data. No private recordings/logs are included in this report.

Not covered: physical power loss, a full real disk, clean-machine deployment, extended endurance, all DPI/theme combinations, and Explorer restart. A passed short test does not claim those scenarios are validated.
