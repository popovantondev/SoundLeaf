$ErrorActionPreference = 'Stop'
foreach ($script in @('Test-PublicSource.ps1','tests\FileHashTests.ps1','Build-Launcher.ps1')) {
    & (Join-Path $PSHOME 'powershell.exe') -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $script)
    if ($LASTEXITCODE -ne 0) { throw "Public build smoke check failed: $script" }
}
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'tests\EmbeddedIconTests.cs') -Raw -Encoding UTF8)
$count = [SoundLeaf.EmbeddedIconTests]::Verify((Join-Path $PSScriptRoot 'SoundLeaf.next.exe'),(Join-Path $PSScriptRoot 'assets\SoundLeaf.ico'))
Write-Output "PARTIAL PASS source audit, hash helpers, build and $count embedded images. No audio capture, encoder/UI tests or release authorization."
