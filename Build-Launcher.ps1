$ErrorActionPreference = 'Stop'

$folder = Split-Path -Parent $PSCommandPath
& (Join-Path $folder 'Build-Icons.ps1')
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.OutputAssembly = Join-Path $folder 'SoundLeaf.next.exe'
$parameters.CompilerOptions = '/target:winexe /platform:x64 /optimize+ /warn:4 /win32manifest:"' + (Join-Path $folder 'src\app.manifest') + '" /win32icon:"' + (Join-Path $folder 'assets\SoundLeaf.ico') + '"'
[void]$parameters.ReferencedAssemblies.Add('System.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Core.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Windows.Forms.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Drawing.dll')
[void]$parameters.ReferencedAssemblies.Add('Accessibility.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Runtime.Serialization.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Xml.dll')
$sources = @('src/TextCatalog.cs','src/TrayPanel.cs','src/TrayAnchorResolver.cs','src/LeafSelect.cs','src/RecordingCatalog.cs','src/RecordingProfile.cs','src/ProfileExport.cs','src/AudioCapture.cs','src/WaveStorage.cs','src/RecordingSession.cs','src/StartupChecks.cs','src/WavReceipt.cs','src/SessionRecovery.cs','src/SoundLeafIcons.cs','src/SoundLeafApp.cs') | ForEach-Object {
    [IO.File]::ReadAllText((Join-Path $folder $_), (New-Object Text.UTF8Encoding($false, $true)))
}
$result = $provider.CompileAssemblyFromSource($parameters, [string[]]$sources)
$result.Errors | ForEach-Object { Write-Output $_.ToString() }
if ($result.Errors.HasErrors) {
    $result.Errors | ForEach-Object { throw $_.ToString() }
}
$provider.Dispose()
Write-Output 'Built SoundLeaf.next.exe. Verify this candidate before installation.'
