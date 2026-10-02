param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[string]$EncoderZip,[switch]$DownloadEncoder)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
. (Join-Path $root 'File-Hash.ps1')
$manifest=Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'setup-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$setup=Join-Path $ReleaseDirectory $manifest.File
if((Get-SoundLeafSha256 $setup) -ne $manifest.Sha256){throw 'Setup SHA mismatch.'}
$assemblies=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll')
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $root 'installer\SoundLeafSetup.cs'),[Text.Encoding]::UTF8)) -ReferencedAssemblies $assemblies
$assembly=[Reflection.Assembly]::LoadFile($setup)
$test=Join-Path $ReleaseDirectory ('SetupTests-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($test)
if([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run installer fixtures from a non-elevated shell.'}
if($DownloadEncoder){$EncoderZip=[SoundLeafSetup.Engine]::Download($test,[Action[string]]{param($message)})}
$uninstaller=Join-Path $test 'Uninstall.exe'
$input=$assembly.GetManifestResourceStream('uninstaller.exe');$output=[IO.File]::Create($uninstaller)
try{$input.CopyTo($output)}finally{$input.Dispose();$output.Dispose()}
$target=Join-Path $test 'Installed'
$stream=$assembly.GetManifestResourceStream('payload.zip')
try{[SoundLeafSetup.Engine]::Install($target,$stream,$uninstaller,$EncoderZip,$false)}finally{$stream.Dispose()}
if((Get-SoundLeafSha256 (Join-Path $target 'SoundLeaf.exe')) -ne $manifest.RuntimeSha256){throw 'Installed app differs.'}
if($EncoderZip -and (Get-SoundLeafSha256 (Join-Path $target 'tools\ffmpeg.exe')) -ne [SoundLeafSetup.Engine]::EncoderExeHash){throw 'Installed encoder differs.'}
$record=Join-Path $target 'Recordings\preserve-test.txt'
[void][IO.Directory]::CreateDirectory((Split-Path -Parent $record));[IO.File]::WriteAllText($record,'Synthetic preservation fixture. Not audio.')
$changed=Join-Path $target 'RIGHTS.md';[IO.File]::AppendAllText($changed,' Synthetic changed-file fixture.')
$blocked=$false; $stream=$assembly.GetManifestResourceStream('payload.zip')
try{[SoundLeafSetup.Engine]::Install($target,$stream,$uninstaller,$null,$false)}catch{$blocked=$true}finally{$stream.Dispose()}
if(!$blocked){throw 'Nonempty destination was not refused.'}
foreach($name in @('../escape.txt','C:\escape.txt','/escape.txt','a/../../escape.txt')){
    $blocked=$false;try{[SoundLeafSetup.Engine]::Child($target,$name)|Out-Null}catch{$blocked=$true};if(!$blocked){throw 'Unsafe path accepted.'}
}
$corrupt=Join-Path $test 'corrupt.zip';[IO.File]::WriteAllText($corrupt,'not an encoder')
$blocked=$false;try{[SoundLeafSetup.Engine]::AddEncoder($corrupt,$test)}catch{$blocked=$true};if(!$blocked){throw 'Corrupt encoder accepted.'}
$lock=[IO.File]::Open((Join-Path $target 'SoundLeaf.exe'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
$blocked=$false;try{[SoundLeafSetup.Engine]::Uninstall($target,$false)}catch{$blocked=$true}finally{$lock.Dispose()}
if(!$blocked -or !(Test-Path $record)){throw 'Locked-app uninstall safety failed.'}
[SoundLeafSetup.Engine]::Uninstall($target,$false)
if(!(Test-Path $record) -or !(Test-Path $changed) -or (Test-Path (Join-Path $target 'SoundLeaf.exe'))){throw 'Uninstall preservation failed.'}
$key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey([SoundLeafSetup.Engine]::RegistryPath)
$shortcut=Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\SoundLeaf.lnk'
$integrationTested=$false
if(!$key -and !(Test-Path -LiteralPath $shortcut)){
    $integration=Join-Path $test 'Integrated'
    $stream=$assembly.GetManifestResourceStream('payload.zip')
    try{[SoundLeafSetup.Engine]::Install($integration,$stream,$uninstaller,$null,$true)}finally{$stream.Dispose()}
    try{
        $installedKey=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey([SoundLeafSetup.Engine]::RegistryPath)
        try{if(!$installedKey -or $installedKey.GetValue('InstallLocation') -ne $integration -or !(Test-Path $shortcut)){throw 'Per-user integration missing.'}}finally{if($installedKey){$installedKey.Dispose()}}
    }finally{[SoundLeafSetup.Engine]::Uninstall($integration,$true)}
    $integrationTested=$true
}elseif($key){$key.Dispose()}
foreach($lang in 0,1,2){
    $form=New-Object SoundLeafSetup.SetupForm($false)
    try{
        foreach($control in $form.Controls){if($control -is [Windows.Forms.ComboBox]){$control.SelectedIndex=$lang}}
        $image=New-Object Drawing.Bitmap($form.Width,$form.Height)
        try{$form.DrawToBitmap($image,$form.ClientRectangle);$image.Save((Join-Path $test ('wizard-'+$lang+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
    }finally{$form.Dispose()}
}
# The compiler uses the asInvoker manifest; also inspect the emitted setup PE bytes.
$bytes=[IO.File]::ReadAllBytes($setup)
$text=[Text.Encoding]::UTF8.GetString($bytes)
if($text -notmatch 'requestedExecutionLevel level="asInvoker"'){throw 'asInvoker manifest missing.'}
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $root 'tests\EmbeddedIconTests.cs') -Raw -Encoding UTF8)
$icons=[SoundLeaf.EmbeddedIconTests]::Verify($setup,(Join-Path $root 'assets\SoundLeaf.ico'))
$manifest.Tested=$true
$manifest | Add-Member -NotePropertyName Checked -NotePropertyValue (Get-Date -Format o) -Force
$manifest | Add-Member -NotePropertyName Scope -NotePropertyValue ('Non-elevated engine install/uninstall fixtures, active-file refusal, data preservation, unsafe paths, corrupt ZIP refusal, asInvoker and '+$icons+' icons; three offscreen wizard renders. No real application launch or end-user wizard interaction. Encoder extraction tested: '+[bool]$EncoderZip+'; actual pinned download tested: '+[bool]$DownloadEncoder+'; per-user shortcut/registry integration tested: '+$integrationTested) -Force
[IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'setup-manifest.json'),($manifest|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Output "PASS setup engine fixtures, data preservation, safety refusals, asInvoker and $icons icons. Not a real end-user wizard run. Fixtures retained: $test"
