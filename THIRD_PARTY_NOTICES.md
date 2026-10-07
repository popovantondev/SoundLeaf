# Third-party components / Drittanbieter / Сторонние компоненты

SoundLeaf invokes FFmpeg as a separate process; it does not link to its libraries. Offline 3.0.5 includes minimal FFmpeg **9.0.2**, Opus **1.6.1** and LAME **3.100**, built from unmodified upstream sources. GPL, version3 and nonfree options are disabled. FFmpeg reports **LGPL 2.1 or later**. Network protocols are disabled.

The corresponding source kit `ffmpeg-9.0.2-corresponding-source.zip` is included under `tools` and separately available on the [same release page](https://github.com/popovantondev/SoundLeaf/releases/tag/v3.0.5). It contains original source archives, build script/instructions, configuration evidence and unmodified component notices. Only `encoder/build-minimal.sh` has an additional LGPL-2.1-or-later permission; SoundLeaf's application rights remain unchanged.

See `tools/licenses`: FFmpeg LGPL, Opus BSD-style terms, LAME LGPL, MinGW-w64 runtime/headers, winpthreads and GCC runtime terms including the Runtime Library Exception. Independently licensed components are not governed by SoundLeaf's original-source restrictions.

The system .NET Framework, Windows Forms, System.Drawing and WASAPI are used; no separate .NET runtime is bundled. The application source archive excludes the encoder binary. [FFmpeg licensing](https://ffmpeg.org/legal.html) · [Build instructions](encoder/README.md). This inventory is not legal advice or patent clearance.

Deutsch: FFmpeg, passende Quellen und unveränderte Lizenztexte sind enthalten. Ihre Lizenzen gelten unabhängig von den Rechten an SoundLeaf; die App selbst hat keine Open-Source-Lizenz.

Русский: FFmpeg, соответствующие исходники и неизменённые лицензии вложены. Их условия независимы от прав на SoundLeaf; само приложение не под открытой лицензией.
