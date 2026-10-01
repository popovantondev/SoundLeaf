$ErrorActionPreference = 'Stop'
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
try {
    $options = New-Object System.CodeDom.Compiler.CompilerParameters
    $options.GenerateExecutable = $true
    $options.OutputAssembly = Join-Path $PSScriptRoot 'Player.Tests.exe'
    $options.CompilerOptions = '/target:exe /platform:x64 /optimize+ /main:Player.Tests'
    [void]$options.ReferencedAssemblies.Add('System.dll')
    [void]$options.ReferencedAssemblies.Add('System.Core.dll')
    $sources = @('src/AudioCapture.cs','src/WaveStorage.cs','src/RecordingSession.cs','src/SessionRecovery.cs','tests/Tests.cs') | ForEach-Object {
        Get-Content -LiteralPath (Join-Path $PSScriptRoot $_) -Encoding UTF8 -Raw
    }
    $result = $provider.CompileAssemblyFromSource($options, [string[]]$sources)
    $result.Errors | ForEach-Object { Write-Output $_.ToString() }
    if ($result.Errors.HasErrors) { throw 'Test build failed.' }
    & $options.OutputAssembly
    if ($LASTEXITCODE -ne 0) { throw 'Player tests failed.' }
} finally { $provider.Dispose() }
