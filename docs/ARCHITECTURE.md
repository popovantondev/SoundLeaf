# Architecture

Educational C# application using a Windows Forms message loop and native Windows audio APIs. The executable has no console window.

- `src/AudioCapture.cs`: WASAPI loopback, PCM formats and device lifecycle.
- `src/WaveStorage.cs`: durable WAV headers, checkpoints, segmented storage and audio timeline.
- `src/RecordingSession.cs`: recording worker, bounded encoder queue, export verification and state updates.
- `src/StartupChecks.cs`: worker-thread folder/space checks and bounded AAC/MKV encode/decode probe in a private temporary directory.
- `src/WavReceipt.cs`: atomic completion metadata for intentional WAV-only sessions, validated against relative paths, lengths, modification times and WAV headers.
- `src/SessionRecovery.cs`: grouped session discovery, protected source snapshot, staging, verification and source archival.
- `src/SoundLeafApp.cs`: tray/menu, startup compatibility, save stages, folder selection coordination and resaving.
- `src/SoundLeafIcons.cs`: separate vector leaf brand, unchanged state artwork and cached animation frames.
- `src/RecordingProfile.cs`: immutable codec/container profile, settings and atomic per-session profile metadata.
- `src/ProfileExport.cs`: one-stream PCM rebuild, protected original conversion, full verification and no-overwrite publication.
- `src/RecordingCatalog.cs`: atomic result receipts and background metadata-based indexing without mass decoding.
- `src/TrayPanel.cs`: DPI-aware tray-only leaf panel, themes, library and settings.
- `src/TextCatalog.cs`: central Russian, German and English message catalog with immediate language switching.
- `tests/Tests.cs` and `tests/ReadinessTests.cs`: synthetic storage/session/recovery/preflight/WAV tests; real loopback lifecycles use `--verify-live` and `--verify-wav-fallback`.

The worker owns the capture device. UI updates are dispatched to the UI thread. A separate encoder queue isolates capture from FFmpeg latency; durable WAV is authoritative if live encoding fails. Publication uses no-overwrite renames. Recovery writes a private copy before touching original file locations; originals are archived only after final decoding and duration verification.

An old failed session and a newer successful session are independent results. The menu must never equate a stopped state with completion of every session in the recording directory.

The default remains MKV/AAC 192. Additional formats use one continuous live encoder across segmented WAV boundaries. Overflow or encoder failure never blocks capture; a failed final is rebuilt from original WAV PCM. Full decoding and 250 ms duration tolerance precede flush, atomic no-overwrite rename and WAV cleanup. Conversion locks source files, creates a separate output and never modifies originals.

New sessions publish `<id>.profile.json` before opening capture; missing legacy profiles default to MKV/AAC 192, corrupt profiles block recovery. Verified encoded/WAV results add `<id>.result.json` for the library; legacy deliberate WAV receipts keep their own `<id>.json` schema. Duration is only shown when receipt integrity matches size, time and paths. Partial and unverified historical files never become verified by merely opening the list.

Settings stay in the installation's `State/settings.json`. Default recording storage remains unchanged. A custom destination owns its dated folders, `State/Sessions`, `Backups` and `RecoveryWork`; changing it does not move earlier audio. Previously selected folders remain in the library. Recovery currently targets the selected destination; select a previous destination to recover it. Format/folder changes are gated while capture, saving or conversion is active; language/theme changes are immediate.

The old installation name, registry migration matcher and single-instance mutex deliberately retain legacy identifiers. No history rewrite or automatic recording-directory move is performed.

`MkvWithWavBackup` is the default. Failed encoder preflight never opens the audio device automatically; the user must select `WavOnly`. This mode skips the encoder entirely, retains segmented verified WAV parts and publishes `State/Sessions/<session>.json` by flush plus no-overwrite rename. Session updates carry the mode, result kind and all final paths. Recovery ignores only validated, deliberately completed WAV sessions. A missing, malformed, changed or incomplete marker keeps the audio eligible for recovery; marker publication failure is an error, not success. Keep state metadata with the recordings when moving the application.
