<p align="center"><img src="assets/tray-preview.png" width="640" alt="Player recording, pause, stop, error and saving icons"></p>

# Player

[Deutsch](docs/README.de.md) · [Русский](docs/README.ru.md) · English

**Strictly an educational project · Windows 11 · x64 · Preview 2.2.0**

Player is a C# learning project exploring Windows system audio, background processing, durable file storage and a system-tray interface. It is not a professional recording solution.

## Features

- Capture the default Windows output device through WASAPI loopback; microphone capture is not implemented.
- Start, pause, resume and stop from the tray. The current interface is Russian; documentation is available in three languages.
- Green play: recording. Yellow bars: paused. Black square: stopped. Rotating ring: saving. Red: failure or unfinished previous sessions while stopped.
- Durable WAV backup with live AAC encoding and one verified final MKV per session.
- Saving stages and an explicit final-file path; open the last verified output from the menu.
- Detect unfinished sessions and rebuild them from finalized or partial WAV parts. Originals are archived only after full verification.
- User-level startup option without administrator rights.

Recording starts automatically when Player is launched. Only capture audio you are entitled to record, with any required participant consent. Silence is normal and is not treated as a fault.

## Run

The current package is source-only; no public binary release is available yet. Build `Player.next.exe`, place a compatible AAC-enabled FFmpeg at `tools/ffmpeg.exe` beside it, then launch the EXE from a writable folder. For normal use, rename the tested candidate to `Player.exe`.

Files are stored beside the executable: `Recordings`, `logs`, `Backups`, and temporary `RecoveryWork`. Keep the entire application folder together. Re-enable startup after moving the folder so the saved path is updated.

## Build and verify

Use Windows PowerShell 5.1 and the Windows .NET Framework compiler. No Python, Node.js or separate .NET SDK is needed to build Player.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-Launcher.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Run-Checks.ps1
```

The full check requires `tools/ffmpeg.exe` and a working output device: its final integration test genuinely records system output in a private `Verification` folder. Other tests use synthetic data. Build and icon checks can be run separately without FFmpeg. The build script never replaces an installed EXE.

## Limitations and privacy

Practical testing is currently limited to the owner's Windows computer. Physical power loss, a full real disk and multi-hour endurance have not been tested. Periodic flushing reduces but cannot eliminate loss on hardware failure. Changing or disconnecting the output device requires a new session. A spinning icon is not a percentage indicator.

Unfinished older sessions are tracked separately from the latest successful result. Recovery refuses missing/duplicate parts, active writers, incompatible formats and existing output names. A failed attempt leaves originals intact and may retain work files. Recovery archives in `Backups/RecoveredSessions` are not removed by the existing 24-hour cleanup of `Backups/RecordingParts`.

No cloud upload or telemetry is implemented. Recordings, logs and local paths are private: do not attach them to public reports without review. Screenshots must use demonstration data.

## Documentation

- User guide: [English](docs/Guide-en.html) · [Deutsch](docs/Guide-de.html) · [Русский](docs/Guide-ru.html)
- [Architecture](docs/ARCHITECTURE.md) · [Verification](docs/VERIFICATION.md) · [Changelog](CHANGELOG.md)
- [Catalog integration](docs/CATALOG.md) · [Rights](RIGHTS.md) · [Third-party components](THIRD_PARTY_NOTICES.md)

Public source visibility is for educational/portfolio inspection, not an open-source license. Original code rights and third-party component terms are separate. A binary release will require an additional distribution review, especially for FFmpeg.
