# Offline release 3.0.5 verification

The application and encoder bytes are unchanged from the fully checked 3.0.5 runtime. Packaging verifies the preserved full receipt, exact hashes and unchanged compiled runtime/test inputs; it does not substitute a new partial check for runtime testing.

Runtime baseline: 74 synthetic audio/storage/profile checks, 42,408 UI assertions with modeled screen/DPI cases, artwork checks and three genuine WASAPI capture scenarios (MKV, Opus and WAV). Genuine capture used a synthetic silence renderer, not a private conversation. See [runtime verification](VERIFICATION.md).

Offline installer verification runs from a non-elevated Windows PowerShell shell. It covers installation, per-user registration/shortcut, locked-app refusal, existing-folder refusal, unsafe paths, corrupt encoder rejection, data preservation on uninstall, runtime/encoder/source hashes, asInvoker metadata and eight icon images. It renders all three wizard languages from the compiled setup assembly and exercises the actual asynchronous installation-button handler in an isolated folder. No recorder is launched by setup tests. This is not a manual end-user walkthrough or a clean-machine deployment test.

Archive checks verify portable runtime/source hashes, original FFmpeg/Opus/LAME source archives, matching source-kit inclusion, narrow script-license permission, mandatory notices, duplicate/unsafe entries and exclusion of private runtime data. SHA256SUMS.txt is generated from checked files. Only explicitly named release assets are uploaded; retained test folders are private.

Website checks use the rendered DE/RU/EN pages in a browser. Desktop and 390-pixel emulated viewport were inspected; language switches work, images load and no horizontal overflow was detected at that narrow viewport. This is browser viewport emulation, not testing on a physical phone. Existing app UI and capture/storage behavior are unchanged.

Unsigned educational preview. Checksum verification is not signing, legal advice, patent clearance or a guarantee against every undiscovered defect. RIGHTS.md retains the application restrictions; only the encoder build script has the expressly approved LGPL exception.
