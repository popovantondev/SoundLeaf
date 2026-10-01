$ErrorActionPreference = 'Stop'
$installRoot = 'C:\Users\Public\Player'
$projectRoot = $PSScriptRoot
$check = Get-Content -LiteralPath (Join-Path $projectRoot '.ai-dev\verification.json') -Raw | ConvertFrom-Json
if (!$check.ok -or $check.timed_out -or $check.checks[0].exit_code -ne 0 -or $check.checks[0].command -contains '-NoLive') { throw 'Complete MegaProg verification required; partial checks cannot authorize installation.' }
$receiptPath = Join-Path $projectRoot 'artifacts\checked-candidate.json'
if (!(Test-Path -LiteralPath $receiptPath)) { throw 'Full-check candidate receipt missing.' }
$verified = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
$candidate = Join-Path $projectRoot 'SoundLeaf.next.exe'
$expectedHash = $verified.Sha256
if (!$verified.Full -or $verified.Version -ne '3.0.2.0' -or (Get-FileHash -LiteralPath $candidate).Hash -ne $expectedHash) { throw 'Candidate does not match completed verification.' }
if ((Get-Item -LiteralPath $candidate).VersionInfo.FileVersion -ne '3.0.2.0') { throw 'Wrong candidate version.' }
if ((Get-FileHash -LiteralPath (Join-Path $installRoot 'tools\ffmpeg.exe')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'tools\ffmpeg.exe')).Hash) { throw 'Installed encoder differs from verified encoder.' }
if ((Get-Item -LiteralPath $installRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Installation root is a reparse point.' }
$first = $false
$updateMutex = New-Object Threading.Mutex($true, 'Local\PlayerCaptureSingle', [ref]$first)
try {
    if (!$first -or (Get-Process -Name SoundLeaf,Player -ErrorAction SilentlyContinue)) { throw 'Recorder is running; save and exit first. No files were replaced.' }
    $backupRoot = Join-Path $installRoot ('Backups\BeforeSoundLeaf302-' + (Get-Date -Format yyyyMMdd-HHmmss) + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
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
    $installedHash = (Get-FileHash -LiteralPath (Join-Path $installRoot 'SoundLeaf.exe')).Hash
    if ($installedHash -ne $expectedHash) { throw 'Installed executable hash mismatch.' }
    # Targeted shell refresh only: no Explorer restart or global icon-cache deletion.
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class SoundLeafShellRefresh { [DllImport("shell32.dll", CharSet=CharSet.Unicode)] public static extern void SHChangeNotify(uint change, uint flags, string path, IntPtr second); }'
    [SoundLeafShellRefresh]::SHChangeNotify(0x2000, 0x1005, (Join-Path $installRoot 'SoundLeaf.exe'), [IntPtr]::Zero)
    [SoundLeafShellRefresh]::SHChangeNotify(0x2000, 0x1005, (Join-Path $installRoot 'SoundLeaf.ico'), [IntPtr]::Zero)
    $receipt = [pscustomobject]@{Version='3.0.2'; Commit=(& git -C $projectRoot rev-parse HEAD).Trim(); Sha256=$installedHash; Backup=$backupRoot; Updated=(Get-Date -Format o); EncoderUnchanged=$true; RecordingsMoved=$false}
    $receipt | ConvertTo-Json | Out-File -LiteralPath (Join-Path $projectRoot 'artifacts\install-3.0.2.json') -Encoding utf8
    $receipt | ConvertTo-Json
} finally { if ($first) { $updateMutex.ReleaseMutex() }; $updateMutex.Dispose() }
