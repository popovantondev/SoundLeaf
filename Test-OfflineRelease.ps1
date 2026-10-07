param([Parameter(Mandatory=$true)][string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$ReleaseDirectory=[IO.Path]::GetFullPath($ReleaseDirectory)
. (Join-Path $PSScriptRoot 'File-Hash.ps1')
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$manifest=Get-Content (Join-Path $ReleaseDirectory 'setup-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(!$manifest.Tested -or !$manifest.EncoderIncluded -or $manifest.AdminRequired -or $manifest.Version -ne '3.0.5'){throw 'Checked offline setup required.'}
if((Get-SoundLeafSha256 (Join-Path $ReleaseDirectory $manifest.File)) -ne $manifest.Sha256){throw 'Setup changed after verification.'}
$checks=0
function Check([bool]$condition,[string]$message){if(!$condition){throw $message};$script:checks++}
function Read-Entry($zip,[string]$name){
    $entry=$zip.GetEntry($name);if(!$entry){throw ('Missing ZIP entry: '+$name)}
    $input=$entry.Open();$memory=New-Object IO.MemoryStream
    try{$input.CopyTo($memory);return ,$memory.ToArray()}finally{$input.Dispose();$memory.Dispose()}
}
function Hash-Bytes([byte[]]$bytes){$hash=[Security.Cryptography.SHA256]::Create();try{return ([BitConverter]::ToString($hash.ComputeHash($bytes))).Replace('-','')}finally{$hash.Dispose()}}
foreach($archive in $manifest.Archives){
    $path=Join-Path $ReleaseDirectory $archive.File
    Check ((Get-SoundLeafSha256 $path) -eq $archive.Sha256) 'Archive hash mismatch.'
    Check ((Get-Item $path).Length -eq $archive.Bytes) 'Archive size mismatch.'
    $zip=[IO.Compression.ZipFile]::OpenRead($path)
    try{
        $names=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        foreach($entry in $zip.Entries){
            Check ($entry.FullName -notmatch '(^/|\\|(^|/)\.\.(/|$)|:|(^|/)(Recordings|State|logs|Backups|artifacts|\.git)/)') 'Unsafe or private archive entry.'
            Check ($names.Add($entry.FullName)) 'Duplicate archive entry.'
        }
        if($archive.File -eq 'SoundLeaf-3.0.5-win-x64.zip'){
            Check ((Hash-Bytes (Read-Entry $zip 'SoundLeaf/SoundLeaf.exe')) -eq $manifest.RuntimeSha256) 'Portable runtime mismatch.'
            Check ((Hash-Bytes (Read-Entry $zip 'SoundLeaf/tools/ffmpeg.exe')) -eq $manifest.EncoderSha256) 'Portable encoder mismatch.'
            Check ((Hash-Bytes (Read-Entry $zip 'SoundLeaf/tools/ffmpeg-9.0.2-corresponding-source.zip')) -eq $manifest.EncoderSourceSha256) 'Portable corresponding source mismatch.'
            foreach($lang in 'de','ru','en'){Check ($null -ne $zip.GetEntry('SoundLeaf/docs/Guide-'+$lang+'.html')) 'Portable guide missing.';Check ($null -ne $zip.GetEntry('SoundLeaf/assets/screenshots/'+$lang+'-control-light.png')) 'Guide screenshot missing.'}
        }elseif($archive.File -eq 'SoundLeaf-3.0.5-source.zip'){
            Check ($null -eq $zip.GetEntry('tools/ffmpeg.exe')) 'Encoder binary in application source.'
            $script=[Text.Encoding]::UTF8.GetString((Read-Entry $zip 'encoder/build-minimal.sh'))
            Check ($script.Contains('SPDX-License-Identifier: LGPL-2.1-or-later')) 'Build script permission missing.'
        }else{
            $script=[Text.Encoding]::UTF8.GetString((Read-Entry $zip 'build-minimal.sh'))
            Check ($script.Contains('SPDX-License-Identifier: LGPL-2.1-or-later')) 'Source kit permission missing.'
            Check ($null -ne $zip.GetEntry('licenses/ffmpeg/COPYING.LGPLv2.1')) 'Source kit license missing.'
            foreach($source in @{
                'ffmpeg-9.0.2.tar.xz'='8C3850283EB25FA026482078A04051E0BE17347B09EF81A0849BEC15A96E002E'
                'opus-1.6.1.tar.gz'='6FFCB593207BE92584DF15B32466ED64BBEC99109F007C82205F0194572411A1'
                'lame-3.100.tar.gz'='DDFE36CAB873794038AE2C1210557AD34857A4B6BDC515785D1DA9E175B1DA1E'
            }.GetEnumerator()){Check ((Hash-Bytes (Read-Entry $zip ('sources/'+$source.Key))) -eq $source.Value) 'Upstream source archive mismatch.'}
        }
    }finally{$zip.Dispose()}
}
$lines=@($manifest.Sha256.ToLowerInvariant()+'  '+$manifest.File)
foreach($archive in $manifest.Archives){$lines+=($archive.Sha256.ToLowerInvariant()+'  '+$archive.File)}
[IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'SHA256SUMS.txt'),($lines -join "`n")+"`n",(New-Object Text.UTF8Encoding($false)))
$receipt=[pscustomobject]@{Passed=$true;Checks=$checks;Checked=(Get-Date -Format o);SourceCommit=$manifest.SourceCommit;SetupHash=$manifest.Sha256;Scope='ZIP membership, hashes, source permissions, pinned upstream archives and notices; no clean-machine test'}
[IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'offline-verification.json'),($receipt|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Output "PASS $checks offline release assertions; checksums generated. No publication performed."
