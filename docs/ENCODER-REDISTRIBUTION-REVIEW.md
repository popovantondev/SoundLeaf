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

A new encoder would need the existing format, fallback, duration, pause, segmented-WAV, recovery and genuine loopback checks before installation or release. MKV/AAC 192 remains unchanged. Publication must provide accessible matching source/materials and notices using an appropriate license-compliance route; exact presentation must be reviewed for that chosen build. No source archive has yet been prepared or labelled complete.

Sources checked: [GyanD 9.0.1 release](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.1), [Gyan build descriptions](https://www.gyan.dev/ffmpeg/builds/), [supplier compilation discussion](https://github.com/GyanD/codexffmpeg/issues/91), [BtbN build instructions](https://github.com/BtbN/FFmpeg-Builds/blob/master/README.md), GNU/FFmpeg license materials above. No vendor was contacted and no issue, email or external write was sent.
