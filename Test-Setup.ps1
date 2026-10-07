param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[string]$EncoderZip,[switch]$DownloadEncoder)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$ReleaseDirectory=[IO.Path]::GetFullPath($ReleaseDirectory)
. (Join-Path $root 'File-Hash.ps1')
$manifest=Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'setup-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$setup=Join-Path $ReleaseDirectory $manifest.File
if((Get-SoundLeafSha256 $setup) -ne $manifest.Sha256){throw 'Setup SHA mismatch.'}
$assemblies=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll')
$code=[IO.File]::ReadAllText((Join-Path $root 'installer\SoundLeafSetup.cs'),[Text.Encoding]::UTF8)
if($manifest.EncoderIncluded){
    if($EncoderZip -or $DownloadEncoder){throw 'Offline tests must not fetch external encoder.'}
    $code=$code.Replace('public const bool BundledEncoder = false;','public const bool BundledEncoder = true;').Replace('public const string BundledEncoderHash = "";','public const string BundledEncoderHash = "'+$manifest.EncoderSha256+'";').Replace('public const string BundledSourceHash = "";','public const string BundledSourceHash = "'+$manifest.EncoderSourceSha256+'";').Replace('public const string RuntimeExeHash = "BB2E3DED749FE456FC3D0418D2596B59303975C98340933E3FAE8ECC6B44E7A3";','public const string RuntimeExeHash = "'+$manifest.RuntimeSha256+'";')
}
Add-Type -TypeDefinition $code -ReferencedAssemblies $assemblies
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
if($manifest.EncoderIncluded){
    if((Get-SoundLeafSha256 (Join-Path $target 'tools\ffmpeg.exe')) -ne $manifest.EncoderSha256 -or (Get-SoundLeafSha256 (Join-Path $target 'tools\ffmpeg-9.0.2-corresponding-source.zip')) -ne $manifest.EncoderSourceSha256){throw 'Offline installed encoder/source mismatch.'}
    foreach($file in @('ffmpeg/COPYING.LGPLv2.1','opus/COPYING','lame/COPYING','crt/COPYING','libgcc/COPYING.RUNTIME','winpthreads/COPYING')){if(!(Test-Path -LiteralPath (Join-Path $target ('tools\licenses\'+$file)))){throw 'Missing bundled license.'}}
}
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
if($manifest.EncoderIncluded -and $integrationTested){
    # Exercise the actual published assembly and its asynchronous button handler.
    $wizardRoot=Join-Path $test 'WizardInstalled'
    $form=[Activator]::CreateInstance($assembly.GetType('SoundLeafSetup.SetupForm'),[object[]]@($false))
    $flags=[Reflection.BindingFlags]'Instance,NonPublic'
    try{
        $form.ShowInTaskbar=$false;$form.Opacity=0;$form.Show()
        $type=$form.GetType()
        $type.GetField('folder',$flags).GetValue($form).Text=$wizardRoot
        if($type.GetField('encoder',$flags).GetValue($form).Enabled){throw 'Offline encoder checkbox must be fixed.'}
        $type.GetField('action',$flags).GetValue($form).PerformClick()
        $deadline=[DateTime]::UtcNow.AddSeconds(40)
        while($type.GetField('busy',$flags).GetValue($form) -and [DateTime]::UtcNow -lt $deadline){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 20}
        if(!$type.GetField('completed',$flags).GetValue($form)){throw ('Actual wizard failed: '+$type.GetField('status',$flags).GetValue($form).Text)}
        if((Get-SoundLeafSha256 (Join-Path $wizardRoot 'tools\ffmpeg.exe')) -ne $manifest.EncoderSha256){throw 'Actual wizard installed wrong encoder.'}
        $installedKey=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey([SoundLeafSetup.Engine]::RegistryPath)
        try{if($installedKey.GetValue('DisplayVersion') -ne '3.0.5.0'){throw 'Actual installer version mismatch.'}}finally{$installedKey.Dispose()}
    }finally{
        $form.Hide();$form.Dispose()
        if(Test-Path -LiteralPath (Join-Path $wizardRoot 'installation.json')){[SoundLeafSetup.Engine]::Uninstall($wizardRoot,$true)}
    }
}
foreach($lang in 0,1,2){
    $form=[Activator]::CreateInstance($assembly.GetType('SoundLeafSetup.SetupForm'),[object[]]@($false))
    try{
        $form.Icon=[Drawing.Icon]::ExtractAssociatedIcon($setup)
        $form.ShowInTaskbar=$false; $form.Opacity=0; $form.Show()
        [Windows.Forms.Application]::DoEvents()
        foreach($control in $form.Controls){if($control -is [Windows.Forms.ComboBox]){$control.SelectedIndex=$lang}}
        $image=New-Object Drawing.Bitmap($form.Width,$form.Height)
        try{$form.DrawToBitmap($image,(New-Object Drawing.Rectangle(0,0,$form.Width,$form.Height)));$image.Save((Join-Path $test ('wizard-'+$lang+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$image.Dispose()}
    }finally{$form.Hide();$form.Dispose()}
}
# The compiler uses the asInvoker manifest; also inspect the emitted setup PE bytes.
$bytes=[IO.File]::ReadAllBytes($setup)
$text=[Text.Encoding]::UTF8.GetString($bytes)
if($text -notmatch 'requestedExecutionLevel level="asInvoker"'){throw 'asInvoker manifest missing.'}
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $root 'tests\EmbeddedIconTests.cs') -Raw -Encoding UTF8)
$icons=[SoundLeaf.EmbeddedIconTests]::Verify($setup,(Join-Path $root 'assets\SoundLeaf.ico'))
$manifest.Tested=$true
$manifest | Add-Member -NotePropertyName Checked -NotePropertyValue (Get-Date -Format o) -Force
$manifest | Add-Member -NotePropertyName Scope -NotePropertyValue ('Non-elevated install/uninstall fixtures, active-file refusal, data preservation, unsafe paths, corrupt ZIP refusal, asInvoker and '+$icons+' icons; three actual-assembly offscreen wizard renders. Actual offline asynchronous wizard handler tested: '+[bool]($manifest.EncoderIncluded -and $integrationTested)+'. No recording application launch, manual end-user wizard or clean-machine deployment. Offline encoder/source hashes tested: '+[bool]$manifest.EncoderIncluded+'; pinned online download tested: '+[bool]$DownloadEncoder+'; per-user integration tested: '+$integrationTested) -Force
[IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'setup-manifest.json'),($manifest|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Output "PASS setup engine fixtures, data preservation, safety refusals, asInvoker and $icons icons. Not a real end-user wizard run. Fixtures retained: $test"
