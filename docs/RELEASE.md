# SoundLeaf 3.0.4 — local release preparation

Strictly an educational Windows system-audio project. This document prepares a GitHub release; it does not announce an upload or a published download.

## Intended assets

- `SoundLeaf-3.0.4-source.zip` and its SHA-256 file: committed source, documentation, tests and repository assets; no encoder, runtime data or Git internals.
- `SoundLeaf-3.0.4-win-x64-no-ffmpeg.zip` and its SHA-256 file: verified x64 EXE, natural leaf, three-language instructions, guides and notices. FFmpeg is explicitly excluded; users provide a compatible build or select WAV-only. The main configured profile remains MKV/AAC 192.
- `release-manifest.json`: source commit, candidate hash, verification time and archive hashes, with `Uploaded=false`.

The portable package is deliberately not a ready-to-record MKV bundle until its external encoder is supplied. Do not describe it as having all dependencies included. The EXE is unsigned; a SHA-256 file is not a signing certificate. No automatic updating or downloading is introduced.

## Prepare locally

1. Commit the intended files without rewriting existing history.
2. For a changed runtime, run complete verification through MegaProg, including real loopback and a hash-pinned receipt with its source commit. For documentation/packaging-only changes, an immutable copy of the already fully verified EXE may be reused: retain its complete runner result and source commit, prove that `src`, icon and build inputs are unchanged, and run separate `Run-ReleaseChecks.ps1` through MegaProg. Do not interrupt a current recording just to repackage unchanged bytes. A partial build or `-NoLive` alone cannot authorize a new runtime.
3. Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Prepare-Release.ps1`. For a preserved runtime capsule add `-RuntimeDirectory .\artifacts\verified-runtime-3.0.4`. The script refuses a dirty tree, changed runtime/candidate, missing full runtime evidence or release checks, and an existing output directory. Its manifest distinguishes current source commit from the earlier runtime verification commit/time.
4. Run `Test-ReleaseArchive.ps1 -Directory <created release folder>`. It audits ZIP membership/checksums, local guide links, exact EXE/version/artwork and absence of FFmpeg/runtime data, and performs clean extraction without launching recording. Review the retained extraction and instructions.
5. Review the GitHub release text below. Repository creation, remote configuration, pushing, tags, uploads and profile-site changes require a separate publication step; no script does them.

The Windows Actions workflow only audits source and performs a Framework build/hash/icon smoke check. Its equivalent command is checked locally; a hosted GitHub run has not happened before publication. No credentials persist, no release artifacts upload, and permissions are read-only.

## Release text — Deutsch

SoundLeaf 3.0.4 ist ein reines Lernprojekt zur Aufnahme von Windows-Systemaudio. Tray-Steuerung, sichere WAV-Sicherung und Prüfung fertiger Dateien, MKV/AAC 192 als Standard, optionale OGG/Opus-, MP3-, M4A/AAC- und AAC-Profile, wählbarer Ordner und Konvertierung ohne Änderung des Originals. Deutsch / Русский / English, helles/dunkles Design und echte Pegelanzeigen. Das portable x64-Paket enthält keinen FFmpeg-Encoder: Einrichtungshinweise beachten oder ausdrücklich WAV wählen. ZIP vollständig entpacken, SHA-256 prüfen; EXE nicht signiert. Nur erlaubte Inhalte mit notwendiger Zustimmung aufnehmen.

## Release text — Русский

SoundLeaf 3.0.4 — сугубо учебный проект записи системного звука Windows. Управление из трея, резервный WAV и проверка готового результата; основной профиль MKV/AAC 192, дополнительные OGG/Opus, MP3, M4A/AAC и AAC. Выбор папки и пересохранение без изменения оригинала. Deutsch / Русский / English, светлая/тёмная темы и индикаторы реального уровня. В переносимом x64-пакете FFmpeg отсутствует: выполните инструкцию установки или явно выберите только WAV. Распакуйте ZIP целиком и проверьте SHA-256; EXE не подписан. Записывайте только разрешённые материалы с необходимым согласием участников.

## Release text — English

SoundLeaf 3.0.4 is strictly an educational Windows system-audio project. Tray controls, WAV backup and verified final output; MKV/AAC 192 by default with optional OGG/Opus, MP3, M4A/AAC and AAC profiles. Select a save folder and convert completed recordings without changing originals. Deutsch / Русский / English, light/dark themes and real level indicators. The portable x64 package excludes FFmpeg: follow setup instructions or explicitly select WAV-only. Extract the whole ZIP and verify SHA-256; the EXE is unsigned. Record only permitted material with required consent.

## Verification boundaries

Runtime baseline: complete 3.0.4 checks on 1 October 2026 (73 core checks, 114 icon checks, six shell-cache checks, 42,306 UI assertions and three loopback modes). On 2 October the owner reported that the latest MKV and long recordings were normal. This is useful user feedback, not an instrumented multi-hour stress test or proof that every Windows discontinuity flag is false. Decode/duration checks do not independently prove completeness against an original conversation. Power loss, an actually full disk and another Windows computer remain outside the tested baseline.

Do not publish private recordings or raw user logs. Repository screenshots are simulated panel renders with demonstration sessions and synthetic level input, not a real recording or measured spectrum. Existing rights remain in `RIGHTS.md`; public source visibility is not an open-source license. Encoder bundling would require a separate review against the exact component's [official license information](https://ffmpeg.org/legal.html).
