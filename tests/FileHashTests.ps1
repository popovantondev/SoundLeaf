$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\File-Hash.ps1')
$testRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Verification'))
[void][IO.Directory]::CreateDirectory($testRoot)
$testFile = Join-Path $testRoot ('hash-' + [Guid]::NewGuid().ToString('N') + '.tmp')
if (![IO.Path]::GetFullPath($testFile).StartsWith($testRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe hash test path.' }
$created = $false
try {
    $empty = [IO.File]::Open($testFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $created = $true
    try { $empty.Flush($true) } finally { $empty.Dispose() }
    if ((Get-SoundLeafSha256 $testFile) -ne 'E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855') { throw 'Empty SHA-256 vector failed.' }
    [IO.File]::WriteAllBytes($testFile, [Text.Encoding]::ASCII.GetBytes('abc'))
    if ((Get-SoundLeafSha256 $testFile) -ne 'BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD') { throw 'ASCII SHA-256 vector failed.' }
    $refused = $false
    try { [void](Get-SoundLeafSha256 ($testFile + '.missing')) } catch { $refused = $true }
    if (!$refused) { throw 'Missing hash input must fail.' }
    Write-Output 'PASS portable SHA-256: empty/ASCII vectors and missing-file refusal.'
} finally { if ($created) { [IO.File]::Delete($testFile) } }
