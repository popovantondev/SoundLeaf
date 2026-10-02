$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root 'File-Hash.ps1')
& (Join-Path $root 'Test-PublicSource.ps1') -History
foreach ($relative in @('Prepare-Release.ps1','Run-PublicChecks.ps1','Run-ReleaseChecks.ps1','Test-PublicSource.ps1','Test-ReleaseArchive.ps1')) {
    $tokens=$null; $errors=$null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $root $relative),[ref]$tokens,[ref]$errors)
    if ($errors.Count) { throw "Invalid release script syntax: $relative" }
}
$runtime = Join-Path $root 'artifacts\verified-runtime-3.0.4'
$receipt = Get-Content -LiteralPath (Join-Path $runtime 'checked-candidate.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$baseline = Get-Content -LiteralPath (Join-Path $runtime 'verification.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (!$receipt.Full -or !$baseline.ok -or $baseline.timed_out -or $baseline.checks[0].exit_code -ne 0 -or $baseline.checks[0].command -contains '-NoLive') { throw 'Incomplete runtime capsule.' }
if ((Get-SoundLeafSha256 (Join-Path $runtime 'SoundLeaf.exe')) -ne $receipt.Sha256) { throw 'Runtime capsule hash mismatch.' }
& git -C $root diff --exit-code $receipt.SourceCommit HEAD -- src Build-Launcher.ps1 Build-Icons.ps1 assets/SoundLeaf.ico
if ($LASTEXITCODE -ne 0) { throw 'Runtime changes need a new complete run, not release-only checks.' }
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $root 'tests\EmbeddedIconTests.cs') -Raw -Encoding UTF8)
$count = [SoundLeaf.EmbeddedIconTests]::Verify((Join-Path $runtime 'SoundLeaf.exe'),(Join-Path $root 'assets\SoundLeaf.ico'))
foreach ($language in @('ru','de','en')) {
    if (!(Test-Path -LiteralPath (Join-Path $root ('.github\ISSUE_TEMPLATE\bug-'+$language+'.yml'))) -or !(Test-Path -LiteralPath (Join-Path $root ('assets\screenshots\'+$language+'-control-light.png')))) { throw 'Missing localized release material.' }
}
& (Join-Path $PSHOME 'powershell.exe') -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'Run-PublicChecks.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Public build smoke command failed.' }
Write-Output "PASS release preparation: source/history audit, five script syntax checks, verified runtime SHA-256 and unchanged runtime inputs, $count embedded images and three-language release materials; local CI-equivalent smoke command passed. Runtime tests are the preserved complete 1 October run; no new live capture, installation or publication."
