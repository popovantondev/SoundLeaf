# FFmpeg redistribution review — 2 October 2026

## Result / Ergebnis / Результат

**Not cleared for bundled/offline public distribution.** This is an engineering evidence review, not a legal opinion or a finding that the supplier violates a license. The existing online Setup remains unchanged; no encoder was replaced, no recording was started/stopped, and nothing was published.

**Офлайн-комплект не подтверждён.** Техническая работа проверенной сборки подтверждена, комплектность материалов для её повторного распространения — нет. **Offline-Verteilung nicht freigegeben:** technische Prüfungen bestanden, passende Quelltext-/Build-Materialien nicht vollständig verifiziert.

## Identified binary and available evidence

- GyanD `9.0.1-essentials_build-www.gyan.dev`, the same encoder already tested with SoundLeaf.
- Vendor ZIP SHA-256: `FEC81AE03971D9DD4BE3EBE02E263BD2EC1D789483F931BDBA5F5715E65DA2E9`. Matched the published GitHub release asset digest and local archive.
- `ffmpeg.exe` SHA-256: `72A489ECCD008C2EC2C0A5856C5C75BC3D8BBFA90166C4566865C246445E6AA3`.
- Vendor LICENSE: GNU GPL version 3 text. Binary flags include `--enable-gpl --enable-version3`, without `--enable-nonfree`; binary license output identifies GPL version 3 or later.
- Vendor README identifies FFmpeg core commit `bf1b838f2ab88b4f8fd83443325c782ea0e0f7fa`, configure output and a list of external-library versions, including Opus and LAME. The source commit was resolved through FFmpeg's GitHub API.
- The checked ZIP contains binaries, documentation and LICENSE/README, but no `.c`, `.h`, `.cpp`, `.cc`, `.cxx`, `.sh`, `.patch` or `.diff` files. File-extension scanning is an inventory aid, not a proof of completeness.
- Supplier repository tree at release commit `4646599` contains README and funding metadata, not a matching source/build-materials package. The release assets contain binary build variants, not a complete corresponding-source archive. Public compilation discussion gives general MSYS2/MinGW guidance, not the required complete package for this exact binary.

## Why the evidence is insufficient

The [GPLv3 text](https://www.gnu.org/licenses/gpl-3.0.html), especially sections 1 and 6, describes corresponding-source obligations for covered binaries. [FFmpeg's official legal page](https://ffmpeg.org/legal.html) also stresses matching sources and separately included libraries. Its checklist concerns library linking and is not automatically a checklist for our separate-process arrangement.

A core-source link and dependency version list do not establish that all applicable dependency sources, changes, build-control materials and required notices for this static binary are available and correctly identified. Examples needing mapping include libopus, LAME, x264/x265 and the other enabled libraries. General-purpose tools and qualifying system libraries may be exempt; no blanket demand to redistribute Windows or every compiler tool is made.

Do not label the build illegal, relicense SoundLeaf, copy a generic source archive and call it matching, or claim that an LGPL variant automatically removes source obligations. The current original-rights text explicitly separates third-party terms; it is unchanged.

## Technical checks actually performed

`Review-Encoder.ps1` runs directly from Windows PowerShell 5.1. It checks the pinned input hashes before execution, reads vendor evidence and tests artificial 0.5-second silence in:

- MKV / AAC 192 kbit/s;
- OGG / Opus 24 kbit/s, mono, 48 kHz, speech/VBR;
- MP3 192 kbit/s;
- M4A / AAC 192 kbit/s;
- AAC / ADTS 192 kbit/s.

All five encoded nonempty outputs and fully decoded successfully. Every child process had a three-second timeout and no console. These are synthetic compatibility checks, not a live-capture/endurance test or licensing clearance. Reports and synthetic files are local ignored artifacts, not release assets.

## Remaining path to an offline release

Either obtain and verify the complete matching materials for the existing GyanD build, or create a separately controlled minimal Windows encoder with only the required audio features and keep its source versions, patches, notices and build scripts together. BtbN publishes Docker-based build recipes and GPL/LGPL variants, but this review has not matched a complete source set to a replacement binary; no automatic switch was made.

A new encoder needs the existing format, fallback, duration, pause, segmented-WAV, recovery and genuine loopback checks before installation or release. MKV/AAC 192 remains unchanged. Publication must provide accessible matching source/materials and notices using an appropriate license-compliance route; exact presentation must be reviewed for that chosen build. No complete matching source kit has been obtained for the original GyanD binary.

## Separately controlled minimal build — 2026-10-02

An isolated non-administrator MSYS2 UCRT64 toolchain built FFmpeg 9.0.2 from its original archive with Opus 1.6.1 and LAME 3.100. No upstream patches, `--enable-gpl`, `--enable-version3` or `--enable-nonfree` were used. The resulting binary reports LGPL 2.1 or later; its SHA-256 is `3F375EDD024FF119A0919E1B12E7D5DFD9A77B208A773CBDBC777850F0477F6F`, size 6,524,928 bytes. Network protocols are disabled. PE imports contain only Windows system DLLs; the linker map records the linked archives, including MinGW runtime, winpthreads and GCC support libraries.

All 74 existing synthetic audio/storage/profile checks passed with this binary in a separate test tree. These include five formats, full decoding, segmented WAV, pause, encoder failure, queue overflow, rebuilding from PCM and recovery. No installed encoder was replaced and no real capture device was opened by this run. A second harness run exposed a legacy Framework path-length limit in deeply nested recovery fixtures; using a short dedicated test root passed, and the new harness rejects an excessively long root before running.

The bundle recipe retains the three exact original source archives, complete audio build script, package/compiler versions, generated configuration, linker map, import list and unmodified upstream/runtime license notices. A matching partial compatibility receipt is required. Source materials are available offline in the runtime kit as well as a separate source ZIP. See [minimal build instructions](../encoder/README.md).

This inventory is not a legal opinion, patent clearance, reproducible-bit-for-bit claim or authorization to publish. After the recorder exited, a fresh full SoundLeaf 3.0.5 run passed, including three genuine MKV/Opus/WAV loopback lifecycles with this encoder. The checked local installation now includes the encoder, source ZIP and unmodified license notices. Public installer integration and download/source presentation remain pending. The offline installer is not yet ready; the old online installer candidate remains separate and unchanged.

Sources checked: [GyanD 9.0.1 release](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.1), [Gyan build descriptions](https://www.gyan.dev/ffmpeg/builds/), [supplier compilation discussion](https://github.com/GyanD/codexffmpeg/issues/91), [BtbN build instructions](https://github.com/BtbN/FFmpeg-Builds/blob/master/README.md), GNU/FFmpeg license materials above. No vendor was contacted and no issue, email or external write was sent.

## Public offline release — 2026-10-07

The minimal encoder bytes are unchanged. The reviewed source kit now includes the expressly approved LGPL-2.1-or-later grant solely for `encoder/build-minimal.sh`; SoundLeaf's application rights remain unchanged. Offline Setup and portable 3.0.5 include the exact encoder, corresponding-source ZIP and unmodified notices. Setup checks binary/source hashes before publishing installation. Non-elevated installer fixtures, actual asynchronous wizard installation and 402 archive assertions passed.

The [public release](https://github.com/popovantondev/SoundLeaf/releases/tag/v3.0.5) separately hosts the matching source kit beside the binary downloads, and all five public assets were downloaded again and verified by size/SHA-256. Earlier pending statements above describe the dated pre-publication state. This is a technical distribution inventory, not a legal opinion or patent clearance. No upstream vendor contact was made.
