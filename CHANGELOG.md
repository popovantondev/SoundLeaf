# Changelog

## 3.0.2 — real audio levels and smooth tray motion, 2026-10-01

- Add read-only peak/RMS monitoring and measured level bars at 95% visual expressiveness; silence/expired packets/pause show a stationary line without changing PCM or encoding.
- Replace the historical “audio received” wording with an explicit detected-signal confirmation and separate device availability; keep silence normal.
- Put vector play/pause/stop icons on the lower control row, with identical pause bars.
- Include the current unfinished session in recording pages, with duration, PCM byte count and a small level display; never offer it as a finished output.
- Animate open/close and reversals on a fixed-bounds premultiplied-alpha surface; stop relayout/resize jitter and release cached/native resources at completion.
- Share the leaf across EXE and native Windows notifications, without changing visible tray states; verify all eight embedded icon images byte-for-byte and refresh the installed path's shell icon.
- Extend meter, pause-glyph, live-card, silence, expiry, notification and motion checks; retain three languages and existing audio/durability tests.
- Guard final installation with a complete-check receipt and matching streaming SHA-256; keep partial and failed runner results distinct from a verified release.

## 3.0.1 — tray panel refinement, 2026-10-01

- Anchor the manually positioned panel to the shell's notification-icon rectangle; retain a saved-click fallback and clamp all sides to the monitor work area.
- Declare Per-Monitor V2 in the manifest; use one explicit geometry/font scale and reposition on DPI changes.
- Add a cancellable 200 ms cached-bitmap growth animation, respecting Windows animation preferences.
- Replace scrolling with compact settings groups and height-dependent recording pages; keep folder and codec details in tooltips and errors in fixed banners.
- Add rounded accessible selectors with bounded upward/downward popups, keyboard navigation and two-stage Escape handling; display lowercase format names.
- Replace the decorative branch and flat brand leaf with one natural vector emblem; preserve all recording/saving state icons and audio behavior.
- Let the Windows 11 compositor smooth window corners instead of an aliased region; reserve sufficient text-line height at enlarged scales.
- Read source and external encoder diagnostics explicitly as UTF-8; preserve Cyrillic and German accents in errors and paths.
- Expand child-boundary, overlap, short-screen, native anchor, popup and animation checks; correct the UI harness to use a real Windows Forms message loop through shutdown.

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
