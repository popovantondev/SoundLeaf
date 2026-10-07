#!/usr/bin/env bash
# SPDX-License-Identifier: LGPL-2.1-or-later
# Copyright (c) 2026 popovantondev.
# This script alone may be used, modified and redistributed under LGPL 2.1 or later.
# No warranty. See licenses/ffmpeg/COPYING.LGPLv2.1 in the corresponding-source kit.
# This permission does not apply to SoundLeaf's application source.
set -euo pipefail
# Run in an isolated MSYS2 UCRT64 login shell. Never install into the runtime app.
work="$(cygpath -u "${1:?Dedicated build folder required}")"
cd "$work"
prefix="$work/prefix"
mkdir -p build prefix evidence
export PATH="/ucrt64/bin:/usr/bin:$PATH"
export CC=gcc CXX=g++ AR=ar RANLIB=ranlib
export CFLAGS='-O2 -std=gnu11 -fno-ident'
export LDFLAGS='-static -static-libgcc'
export PKG_CONFIG_PATH="$prefix/lib/pkgconfig"
export GIT_DIR=/dev/null
printf '%s  %s\n' \
  8c3850283eb25fa026482078a04051e0be17347b09ef81a0849bec15a96e002e sources/ffmpeg-9.0.2.tar.xz \
  6ffcb593207be92584df15b32466ed64bbec99109f007c82205f0194572411a1 sources/opus-1.6.1.tar.gz \
  ddfe36cab873794038ae2c1210557ad34857a4b6bdc515785d1da9e175b1da1e sources/lame-3.100.tar.gz | sha256sum -c -
pacman -Q > evidence/toolchain-packages.txt
gcc --version > evidence/compiler.txt
for component in ffmpeg-9.0.2 opus-1.6.1 lame-3.100; do
  if [[ -e "build/$component" ]]; then echo "Existing component directory; overwrite refused: $component" >&2; exit 1; fi
done
tar -xf sources/opus-1.6.1.tar.gz -C build
tar -xf sources/lame-3.100.tar.gz -C build
tar -xf sources/ffmpeg-9.0.2.tar.xz -C build
jobs="${BUILD_JOBS:-4}"
cd build/opus-1.6.1
./configure --prefix="$prefix" --disable-shared --enable-static --disable-doc --disable-extra-programs
make -j"$jobs"
make check -j"$jobs"
make install
cd ../lame-3.100
./configure --prefix="$prefix" --disable-shared --enable-static --disable-frontend --disable-decoder --disable-nasm --without-libiconv
make -j"$jobs"
make install
cd ../ffmpeg-9.0.2
./configure \
  --prefix="$prefix" --arch=x86_64 --target-os=mingw32 \
  --cc=gcc --cxx=g++ --pkg-config=pkg-config --pkg-config-flags=--static \
  --extra-cflags="-O2 -I$prefix/include -fno-ident" \
  --extra-ldflags="-L$prefix/lib -static -static-libgcc -Wl,-Map,$work/evidence/ffmpeg-link.map" \
  --disable-autodetect --disable-network --disable-everything \
  --disable-shared --enable-static --disable-doc --disable-debug \
  --disable-ffplay --disable-ffprobe --enable-ffmpeg \
  --enable-libopus --enable-libmp3lame \
  --enable-avcodec --enable-avformat --enable-avfilter --enable-swresample \
  --enable-indev=lavfi \
  --enable-protocol=file,pipe,concat \
  --enable-demuxer=matroska,ogg,mp3,mov,aac,wav,concat,pcm_s16le,pcm_s24le,pcm_s32le,pcm_f32le,pcm_f64le \
  --enable-muxer=matroska,ogg,mp3,ipod,adts,null,wav,pcm_s16le,pcm_f32le \
  --enable-encoder=aac,libopus,libmp3lame,pcm_s16le,pcm_s24le,pcm_s32le,pcm_f32le,pcm_f64le \
  --enable-decoder=aac,aac_fixed,aac_latm,mp3,mp3float,opus,libopus,pcm_s16le,pcm_s16be,pcm_s24le,pcm_s32le,pcm_f32le,pcm_f64le,pcm_u8,pcm_alaw,pcm_mulaw \
  --enable-parser=aac,aac_latm,mpegaudio,opus \
  --enable-filter=aresample,anull,aformat,anullsrc,atrim,asetpts,concat,volume,sine,pan \
  --enable-bsf=aac_adtstoasc,extract_extradata
make -j"$jobs"
make install
cp config.h ffbuild/config.mak ffbuild/config.log "$work/evidence/"
objdump -p "$prefix/bin/ffmpeg.exe" > "$work/evidence/pe-imports.txt"
"$prefix/bin/ffmpeg.exe" -version > "$work/evidence/ffmpeg-version.txt" 2>&1
"$prefix/bin/ffmpeg.exe" -L > "$work/evidence/ffmpeg-license.txt" 2>&1
echo 'BUILD COMPLETE: prefix/bin/ffmpeg.exe; packaging and SoundLeaf verification still required.'
