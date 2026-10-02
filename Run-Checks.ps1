param([switch]$NoLive)
$ErrorActionPreference = 'Stop'
$folder = $PSScriptRoot
. (Join-Path $folder 'File-Hash.ps1')
$commit = (& git -C $folder rev-parse HEAD).Trim()
if (@(& git -C $folder status --porcelain).Count) { throw 'Commit source changes before complete candidate verification.' }
$encoderHash = Get-SoundLeafSha256 (Join-Path $folder 'tools\ffmpeg.exe')
$shell = Join-Path $PSHOME 'powershell.exe'
foreach ($script in @('Test-PublicSource.ps1','tests\FileHashTests.ps1','Build-Launcher.ps1','Run-Tests.ps1','Run-IconTests.ps1','Run-UiTests.ps1')) {
    & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $folder $script)
    if ($LASTEXITCODE -ne 0) { throw "Failed: $script" }
}
if ($NoLive) { Write-Output 'PARTIAL PASS build, synthetic audio/storage/profile tests, icons and UI; genuine loopback deliberately omitted.'; exit 0 }
$first = $false
$liveGuard = New-Object Threading.Mutex($true, 'Local\PlayerCaptureSingle', [ref]$first)
$fixture = $null
try {
    if (!$first) { throw 'Installed recorder is running. Live fixture deferred; stop/save/exit it before a complete verification.' }
    Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $folder 'tests\LiveAudioFixture.cs') -Raw -Encoding UTF8)
    $fixture = New-Object SoundLeaf.LiveAudioFixture
    foreach ($scenario in @('--verify-live','--verify-opus','--verify-wav-fallback')) {
        $check = Start-Process -FilePath (Join-Path $folder 'SoundLeaf.next.exe') -ArgumentList $scenario -WindowStyle Hidden -PassThru
        $watch = [Diagnostics.Stopwatch]::StartNew()
        while (!$check.WaitForExit(1000)) {
            if ($watch.Elapsed.TotalSeconds -gt 120) {
                $check.Kill()
                throw 'Live verification timed out; test artifacts retained.'
            }
        }
        if ($check.ExitCode -ne 0) { throw "Live verification failed: $($check.ExitCode)" }
    }
} finally {
    if ($fixture) { $fixture.Dispose() }
    if ($first) { $liveGuard.ReleaseMutex() }; $liveGuard.Dispose()
}
Write-Output 'PASS build, storage/readiness/profiles/WAV tests, icons, panel and genuine MKV/Opus/WAV loopback lifecycles using an inaudible digital-silence renderer.'
$candidate = Join-Path $folder 'SoundLeaf.next.exe'
if ((& git -C $folder rev-parse HEAD).Trim() -ne $commit -or @(& git -C $folder status --porcelain).Count -or (Get-SoundLeafSha256 (Join-Path $folder 'tools\ffmpeg.exe')) -ne $encoderHash) { throw 'Inputs changed during verification; no complete receipt written.' }
$checked = [pscustomobject]@{Full=$true; SourceCommit=$commit; Version=(Get-Item -LiteralPath $candidate).VersionInfo.FileVersion; Sha256=(Get-SoundLeafSha256 $candidate); EncoderSha256=$encoderHash; Checked=(Get-Date -Format o); Runner='Run-Checks.ps1'; LiveScenarios=@('MKV','Opus','WAV')}
[void][IO.Directory]::CreateDirectory((Join-Path $folder 'artifacts'))
$checked | ConvertTo-Json | Out-File -LiteralPath (Join-Path $folder 'artifacts\checked-candidate.json') -Encoding utf8
