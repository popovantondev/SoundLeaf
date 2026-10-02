param([string]$VendorZip,[string]$Encoder)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
. (Join-Path $root 'File-Hash.ps1')
if(!$VendorZip){$VendorZip=Join-Path $root 'artifacts\installer-toolchain\ffmpeg-9.0.1-essentials_build.zip'}
if(!$Encoder){$Encoder='C:\Users\Public\Player\tools\ffmpeg.exe'}
$zipHash=Get-SoundLeafSha256 $VendorZip
$exeHash=Get-SoundLeafSha256 $Encoder
if($zipHash -ne 'FEC81AE03971D9DD4BE3EBE02E263BD2EC1D789483F931BDBA5F5715E65DA2E9' -or $exeHash -ne '72A489ECCD008C2EC2C0A5856C5C75BC3D8BBFA90166C4566865C246445E6AA3'){throw 'Unreviewed encoder input; refusing to run it.'}
$output=Join-Path $root ('artifacts\EncoderReview-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($VendorZip)
try{
    $reader=New-Object IO.StreamReader($archive.GetEntry('ffmpeg-9.0.1-essentials_build/README.txt').Open())
    try{$readme=$reader.ReadToEnd()}finally{$reader.Dispose()}
    $reader=New-Object IO.StreamReader($archive.GetEntry('ffmpeg-9.0.1-essentials_build/LICENSE').Open())
    try{$license=$reader.ReadToEnd()}finally{$reader.Dispose()}
    $sourceEntries=@($archive.Entries | Where-Object {$_.FullName -match '\.(c|h|cpp|cc|cxx|sh|patch|diff)$'} | ForEach-Object {$_.FullName})
}finally{$archive.Dispose()}
if($license -notmatch 'Version 3, 29 June 2007' -or $readme -notmatch 'bf1b838f2a'){throw 'Vendor license/source identity changed.'}
$versions=$readme.Substring($readme.IndexOf("external libraries' versions:")).Split("`n") | Select-Object -Skip 2 | Where-Object {$_.Trim()}
function Run-Encoder([string]$arguments){
    $info=New-Object Diagnostics.ProcessStartInfo($Encoder,$arguments)
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    $process=New-Object Diagnostics.Process
    $process.StartInfo=$info
    try{
        [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(3000)){$process.Kill();$process.WaitForExit();throw 'Own synthetic encoder check exceeded three seconds.'}
        $result=$stdout.Result+$stderr.Result
        if($process.ExitCode -ne 0){throw ('Synthetic encoder failure: '+$result)}
        return $result
    }finally{$process.Dispose()}
}
$build=Run-Encoder '-version'
if($build -notmatch '--enable-gpl' -or $build -match '--enable-nonfree'){throw 'Unexpected encoder license flags.'}
$profiles=@(
 @{Name='mkv/aac192';Extension='mkv';Arguments='-c:a aac -b:a 192k -f matroska'},
 @{Name='ogg/opus24 mono';Extension='ogg';Arguments='-c:a libopus -b:a 24k -vbr on -application voip -ac 1 -ar 48000 -f ogg'},
 @{Name='mp3/192';Extension='mp3';Arguments='-c:a libmp3lame -b:a 192k -f mp3'},
 @{Name='m4a/aac192';Extension='m4a';Arguments='-c:a aac -b:a 192k -f ipod'},
 @{Name='aac/adts192';Extension='aac';Arguments='-c:a aac -b:a 192k -f adts'}
)
$checks=@()
foreach($profile in $profiles){
    $file=Join-Path $output ('synthetic.'+$profile.Extension)
    Run-Encoder ('-hide_banner -loglevel error -nostdin -n -f lavfi -i anullsrc=r=48000:cl=stereo -t 0.5 '+$profile.Arguments+' "'+$file+'"') | Out-Null
    Run-Encoder ('-hide_banner -loglevel error -nostdin -xerror -i "'+$file+'" -f null NUL') | Out-Null
    if((Get-Item -LiteralPath $file).Length -eq 0){throw 'Empty synthetic output.'}
    $checks+=[pscustomobject]@{Profile=$profile.Name;SyntheticEncode=$true;FullDecode=$true;ProcessLimitMs=3000}
}
$report=[pscustomobject]@{Checked=(Get-Date -Format o);ArchiveSha256=$zipHash;ExecutableSha256=$exeHash;License='GPL-3.0-or-later reported by binary; vendor LICENSE is GPLv3';FfmpegSourceCommit='bf1b838f2ab88b4f8fd83443325c782ea0e0f7fa';SourceFilesInVendorArchive=$sourceEntries;ExternalLibraryVersions=@($versions | ForEach-Object {$_.Trim()});ConfigureAndCompiler=$build;SyntheticProfiles=$checks;RedistributionCleared=$false;Reason='Matching external-library sources, their complete notices, patches and build-control materials have not been verified. The FFmpeg core commit and version list alone do not establish complete corresponding source.';NoCapture=$true;InstalledEncoderChanged=$false}
[IO.File]::WriteAllText((Join-Path $output 'review.json'),($report|ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
Write-Output "PASS pinned identity, GPL flags and five synthetic encode/full-decode profiles. Offline redistribution NOT CLEARED. Report: $output\review.json"
