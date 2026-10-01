$ErrorActionPreference = 'Stop'
$folder = $PSScriptRoot
$shell = Join-Path $PSHOME 'powershell.exe'
foreach ($script in @('Build-Launcher.ps1','Run-Tests.ps1','Run-IconTests.ps1')) {
    & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $folder $script)
    if ($LASTEXITCODE -ne 0) { throw "Failed: $script" }
}
$check = Start-Process -FilePath (Join-Path $folder 'Player.next.exe') -ArgumentList '--verify-live' -WindowStyle Hidden -PassThru
$watch = [Diagnostics.Stopwatch]::StartNew()
while (!$check.WaitForExit(1000)) {
    if ($watch.Elapsed.TotalSeconds -gt 120) {
        $check.Kill()
        throw 'Live verification timed out; test artifacts retained.'
    }
}
if ($check.ExitCode -ne 0) { throw "Live verification failed: $($check.ExitCode)" }
Write-Output 'PASS build, storage/recovery tests, icons and real loopback lifecycle.'
