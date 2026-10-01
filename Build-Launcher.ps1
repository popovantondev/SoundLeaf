$ErrorActionPreference = 'Stop'

$folder = Split-Path -Parent $PSCommandPath
& (Join-Path $folder 'Build-Icons.ps1')
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.OutputAssembly = Join-Path $folder 'Player.next.exe'
$parameters.CompilerOptions = '/target:winexe /platform:x64 /optimize+ /warn:4 /win32icon:"' + (Join-Path $folder 'assets\Player.ico') + '"'
[void]$parameters.ReferencedAssemblies.Add('System.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Windows.Forms.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Drawing.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Runtime.Serialization.dll')
[void]$parameters.ReferencedAssemblies.Add('System.Xml.dll')
$sources = @('src/AudioCapture.cs','src/WaveStorage.cs','src/RecordingSession.cs','src/StartupChecks.cs','src/WavReceipt.cs','src/SessionRecovery.cs','src/PlayerIcons.cs','src/PlayerApp.cs') | ForEach-Object {
    Get-Content -LiteralPath (Join-Path $folder $_) -Raw -Encoding UTF8
}
$result = $provider.CompileAssemblyFromSource($parameters, [string[]]$sources)
$result.Errors | ForEach-Object { Write-Output $_.ToString() }
if ($result.Errors.HasErrors) {
    $result.Errors | ForEach-Object { throw $_.ToString() }
}
$provider.Dispose()
Write-Output 'Built Player.next.exe. Verify this candidate before installation.'
