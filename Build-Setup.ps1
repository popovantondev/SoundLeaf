param([Parameter(Mandatory=$true)][string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
. (Join-Path $root 'File-Hash.ps1')
$manifest=Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'release-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.SourceCommit -ne (& git -C $root rev-parse HEAD).Trim() -or @(& git -C $root status --porcelain).Count) { throw 'Use a clean matching release source commit.' }
$portable=Join-Path $ReleaseDirectory 'Portable\SoundLeaf'
if ((Get-SoundLeafSha256 (Join-Path $portable 'SoundLeaf.exe')) -ne $manifest.ExecutableSha256 -or $manifest.EncoderIncluded) { throw 'Expected verified no-encoder runtime.' }
$output=Join-Path $ReleaseDirectory 'SoundLeaf-3.0.4-Setup-online.exe'
if(Test-Path -LiteralPath $output){throw 'Setup already exists; overwrite refused.'}
$stage=Join-Path $ReleaseDirectory ('SetupBuild-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($stage)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload=Join-Path $stage 'payload.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($portable,$payload)
$provider=New-Object Microsoft.CSharp.CSharpCodeProvider
$code=[IO.File]::ReadAllText((Join-Path $root 'installer\SoundLeafSetup.cs'),(New-Object Text.UTF8Encoding($false,$true)))
function Compile-Setup([string]$destination,[string[]]$resources){
    $options=New-Object CodeDom.Compiler.CompilerParameters
    $options.GenerateExecutable=$true; $options.OutputAssembly=$destination
    $options.CompilerOptions='/target:winexe /platform:x64 /optimize+ /warn:4 /win32manifest:"'+(Join-Path $root 'src\app.manifest')+'" /win32icon:"'+(Join-Path $root 'assets\SoundLeaf.ico')+'"'
    foreach($assembly in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll')){[void]$options.ReferencedAssemblies.Add($assembly)}
    foreach($resource in $resources){[void]$options.EmbeddedResources.Add($resource)}
    $result=$provider.CompileAssemblyFromSource($options,[string[]]@($code))
    $result.Errors | ForEach-Object {Write-Output $_.ToString()}
    if($result.Errors.HasErrors){throw 'Installer compilation failed.'}
}
$uninstall=Join-Path $stage 'uninstaller.exe'
Compile-Setup $uninstall @()
Compile-Setup $output @($payload,$uninstall)
$provider.Dispose()
$sha=Get-SoundLeafSha256 $output
[IO.File]::WriteAllText($output+'.sha256',$sha.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($output)+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
$receipt=[pscustomobject]@{SourceCommit=$manifest.SourceCommit;File=[IO.Path]::GetFileName($output);Sha256=$sha;RuntimeSha256=$manifest.ExecutableSha256;AdminRequired=$false;EncoderIncluded=$false;EncoderDownload='https://github.com/GyanD/codexffmpeg/releases/download/9.0.1/ffmpeg-9.0.1-essentials_build.zip';EncoderArchiveSha256='FEC81AE03971D9DD4BE3EBE02E263BD2EC1D789483F931BDBA5F5715E65DA2E9';Published=$false;Tested=$false}
[IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'setup-manifest.json'),($receipt|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Output "Built per-user ONLINE installer (not yet tested): $output"
