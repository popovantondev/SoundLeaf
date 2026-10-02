param([string]$OutputDirectory, [string]$RuntimeDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root 'File-Hash.ps1')
if (@(& git -C $root status --porcelain).Count) { throw 'Commit changes before preparing a release.' }
if ($LASTEXITCODE -ne 0) { throw 'Git status failed.' }
& (Join-Path $root 'Test-PublicSource.ps1') -History
$commit = (& git -C $root rev-parse HEAD).Trim()
$short = (& git -C $root rev-parse --short HEAD).Trim()
if ($RuntimeDirectory) {
    $verified = Get-Content -LiteralPath (Join-Path $RuntimeDirectory 'checked-candidate.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $runtimeCheck = Get-Content -LiteralPath (Join-Path $RuntimeDirectory 'verification.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $candidate = Join-Path $RuntimeDirectory 'SoundLeaf.exe'
    $check = Get-Content -LiteralPath (Join-Path $root 'artifacts\release-checks.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (!$check.Passed -or $check.SourceCommit -ne $commit -or $check.RuntimeSha256 -ne $verified.Sha256) { throw 'Matching direct Run-ReleaseChecks.ps1 receipt required.' }
    & git -C $root diff --exit-code $verified.SourceCommit HEAD -- src Build-Launcher.ps1 Build-Icons.ps1 assets/SoundLeaf.ico
    if ($LASTEXITCODE -ne 0) { throw 'Runtime inputs changed; a new complete runtime verification is required.' }
} else {
    $verified = Get-Content -LiteralPath (Join-Path $root 'artifacts\checked-candidate.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $runtimeCheck = $null
    $candidate = Join-Path $root 'SoundLeaf.next.exe'
    if ($verified.SourceCommit -ne $commit) { throw 'A new candidate must match its verified source commit.' }
}
$expectedCommand = Join-Path $root 'Run-Checks.ps1'
if (!$verified.Full -or !$verified.SourceCommit) { throw 'Matching full runtime verification is required.' }
if ($RuntimeDirectory -and (!$runtimeCheck.ok -or $runtimeCheck.timed_out -or $runtimeCheck.checks.Count -ne 1 -or $runtimeCheck.checks[0].exit_code -ne 0 -or $runtimeCheck.checks[0].command[-1] -ne $expectedCommand -or $runtimeCheck.checks[0].command -contains '-NoLive')) { throw 'Incomplete preserved runtime verification.' }
if ($verified.Version -ne '3.0.4.0' -or (Get-Item -LiteralPath $candidate).VersionInfo.FileVersion -ne $verified.Version -or (Get-SoundLeafSha256 $candidate) -ne $verified.Sha256) { throw 'Candidate differs from verified release input.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts' }
$directory = [IO.Path]::GetFullPath((Join-Path $OutputDirectory ('SoundLeaf-3.0.4-release-'+$short)))
if (Test-Path -LiteralPath $directory) { throw 'Release directory exists; overwrite refused.' }
[void][IO.Directory]::CreateDirectory($directory)
$stage = Join-Path $directory 'Portable\SoundLeaf'
[void][IO.Directory]::CreateDirectory($stage)
$pairs = @(
    @{Source=$candidate;Target='SoundLeaf.exe'},
    @{Source=(Join-Path $root 'assets\SoundLeaf.ico');Target='SoundLeaf.ico'},
    @{Source=(Join-Path $root 'distribution\README.md');Target='README.md'},
    @{Source=(Join-Path $root 'distribution\ENCODER-SETUP.txt');Target='tools\README.txt'},
    @{Source=(Join-Path $root 'RIGHTS.md');Target='RIGHTS.md'},
    @{Source=(Join-Path $root 'THIRD_PARTY_NOTICES.md');Target='THIRD_PARTY_NOTICES.md'}
)
foreach ($relative in (& git -C $root ls-files -- docs assets)) {
    # Only end-user guides/resources: no local runner report or historical machine report.
    if ($relative -match '^docs/(Guide-(ru|de|en)\.html|guide\.css)$' -or $relative -match '^assets/(SoundLeaf\.ico|tray-preview\.png|screenshots/[^/]+\.png)$') {
        $pairs += @{Source=(Join-Path $root $relative);Target=$relative}
    }
}
foreach ($pair in $pairs) {
    $target = [IO.Path]::GetFullPath((Join-Path $stage $pair.Target))
    if (!$target.StartsWith($stage+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe package path.' }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    [IO.File]::Copy($pair.Source,$target,$false)
}
# Standalone guide footer links resolve to full source documentation online, not absent files.
foreach ($language in @('ru','de','en')) {
    $guide = Join-Path $stage ('docs\Guide-'+$language+'.html')
    $text = [IO.File]::ReadAllText($guide,[Text.Encoding]::UTF8)
    $text = $text.Replace('href="README.ru.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/docs/README.ru.md"').Replace('href="README.de.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/docs/README.de.md"').Replace('href="README.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/README.md"').Replace('href="../README.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/README.md"').Replace('href="VERIFICATION.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/docs/VERIFICATION.md"')
    [IO.File]::WriteAllText($guide,$text,(New-Object Text.UTF8Encoding($false)))
}
$portableZip = Join-Path $directory 'SoundLeaf-3.0.4-win-x64-no-ffmpeg.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory((Split-Path -Parent $stage),$portableZip)
$sourceZip = Join-Path $directory 'SoundLeaf-3.0.4-source.zip'
& git -C $root archive --format=zip --output=$sourceZip HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source archive failed.' }
$entries = @()
foreach ($file in @($portableZip,$sourceZip)) {
    $hash = Get-SoundLeafSha256 $file
    [IO.File]::WriteAllText($file+'.sha256',$hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($file)+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
    $entries += [pscustomobject]@{File=[IO.Path]::GetFileName($file);Sha256=$hash}
}
$manifest = [pscustomobject]@{Version='3.0.4';SourceCommit=$commit;RuntimeSourceCommit=$verified.SourceCommit;RuntimeChecked=$verified.Checked;ReusedVerifiedRuntime=[bool]$RuntimeDirectory;Prepared=(Get-Date -Format o);ExecutableSha256=$verified.Sha256;EncoderIncluded=$false;Uploaded=$false;Archives=$entries}
[IO.File]::WriteAllText((Join-Path $directory 'release-manifest.json'),($manifest|ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
Write-Output "Prepared local release candidates: $directory. FFmpeg absent; no upload, tag, installation or recorder launch."
