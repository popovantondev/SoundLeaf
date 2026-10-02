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

## Follow-up — complete run and installation

After the recorder exited, the complete direct run passed with version 3.0.5 and the minimal encoder: 74 core checks, 114 artwork checks, six shell-cache checks, 42,408 UI assertions, 252 panel renders and genuine MKV/Opus/WAV capture lifecycles. The fixture rendered inaudible digital silence, not a conversation. A manual muted-desktop/listening test and user acceptance remain separate from this run. Simulated DPI/layout checks are not a claim of physical testing on three different monitors.

The first complete attempt stopped at a popup check. Diagnostics identified external application focus loss with `AppFocusChange` while the panel itself stayed visible. Modeled geometry checks now disable popup autoclose only in the test matrix; native functional checks retain normal autoclose and focus behavior. Another attempt passed its checks but was correctly denied a complete receipt because its source commit changed during execution. Only the final unchanged-input run authorized installation.

The installed EXE and encoder match the final receipt. The previous executable and encoder are preserved in a local backup; all 23 recording/state metadata entries and the exact settings hash stayed unchanged. Embedded artwork and fresh shell readback match the natural leaf. No application was automatically launched and nothing was uploaded. The separate offline installer remains pending.
