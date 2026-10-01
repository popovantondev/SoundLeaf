# Architecture

Educational C# application using a Windows Forms message loop and native Windows audio APIs. The executable has no console window.

- `src/AudioCapture.cs`: WASAPI loopback, PCM formats and device lifecycle.
- `src/WaveStorage.cs`: durable WAV headers, checkpoints, segmented storage and audio timeline.
- `src/RecordingSession.cs`: recording worker, bounded encoder queue, export verification and state updates.
- `src/SessionRecovery.cs`: grouped session discovery, protected source snapshot, staging, verification and source archival.
- `src/PlayerApp.cs`: tray/menu, user startup, save-stage presentation and unfinished-session count.
- `src/PlayerIcons.cs`: code-native artwork for the executable and cached animation frames.
- `tests/Tests.cs`: synthetic storage/session/recovery tests; real loopback lifecycle lives in `--verify-live`.

The worker owns the capture device. UI updates are dispatched to the UI thread. A separate encoder queue isolates capture from FFmpeg latency; durable WAV is authoritative if live encoding fails. Publication uses no-overwrite renames. Recovery writes a private copy before touching original file locations; originals are archived only after final decoding and duration verification.

An old failed session and a newer successful session are independent results. The menu must never equate a stopped state with completion of every session in the recording directory.
