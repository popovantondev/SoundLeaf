# Changelog

## 2.3.0 — educational preview, 2026-10-01

- Keep the green triangle fixed at the center of a thicker rounded saving arc; preserve 24 frames and 80 ms timing. Other state icons are unchanged.
- Check durable folder writes, free space and AAC/MKV encode/decode in the recording worker before opening the audio device; bound each preflight FFmpeg process to three seconds.
- Block capture below 256 MiB and show initial space warnings without promising room for a full session.
- Offer explicit WAV-only capture after encoder preflight failure, without an automatic mode switch or encoder launch.
- Publish verified WAV parts with atomic completion receipts, list all result paths and exclude deliberate WAV completion from crash recovery. Metadata failure preserves audio and reports an error.
- Expand verification to 48 core checks, 97 icon checks and real MKV/WAV loopback lifecycles; update affected multilingual documentation.

## 2.2.0 — educational preview, 2026-10-01

- Show concrete save stages and the last verified final-file path.
- Do not label every stopped operation as a saved recording.
- Detect older unfinished sessions separately from the current result.
- Recover complete sessions from finalized and partial WAV files; preserve originals in a separate archive after verification.
- Refuse missing/duplicate parts, active writers, mixed formats and output collisions.
- Add seven recovery tests, a combined verification command, multilingual documentation and draft catalog cards.
- Establish a local Git repository with separate baseline, fixes and documentation commits.

## 2.1.0

- Shared multi-resolution executable/tray artwork; 24-frame saving animation.

## 2.0.0

- Native Windows tray recorder, durable WAV storage, parallel AAC/MKV export, verified concatenation, pause and user-level startup.
