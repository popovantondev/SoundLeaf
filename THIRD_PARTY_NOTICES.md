# Third-party components

Player uses the Windows .NET Framework libraries, Windows Forms, System.Drawing and Windows WASAPI. The current executable relies on the system runtime rather than bundling a separate runtime.

FFmpeg is invoked as a separate process. Its executable is not included in the source repository/archive. The locally tested binary identifies itself as `9.0.1-essentials_build-www.gyan.dev`, built with `--enable-gpl --enable-version3`; `ffmpeg -L` reports GPL version 3 or later. These terms apply independently to that component, not the project's RIGHTS text.

Before distributing a binary package containing FFmpeg, verify the exact build's licenses, corresponding-source availability and notices, and include the required materials. No bundled public binary release is prepared by this source package. A successful local test does not establish distribution compliance.

Die Drittanbieterbedingungen gelten unabhängig von den Projekt-Rechten. FFmpeg ist nicht im Quelltextpaket enthalten. Vor einem Binärpaket die konkrete Build-Version und ihre Verteilungsvoraussetzungen prüfen.

Условия сторонних компонентов не заменяются правами проекта. FFmpeg не включён в исходники. Перед выпуском пакета с FFmpeg требуется отдельная проверка конкретной сборки и материалов для её распространения.
