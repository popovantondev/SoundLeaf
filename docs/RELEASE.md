# SoundLeaf 3.0.5 — offline release

Strictly an educational Windows system-audio project. Preview tag: `v3.0.5`; app/file version: 3.0.5 / 3.0.5.0. EXEs are unsigned.

## Assets

- `SoundLeaf-3.0.5-Setup.exe`: offline, current-user installation without administrator rights; no app launch or startup enablement.
- `SoundLeaf-3.0.5-win-x64.zip`: complete portable runtime, FFmpeg 9.0.2, matching sources and notices.
- `SoundLeaf-3.0.5-source.zip`: committed application source without private data or binaries.
- `ffmpeg-9.0.2-corresponding-source.zip`: separately downloadable corresponding-source kit, also embedded in the runtime.
- `SHA256SUMS.txt`: exact hashes, not code signing.

## Preparation and checks

1. Commit intended changes without rewriting history. Run `Test-PublicSource.ps1 -History`.
2. For changed runtime inputs run the complete `Run-Checks.ps1` including genuine MKV/Opus/WAV capture. Do not interrupt active recording. Packaging-only releases may reuse an exact fully checked 3.0.5 capsule; `Prepare-OfflineRelease.ps1` rejects changed runtime/test inputs or encoder bytes.
3. Build the encoder source/runtime kit with `encoder/Build-Bundle.ps1`; retain licenses and the narrow build-script permission. This does not change the application's restrictive rights.
4. In Windows PowerShell run `Prepare-OfflineRelease.ps1 -EncoderBundle <verified bundle>`. Run `Test-Setup.ps1 -ReleaseDirectory <output>` without online flags, then `Test-OfflineRelease.ps1 -ReleaseDirectory <output>`.
5. Review three-language wizard renders, retained fixtures and the website. Run `Run-PublicChecks.ps1` and `Build-Pages.ps1`. Root Pages files are generated from `docs/Guide-*.html` and `docs/guide.css`; keep them synchronized.
6. Upload only the five named assets, not the private artifact folder. Verify downloaded release assets against SHA-256 before publishing. Enable GitHub Pages from main, root. Only then add catalog metadata and exact asset sizes/hashes with the profile generator.

The hosted Windows workflow runs source/hash/build/icon smoke checks, not audio capture or full release validation. Local installer checks include non-elevated engine fixtures and the actual asynchronous offline wizard handler, not a human end-user walkthrough or clean-machine deployment. Existing recordings and installation are not migrated.

## Release description

Deutsch: Lernprojekt für Windows-Systemaudio. Tray-Steuerung, geprüfte Ausgaben, MKV/AAC 192 als Standard; optional OGG/Opus 24, MP3, M4A/AAC und AAC. Offline-Setup und portable Version enthalten FFmpeg, passende Quellen und Lizenzen. Keine Administratorrechte. Deutsch / Русский / English. Unsignierte Vorschau; keine professionelle Aufnahmelösung.

Русский: Учебное приложение для записи системного звука Windows. Управление из трея и проверка готовых файлов; основной MKV/AAC 192, дополнительные OGG/Opus 24, MP3, M4A/AAC и AAC. Автономный установщик и portable включают FFmpeg, соответствующие исходники и лицензии. Без администратора. Deutsch / Русский / English. Неподписанная предварительная версия, не профессиональное средство записи.

English: Educational Windows system-audio application. Tray controls and verified output; MKV/AAC 192 by default, optional OGG/Opus 24, MP3, M4A/AAC and AAC. Offline Setup and portable runtime include FFmpeg, corresponding sources and notices. No administrator rights. Deutsch / Русский / English. Unsigned preview, not a professional recording solution.
