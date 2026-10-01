# Architecture

Educational C# application using a Windows Forms message loop and native Windows audio APIs. The executable has no console window.

- `src/AudioCapture.cs`: WASAPI loopback, PCM formats and device lifecycle.
- `src/WaveStorage.cs`: durable WAV headers, checkpoints, segmented storage and audio timeline.
- `src/RecordingSession.cs`: recording worker, bounded encoder queue, export verification and state updates.
- `src/StartupChecks.cs`: worker-thread folder/space checks and bounded AAC/MKV encode/decode probe in a private temporary directory.
- `src/WavReceipt.cs`: atomic completion metadata for intentional WAV-only sessions, validated against relative paths, lengths, modification times and WAV headers.
- `src/SessionRecovery.cs`: grouped session discovery, protected source snapshot, staging, verification and source archival.
- `src/PlayerApp.cs`: tray/menu, user startup, save-stage presentation and unfinished-session count.
- `src/PlayerIcons.cs`: code-native artwork for the executable and cached animation frames.
- `tests/Tests.cs` and `tests/ReadinessTests.cs`: synthetic storage/session/recovery/preflight/WAV tests; real loopback lifecycles use `--verify-live` and `--verify-wav-fallback`.

The worker owns the capture device. UI updates are dispatched to the UI thread. A separate encoder queue isolates capture from FFmpeg latency; durable WAV is authoritative if live encoding fails. Publication uses no-overwrite renames. Recovery writes a private copy before touching original file locations; originals are archived only after final decoding and duration verification.

An old failed session and a newer successful session are independent results. The menu must never equate a stopped state with completion of every session in the recording directory.

`MkvWithWavBackup` is the default. Failed encoder preflight never opens the audio device automatically; the user must select `WavOnly`. This mode skips the encoder entirely, retains segmented verified WAV parts and publishes `State/Sessions/<session>.json` by flush plus no-overwrite rename. Session updates carry the mode, result kind and all final paths. Recovery ignores only validated, deliberately completed WAV sessions. A missing, malformed, changed or incomplete marker keeps the audio eligible for recovery; marker publication failure is an error, not success. Keep state metadata with the recordings when moving the application.
