param([Parameter(Mandatory=$true)][string]$BuildRoot,[Parameter(Mandatory=$true)][string]$CompatibilityReport,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'File-Hash.ps1')
$BuildRoot=[IO.Path]::GetFullPath($BuildRoot)
$binary=Join-Path $BuildRoot 'prefix\bin\ffmpeg.exe'
$binaryHash='3F375EDD024FF119A0919E1B12E7D5DFD9A77B208A773CBDBC777850F0477F6F'
if((Get-SoundLeafSha256 $binary) -ne $binaryHash){throw 'Unreviewed encoder binary; bundle refused.'}
$compatibility=Get-Content -LiteralPath $CompatibilityReport -Raw -Encoding UTF8 | ConvertFrom-Json
if(!$compatibility.Passed -or $compatibility.Full -or $compatibility.Checks -lt 74 -or $compatibility.EncoderSha256 -ne $binaryHash){throw 'Missing matching synthetic compatibility receipt.'}
if((Get-SoundLeafSha256 (Join-Path (Split-Path -Parent $CompatibilityReport) 'synthetic-checks.txt')) -ne $compatibility.LogSha256){throw 'Compatibility log hash mismatch.'}
$inputs=@{
    'ffmpeg-9.0.2.tar.xz'='8C3850283EB25FA026482078A04051E0BE17347B09EF81A0849BEC15A96E002E'
    'opus-1.6.1.tar.gz'='6FFCB593207BE92584DF15B32466ED64BBEC99109F007C82205F0194572411A1'
    'lame-3.100.tar.gz'='DDFE36CAB873794038AE2C1210557AD34857A4B6BDC515785D1DA9E175B1DA1E'
}
foreach($name in $inputs.Keys){if((Get-SoundLeafSha256 (Join-Path $BuildRoot ('sources\'+$name))) -ne $inputs[$name]){throw "Source hash mismatch: $name"}}
$license=[IO.File]::ReadAllText((Join-Path $BuildRoot 'evidence\ffmpeg-license.txt'))
if($license -notmatch 'GNU Lesser General Public' -or $license -match '--enable-(gpl|nonfree|version3)'){throw 'Unexpected encoder license/configuration.'}
$imports=[IO.File]::ReadAllText((Join-Path $BuildRoot 'evidence\pe-imports.txt'))
foreach($match in [regex]::Matches($imports,'DLL Name:\s*(\S+)')){
    if($match.Groups[1].Value -notmatch '^(bcrypt|kernel32|shell32|api-ms-win-crt-[a-z0-9-]+)\.dll$'){throw 'Unexpected external encoder DLL.'}
}
if(!$OutputDirectory){$OutputDirectory=Join-Path $repo ('artifacts\EncoderBundle-9.0.2-'+[Guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Bundle output already exists; overwrite refused.'}
$runtime=Join-Path $OutputDirectory 'Runtime'
$sources=Join-Path $OutputDirectory 'CorrespondingSource'
foreach($folder in @($runtime,$sources,(Join-Path $sources 'sources'),(Join-Path $sources 'evidence'),(Join-Path $runtime 'tools\licenses'))){[void][IO.Directory]::CreateDirectory($folder)}
Copy-Item -LiteralPath $binary -Destination (Join-Path $runtime 'tools\ffmpeg.exe')
foreach($name in $inputs.Keys){Copy-Item -LiteralPath (Join-Path $BuildRoot ('sources\'+$name)) -Destination (Join-Path $sources ('sources\'+$name))}
foreach($name in @('build-minimal.sh','README.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $sources $name)}
foreach($name in @('toolchain-packages.txt','compiler.txt','config.h','config.mak','config.log','ffmpeg-link.map','pe-imports.txt','ffmpeg-version.txt','ffmpeg-license.txt')){
    Copy-Item -LiteralPath (Join-Path $BuildRoot ('evidence\'+$name)) -Destination (Join-Path $sources ('evidence\'+$name))
}
$notices=@{
    'ffmpeg'=@('build\ffmpeg-9.0.2\COPYING.LGPLv2.1','build\ffmpeg-9.0.2\LICENSE.md')
    'opus'=@('build\opus-1.6.1\COPYING','build\opus-1.6.1\AUTHORS')
    'lame'=@('build\lame-3.100\COPYING')
}
foreach($component in $notices.Keys){
    $folder=Join-Path $runtime ('tools\licenses\'+$component);[void][IO.Directory]::CreateDirectory($folder)
    foreach($relative in $notices[$component]){Copy-Item -LiteralPath (Join-Path $BuildRoot $relative) -Destination $folder}
}
foreach($component in @('crt','headers','libgcc','libatomic','winpthreads','libwinpthread')){
    Copy-Item -LiteralPath (Join-Path $BuildRoot ('msys64\ucrt64\share\licenses\'+$component)) -Destination (Join-Path $runtime ('tools\licenses\'+$component)) -Recurse
}
Copy-Item -LiteralPath (Join-Path $runtime 'tools\licenses') -Destination (Join-Path $sources 'licenses') -Recurse
Copy-Item -LiteralPath $CompatibilityReport -Destination (Join-Path $sources 'evidence\compatibility.json')
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$sourceZip=Join-Path $OutputDirectory 'ffmpeg-9.0.2-corresponding-source.zip'
$zipStream=[IO.File]::Open($sourceZip,[IO.FileMode]::CreateNew)
$zip=New-Object IO.Compression.ZipArchive($zipStream,[IO.Compression.ZipArchiveMode]::Create,$true)
try{
    foreach($file in Get-ChildItem -LiteralPath $sources -Recurse -File){
        $relative=$file.FullName.Substring($sources.Length+1).Replace('\','/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
    }
}finally{$zip.Dispose();$zipStream.Dispose()}
Copy-Item -LiteralPath $sourceZip -Destination (Join-Path $runtime 'tools\ffmpeg-9.0.2-corresponding-source.zip')
$receipt=[pscustomobject]@{Encoder='FFmpeg 9.0.2';Sha256=$binaryHash;Bytes=(Get-Item -LiteralPath $binary).Length;SourceArchives=$inputs;SourceKitSha256=(Get-SoundLeafSha256 $sourceZip);UnmodifiedUpstream=$true;NetworkProtocols=$false;SeparateProcess=$true;RuntimeCompatibleSyntheticChecks=$compatibility.Checks;CompatibilitySourceCommit=$compatibility.SourceCommit;LiveWithThisEncoder=$false;InstallerTested=$false;Published=$false}
[IO.File]::WriteAllText((Join-Path $runtime 'tools\encoder-bundle.json'),($receipt|ConvertTo-Json -Depth 4),(New-Object Text.UTF8Encoding($false)))
foreach($file in @($sourceZip,(Join-Path $runtime 'tools\ffmpeg.exe'))){[IO.File]::WriteAllText($file+'.sha256',(Get-SoundLeafSha256 $file).ToLowerInvariant()+'  '+[IO.Path]::GetFileName($file)+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))}
Write-Output "Assembled source and runtime kit, NOT an installer or release approval: $OutputDirectory"
