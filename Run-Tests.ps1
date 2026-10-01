$ErrorActionPreference = 'Stop'
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
try {
    $options = New-Object System.CodeDom.Compiler.CompilerParameters
    $options.GenerateExecutable = $true
    $options.OutputAssembly = Join-Path $PSScriptRoot 'SoundLeaf.Tests.exe'
    $options.CompilerOptions = '/target:exe /platform:x64 /optimize+ /main:SoundLeaf.Tests'
    [void]$options.ReferencedAssemblies.Add('System.dll')
    [void]$options.ReferencedAssemblies.Add('System.Core.dll')
    [void]$options.ReferencedAssemblies.Add('System.Runtime.Serialization.dll')
    [void]$options.ReferencedAssemblies.Add('System.Xml.dll')
    $sources = @('src/TextCatalog.cs','src/RecordingCatalog.cs','src/RecordingProfile.cs','src/ProfileExport.cs','src/AudioCapture.cs','src/WaveStorage.cs','src/RecordingSession.cs','src/StartupChecks.cs','src/WavReceipt.cs','src/SessionRecovery.cs','tests/Tests.cs','tests/ReadinessTests.cs','tests/ProfileTests.cs') | ForEach-Object {
        Get-Content -LiteralPath (Join-Path $PSScriptRoot $_) -Encoding UTF8 -Raw
    }
    $result = $provider.CompileAssemblyFromSource($options, [string[]]$sources)
    $result.Errors | ForEach-Object { Write-Output $_.ToString() }
    if ($result.Errors.HasErrors) { throw 'Test build failed.' }
    & $options.OutputAssembly
    if ($LASTEXITCODE -ne 0) { throw 'SoundLeaf tests failed.' }
} finally { $provider.Dispose() }
