# Changelog

## 3.0.0 — SoundLeaf educational preview, 2026-10-01

- Rename the application and executable; keep existing installation/recordings and shared single-instance protection.
- Retain default MKV/AAC 192; add OGG/Opus 24 mono, MP3, M4A/AAC and ADTS AAC, per-format bitrates and immutable session profiles.
- Stream additional formats through one bounded encoder across WAV segments; rebuild failed outputs from original PCM and verify before publication.
- Add atomic profile/result metadata and profile-aware recovery compatible with intentional WAV completion.
- Add leaf-themed tray panel, light/dark/system themes, immediate German/Russian/English selection and separate leaf executable emblem; existing status icons remain.
- Add background recording library, safe save-folder selection and resaving existing recordings without modifying originals.
- Expand codec, queue failure, corrupt metadata and panel rendering checks; retain all earlier storage/readiness/recovery checks.

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
