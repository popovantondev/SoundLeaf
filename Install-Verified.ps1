param([string]$EncoderBundle)
$ErrorActionPreference = 'Stop'
$installRoot = 'C:\Users\Public\Player'
$projectRoot = $PSScriptRoot
. (Join-Path $projectRoot 'File-Hash.ps1')
$receiptPath = Join-Path $projectRoot 'artifacts\checked-candidate.json'
if (!(Test-Path -LiteralPath $receiptPath)) { throw 'Full-check candidate receipt missing.' }
$verified = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
$candidate = Join-Path $projectRoot 'SoundLeaf.next.exe'
$expectedHash = $verified.Sha256
if (!$verified.Full -or $verified.Runner -ne 'Run-Checks.ps1' -or !$verified.EncoderSha256 -or $verified.LiveScenarios.Count -ne 3 -or (Get-SoundLeafSha256 $candidate) -ne $expectedHash) { throw 'Candidate does not match complete direct verification.' }
if ((Get-Item -LiteralPath $candidate).VersionInfo.FileVersion -ne $verified.Version) { throw 'Wrong candidate version.' }
if (@(& git -C $projectRoot status --porcelain).Count) { throw 'Commit source changes before installation.' }
& git -C $projectRoot diff --exit-code $verified.SourceCommit HEAD -- src tests Build-Launcher.ps1 Build-Icons.ps1 Run-Checks.ps1 Run-Tests.ps1 Run-IconTests.ps1 Run-UiTests.ps1 assets/SoundLeaf.ico
if ($LASTEXITCODE -ne 0) { throw 'Tested inputs changed; complete verification required again.' }
if ((Get-SoundLeafSha256 (Join-Path $projectRoot 'tools\ffmpeg.exe')) -ne $verified.EncoderSha256) { throw 'Project encoder differs from verified encoder.' }
$encoderChanged = (Get-SoundLeafSha256 (Join-Path $installRoot 'tools\ffmpeg.exe')) -ne $verified.EncoderSha256
if ($EncoderBundle) {
    $EncoderBundle = [IO.Path]::GetFullPath($EncoderBundle)
    $bundle = Get-Content -LiteralPath (Join-Path $EncoderBundle 'Runtime\tools\encoder-bundle.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($bundle.Sha256 -ne $verified.EncoderSha256 -or (Get-SoundLeafSha256 (Join-Path $EncoderBundle 'Runtime\tools\ffmpeg.exe')) -ne $verified.EncoderSha256 -or (Get-SoundLeafSha256 (Join-Path $EncoderBundle 'Runtime\tools\ffmpeg-9.0.2-corresponding-source.zip')) -ne $bundle.SourceKitSha256) { throw 'Encoder bundle differs from verified binary/source materials.' }
} elseif ($encoderChanged) { throw 'Provide the matching encoder bundle to update a different installed encoder.' }
if ((Get-Item -LiteralPath $installRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Installation root is a reparse point.' }
$first = $false
$updateMutex = New-Object Threading.Mutex($true, 'Local\PlayerCaptureSingle', [ref]$first)
try {
    if (!$first -or (Get-Process -Name SoundLeaf,Player -ErrorAction SilentlyContinue)) { throw 'Recorder is running; save and exit first. No files were replaced.' }
    Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $projectRoot 'ShellIconRefresh.cs') -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing
    $oldIcons = @()
    foreach ($relative in @('SoundLeaf.exe','SoundLeaf.ico')) {
        $oldPath = Join-Path $installRoot $relative
        if (Test-Path -LiteralPath $oldPath) { $oldIcons += [SoundLeaf.ShellIconRefresh]::Capture($oldPath) }
    }
    $backupRoot = Join-Path $installRoot ('Backups\BeforeSoundLeaf-' + $verified.Version + '-' + (Get-Date -Format yyyyMMdd-HHmmss) + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
    if (![IO.Path]::GetFullPath($backupRoot).StartsWith($installRoot + '\Backups\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe backup target.' }
    [void][IO.Directory]::CreateDirectory($backupRoot)
    $pairs = @(
        @{Source=$candidate; Relative='SoundLeaf.exe'},
        @{Source=(Join-Path $projectRoot 'assets\SoundLeaf.ico'); Relative='SoundLeaf.ico'},
        @{Source=(Join-Path $projectRoot 'docs\VERIFICATION.md'); Relative='VERIFIED.md'},
        @{Source=(Join-Path $projectRoot 'docs\README.ru.md'); Relative='README.ru.md'},
        @{Source=(Join-Path $projectRoot 'README.md'); Relative='README.md'},
        @{Source=(Join-Path $projectRoot 'CHANGELOG.md'); Relative='CHANGELOG.md'},
        @{Source=(Join-Path $projectRoot 'RIGHTS.md'); Relative='RIGHTS.md'},
        @{Source=(Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md'); Relative='THIRD_PARTY_NOTICES.md'}
    )
    foreach ($relative in (& git -C $projectRoot ls-files -- docs assets)) { $pairs += @{Source=(Join-Path $projectRoot $relative); Relative=$relative} }
    if ($EncoderBundle) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $EncoderBundle 'Runtime\tools') -Recurse -File) {
            if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Encoder bundle contains a reparse file.' }
            $pairs += @{Source=$file.FullName; Relative=$file.FullName.Substring((Join-Path $EncoderBundle 'Runtime').Length+1)}
        }
    }
    foreach ($pair in $pairs) {
        $target = [IO.Path]::GetFullPath((Join-Path $installRoot $pair.Relative))
        $backup = [IO.Path]::GetFullPath((Join-Path $backupRoot $pair.Relative))
        if (!$target.StartsWith($installRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or !$backup.StartsWith($backupRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe replacement target.' }
        $targetParent = [IO.Path]::GetDirectoryName($target)
        [void][IO.Directory]::CreateDirectory($targetParent)
        if ((Get-Item -LiteralPath $targetParent).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Target parent is a reparse point.' }
        if ((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Target is a reparse point.' }
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup))
        $stage = $target + '.' + [Guid]::NewGuid().ToString('N') + '.install'
        if ($pair.Relative -eq 'README.ru.md') {
            $guide = [IO.File]::ReadAllText($pair.Source, [Text.Encoding]::UTF8)
            $guide = $guide.Replace('(../README.md)','(README.md)').Replace('(README.de.md)','(docs/README.de.md)').Replace('(../assets/','(assets/').Replace('(Guide-ru.html)','(docs/Guide-ru.html)').Replace('(VERIFICATION.md)','(docs/VERIFICATION.md)').Replace('(ARCHITECTURE.md)','(docs/ARCHITECTURE.md)').Replace('(../CHANGELOG.md)','(CHANGELOG.md)').Replace('(../RIGHTS.md)','(RIGHTS.md)').Replace('(../THIRD_PARTY_NOTICES.md)','(THIRD_PARTY_NOTICES.md)')
            [IO.File]::WriteAllText($stage, $guide, (New-Object Text.UTF8Encoding($false)))
        } else { [IO.File]::Copy($pair.Source, $stage, $false) }
        $flush = [IO.File]::Open($stage, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try { $flush.Flush($true) } finally { $flush.Dispose() }
        if (Test-Path -LiteralPath $target) { [IO.File]::Replace($stage, $target, $backup) } else { [IO.File]::Move($stage, $target) }
    }
    $installedHash = Get-SoundLeafSha256 (Join-Path $installRoot 'SoundLeaf.exe')
    if ($installedHash -ne $expectedHash) { throw 'Installed executable hash mismatch.' }
    if ((Get-SoundLeafSha256 (Join-Path $installRoot 'tools\ffmpeg.exe')) -ne $verified.EncoderSha256) { throw 'Installed encoder hash mismatch.' }
    # Notify the changed files, then invalidate shell artwork once. No Explorer restart,
    # icon-cache file deletion, file associations or registry writes.
    foreach ($oldIcon in $oldIcons) { [SoundLeaf.ShellIconRefresh]::Refresh($oldIcon) }
    [SoundLeaf.ShellIconRefresh]::InvalidateShellArtwork()
    $receipt = [pscustomobject]@{Version=$verified.Version; Commit=(& git -C $projectRoot rev-parse HEAD).Trim(); TestedSourceCommit=$verified.SourceCommit; Sha256=$installedHash; EncoderSha256=$verified.EncoderSha256; Backup=$backupRoot; Updated=(Get-Date -Format o); EncoderUnchanged=(!$encoderChanged); RecordingsMoved=$false; ShellIconsRefreshed=$oldIcons.Count; ShellArtworkInvalidated=$true; ExplorerRestarted=$false; CacheFilesDeleted=$false}
    $receipt | ConvertTo-Json | Out-File -LiteralPath (Join-Path $projectRoot ('artifacts\install-'+$verified.Version+'.json')) -Encoding utf8
    $receipt | ConvertTo-Json
} finally { if ($first) { $updateMutex.ReleaseMutex() }; $updateMutex.Dispose() }
