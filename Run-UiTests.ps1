param([switch]$NativeOnly)
$ErrorActionPreference = 'Stop'
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
try {
    $options = New-Object System.CodeDom.Compiler.CompilerParameters
    $options.GenerateExecutable = $true
    $options.OutputAssembly = Join-Path $PSScriptRoot 'SoundLeaf.UI.Tests.exe'
    $options.CompilerOptions = '/target:exe /platform:x64 /optimize+ /main:SoundLeaf.UiTests /win32manifest:"' + (Join-Path $PSScriptRoot 'src\app.manifest') + '"'
    foreach ($assembly in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','Accessibility.dll')) { [void]$options.ReferencedAssemblies.Add($assembly) }
    $sources = @('src/AudioMeter.cs','src/TextCatalog.cs','src/TrayPanel.cs','src/TrayAnchorResolver.cs','src/LeafSelect.cs','src/RecordingCatalog.cs','src/RecordingProfile.cs','src/ProfileExport.cs','src/AudioCapture.cs','src/WaveStorage.cs','src/RecordingSession.cs','src/StartupChecks.cs','src/WavReceipt.cs','src/SessionRecovery.cs','src/SoundLeafIcons.cs','tests/UiTests.cs') | ForEach-Object { [IO.File]::ReadAllText((Join-Path $PSScriptRoot $_), (New-Object Text.UTF8Encoding($false, $true))) }
    $result = $provider.CompileAssemblyFromSource($options, [string[]]$sources)
    $result.Errors | ForEach-Object { Write-Output $_.ToString() }
    if ($result.Errors.HasErrors) { throw 'UI test build failed.' }
    if ($NativeOnly) { & $options.OutputAssembly '--native' } else { & $options.OutputAssembly }
    if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
} finally { $provider.Dispose() }
