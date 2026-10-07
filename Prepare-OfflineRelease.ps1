param([Parameter(Mandatory=$true)][string]$EncoderBundle,[string]$OutputDirectory)
$ErrorActionPreference='Stop'; $root=$PSScriptRoot
. (Join-Path $root 'File-Hash.ps1')
if (@(& git -C $root status --porcelain).Count) {throw 'Commit source before preparing release.'}
& (Join-Path $root 'Test-PublicSource.ps1') -History
$commit=(& git -C $root rev-parse HEAD).Trim(); $short=(& git -C $root rev-parse --short HEAD).Trim()
$capsule=Join-Path $root 'artifacts\verified-runtime-3.0.5'
$checked=Get-Content (Join-Path $capsule 'checked-candidate.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$candidate=Join-Path $capsule 'SoundLeaf.exe'
if(!$checked.Full -or $checked.Runner -ne 'Run-Checks.ps1' -or $checked.Version -ne '3.0.5.0' -or $checked.LiveScenarios.Count -ne 3 -or (Get-SoundLeafSha256 $candidate) -ne $checked.Sha256){throw 'Complete matching 3.0.5 runtime required.'}
& git -C $root diff --exit-code $checked.SourceCommit HEAD -- src tests Build-Launcher.ps1 Build-Icons.ps1 assets/SoundLeaf.ico Run-Checks.ps1 Run-Tests.ps1 Run-IconTests.ps1 Run-UiTests.ps1
if($LASTEXITCODE -ne 0){throw 'Runtime or test inputs changed since full check.'}
$EncoderBundle=[IO.Path]::GetFullPath($EncoderBundle)
$bundle=Get-Content (Join-Path $EncoderBundle 'Runtime\tools\encoder-bundle.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$encoder=Join-Path $EncoderBundle 'Runtime\tools\ffmpeg.exe'
$source=Join-Path $EncoderBundle 'Runtime\tools\ffmpeg-9.0.2-corresponding-source.zip'
if($checked.EncoderSha256 -ne $bundle.Sha256 -or (Get-SoundLeafSha256 $encoder) -ne $checked.EncoderSha256 -or (Get-SoundLeafSha256 $source) -ne $bundle.SourceKitSha256){throw 'Encoder/source kit differs from checked encoder.'}
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts'}
$release=Join-Path $OutputDirectory ('SoundLeaf-3.0.5-release-'+$short)
if(Test-Path -LiteralPath $release){throw 'Release already exists.'}
$portable=Join-Path $release 'Portable\SoundLeaf';[void][IO.Directory]::CreateDirectory($portable)
Copy-Item -LiteralPath $candidate -Destination (Join-Path $portable 'SoundLeaf.exe')
foreach($file in @('RIGHTS.md','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $root $file) -Destination $portable}
Copy-Item -LiteralPath (Join-Path $root 'distribution\README.md') -Destination (Join-Path $portable 'README.md')
foreach($relative in (& git -C $root ls-files -- docs assets)){
    if($relative -match '^docs/(Guide-(ru|de|en)\.html|guide\.css)$' -or $relative -match '^assets/(SoundLeaf\.ico|tray-preview\.png|screenshots/[^/]+\.png|soundleaf-[^/]+\.(png|svg))$'){
        $destination=Join-Path $portable $relative;[void][IO.Directory]::CreateDirectory((Split-Path -Parent $destination));Copy-Item -LiteralPath (Join-Path $root $relative) -Destination $destination
    }
}
Copy-Item -LiteralPath (Join-Path $root 'assets\SoundLeaf.ico') -Destination (Join-Path $portable 'SoundLeaf.ico')
Copy-Item -LiteralPath (Join-Path $EncoderBundle 'Runtime\tools') -Destination (Join-Path $portable 'tools') -Recurse
Copy-Item -LiteralPath $source -Destination (Join-Path $release 'ffmpeg-9.0.2-corresponding-source.zip')
foreach($language in @('de','ru','en')){
    $guide=Join-Path $portable ('docs\Guide-'+$language+'.html');$html=[IO.File]::ReadAllText($guide,[Text.Encoding]::UTF8)
    foreach($name in @('README.ru.md','README.de.md','VERIFICATION.md')){$html=$html.Replace('href="'+$name+'"','href="https://github.com/popovantondev/SoundLeaf/blob/main/docs/'+$name+'"')}
    $html=$html.Replace('href="../README.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/README.md"')
    [IO.File]::WriteAllText($guide,$html,(New-Object Text.UTF8Encoding($false)))
}
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
function Zip-Folder([string]$folder,[string]$destination){
    $stream=[IO.File]::Open($destination,[IO.FileMode]::CreateNew);$zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
    try{foreach($file in Get-ChildItem -LiteralPath $folder -File -Recurse){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$file.FullName.Substring($folder.Length+1).Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal)}}finally{$zip.Dispose();$stream.Dispose()}
}
$zipPath=Join-Path $release 'SoundLeaf-3.0.5-win-x64.zip';Zip-Folder (Split-Path -Parent $portable) $zipPath
$sourceZip=Join-Path $release 'SoundLeaf-3.0.5-source.zip'; & git -C $root archive --format=zip --output=$sourceZip HEAD
if($LASTEXITCODE -ne 0){throw 'Source archive failed.'}
$build=Join-Path $release 'SetupBuild';[void][IO.Directory]::CreateDirectory($build)
$payload=Join-Path $build 'payload.zip';Zip-Folder $portable $payload
$code=[IO.File]::ReadAllText((Join-Path $root 'installer\SoundLeafSetup.cs'),[Text.Encoding]::UTF8)
$code=$code.Replace('public const bool BundledEncoder = false;','public const bool BundledEncoder = true;').Replace('public const string BundledEncoderHash = "";','public const string BundledEncoderHash = "'+$bundle.Sha256+'";').Replace('public const string BundledSourceHash = "";','public const string BundledSourceHash = "'+$bundle.SourceKitSha256+'";').Replace('public const string RuntimeExeHash = "BB2E3DED749FE456FC3D0418D2596B59303975C98340933E3FAE8ECC6B44E7A3";','public const string RuntimeExeHash = "'+$checked.Sha256+'";')
$provider=New-Object Microsoft.CSharp.CSharpCodeProvider
function Compile-Installer([string]$path,[string[]]$resources){
    $options=New-Object CodeDom.Compiler.CompilerParameters;$options.GenerateExecutable=$true;$options.OutputAssembly=$path
    $options.CompilerOptions='/target:winexe /platform:x64 /optimize+ /warn:4 /win32manifest:"'+(Join-Path $root 'src\app.manifest')+'" /win32icon:"'+(Join-Path $root 'assets\SoundLeaf.ico')+'"'
    foreach($assembly in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll')){[void]$options.ReferencedAssemblies.Add($assembly)}
    foreach($resource in $resources){[void]$options.EmbeddedResources.Add($resource)}
    $result=$provider.CompileAssemblyFromSource($options,[string[]]@($code));$result.Errors|ForEach-Object{Write-Output $_.ToString()};if($result.Errors.HasErrors){throw 'Installer compilation failed.'}
}
try{$uninstaller=Join-Path $build 'uninstaller.exe';Compile-Installer $uninstaller @();$setup=Join-Path $release 'SoundLeaf-3.0.5-Setup.exe';Compile-Installer $setup @($payload,$uninstaller)}finally{$provider.Dispose()}
$archives=@();foreach($file in @($zipPath,$sourceZip,(Join-Path $release 'ffmpeg-9.0.2-corresponding-source.zip'))){$hash=Get-SoundLeafSha256 $file;[IO.File]::WriteAllText($file+'.sha256',$hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($file)+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)));$archives+=[pscustomobject]@{File=[IO.Path]::GetFileName($file);Sha256=$hash;Bytes=(Get-Item $file).Length}}
$setupHash=Get-SoundLeafSha256 $setup;[IO.File]::WriteAllText($setup+'.sha256',$setupHash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($setup)+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
$manifest=[pscustomobject]@{Version='3.0.5';SourceCommit=$commit;RuntimeSourceCommit=$checked.SourceCommit;RuntimeSha256=$checked.Sha256;EncoderSha256=$checked.EncoderSha256;EncoderSourceSha256=$bundle.SourceKitSha256;EncoderIncluded=$true;AdminRequired=$false;File=[IO.Path]::GetFileName($setup);Sha256=$setupHash;Published=$false;Tested=$false;Archives=$archives}
[IO.File]::WriteAllText((Join-Path $release 'setup-manifest.json'),($manifest|ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
Write-Output "Prepared OFFLINE installer, portable and source candidates; NOT yet installer-tested or published: $release"
