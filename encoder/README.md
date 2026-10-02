# Minimal Windows encoder build

This builds a separate `ffmpeg.exe` process for SoundLeaf. SoundLeaf does not link to its libraries. FFmpeg 9.0.2, Opus 1.6.1 and LAME 3.100 are built from unmodified pinned upstream archives. No GPL, version3 or nonfree configure option is enabled. FFmpeg reports LGPL 2.1 or later. Upstream notices and component terms remain independent of SoundLeaf's original-source rights.

## Build environment

Use an isolated MSYS2 UCRT64 installation in a short ASCII path. The tested base archive was:

- `https://github.com/msys2/msys2-installer/releases/download/2026-09-27/msys2-base-x86_64-20260927.tar.xz`
- SHA-256 `EA2F31A0B6ADE63914CE441FFB022F0F6AA96982BFEFA2326460A26D5FB01322`

Initialize its login shell, update its private package database/runtime with `pacman -Syu`, then install `make mingw-w64-ucrt-x86_64-gcc mingw-w64-ucrt-x86_64-nasm mingw-w64-ucrt-x86_64-pkgconf`. No administrator rights or changes to Windows PATH are needed. Package signatures must remain enabled. The source kit records the exact versions used; installing the newest packages is not a promise of byte-identical output.

Place the three original source archives in `sources` under a fresh dedicated work folder. Their names and SHA-256 values are checked by `build-minimal.sh`. Start an UCRT64 login shell and run:

```sh
bash /path/to/build-minimal.sh C:/DedicatedBuildFolder
```

The script refuses existing component directories rather than overwriting another build. It builds static audio libraries, runs Opus's tests, then builds FFmpeg. Output is `prefix/bin/ffmpeg.exe`; configuration, compiler/package versions, linker map and PE imports are saved under `evidence`. Complete configure arguments are in the script and generated configuration record. No upstream source patches were applied.

The build is intentionally limited to the audio formats and synthetic inputs used by SoundLeaf. It is not a general video converter. Hardware capture is still performed by SoundLeaf's unchanged Windows capture path. Network protocols are disabled in this encoder.

## Bundle and release

Run `Test-Compatibility.ps1 -Encoder C:\DedicatedBuildFolder\prefix\bin\ffmpeg.exe` from Windows PowerShell. It copies the test inputs into its own unique folder, runs synthetic checks with that encoder and writes a partial compatibility receipt. It does not open a real capture device or change the installed encoder.

`Build-Bundle.ps1 -BuildRoot C:\DedicatedBuildFolder -CompatibilityReport <path-to-compatibility.json>` assembles the exact binary, original source archives, build script, configuration evidence and unmodified notices. It requires the matching partial receipt, refuses unrecognized binary or source hashes and existing output directories. It does not install the encoder, stop the recorder, change the application or authorize publication.

The source kit must accompany any public release of this binary, remain downloadable beside it on the same release host, and retain upstream terms. The runtime kit also includes a copy for offline access. Static runtime notices include MinGW-w64, winpthreads and GCC's Runtime Library Exception. This technical inventory is not a legal opinion or a patent clearance. See [FFmpeg's official guidance](https://ffmpeg.org/legal.html).

After bundling, run the complete SoundLeaf verification with this encoder and the exact application candidate, including genuine MKV/Opus/WAV capture after the installed recorder has exited. A synthetic compatibility run alone is not an offline installer or release approval.
