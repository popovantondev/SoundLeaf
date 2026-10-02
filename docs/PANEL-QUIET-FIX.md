# Panel visibility and quiet indication — 2026-10-02

## Changes

The panel now appears and hides immediately. The screenshot-to-live-window handoff, animated native surface and motion timer were removed from the panel visibility path. Native rounded corners and shadow remain; recording/saving tray animation is unchanged.

Current audio indication uses RMS hysteresis: on at 0.004 (about −48 dBFS), off below 0.002 (about −54 dBFS). Below-threshold RMS cannot be overridden by a large peak. Pause, missing device and stale packets reset the indication. The same gate is used by the large and compact meters. Capture bytes, historical signal confirmation, WAV durability and encoder parameters are unchanged.

This is a visual noise floor, not proof that a particular application is playing. Very quiet real playback can be displayed as quiet. Sound below this threshold is still recorded.

## Actual verification

- 74 synthetic/core checks passed, including RMS hysteresis, nonfinite values, unchanged PCM, five format paths, durability and recovery.
- 42,408 UI assertions passed; 252 panel renders. Russian, German and English; light/dark themes; simulated 100/150/200% layout and short screens.
- Native-window checks on the current monitor passed: actual tray anchor, repeated opening, Escape, focus loss, dropdown focus, immediate closure, no animation image allocated, composited rounded corners in both themes.
- 114 artwork checks and six shell-cache checks passed. The separate recording/saving tray states remain intact.
- The application candidate compiled successfully.

## Not yet verified or installed

The installed recorder was running during these checks and was not interrupted or replaced. Genuine post-change recording cycles, a manual muted-desktop/listening test and user acceptance of the new behavior remain pending until it has saved and exited. Simulated DPI/layout checks are not a claim of physical testing on three different monitors. The former runtime verification receipt does not authorize publishing this changed candidate.

The separate minimal encoder build and offline installer are still in preparation; they are not declared ready by this UI report.
