param([Parameter(Mandatory=$true)][string]$Encoder,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'File-Hash.ps1')
$Encoder=[IO.Path]::GetFullPath($Encoder)
$hash=Get-SoundLeafSha256 $Encoder
if($hash -ne '3F375EDD024FF119A0919E1B12E7D5DFD9A77B208A773CBDBC777850F0477F6F'){throw 'Unreviewed encoder.'}
& git -C $repo diff --exit-code HEAD -- src tests Run-Tests.ps1 | Out-Null
if($LASTEXITCODE -ne 0){throw 'Commit compilation/test inputs before checking encoder compatibility.'}
$commit=(& git -C $repo rev-parse HEAD).Trim()
if(!$OutputDirectory){$OutputDirectory=Join-Path ([Environment]::GetFolderPath('CommonDocuments')) ('SLCheck-'+[Guid]::NewGuid().ToString('N').Substring(0,12))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if($OutputDirectory.Length -gt 60){throw 'Use a short dedicated verification path (at most 60 characters); nested recovery fixtures otherwise exceed legacy Framework limits.'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Existing verification folder; overwrite refused.'}
[void][IO.Directory]::CreateDirectory($OutputDirectory)
foreach($folder in @('src','tests')){Copy-Item -LiteralPath (Join-Path $repo $folder) -Destination (Join-Path $OutputDirectory $folder) -Recurse}
Copy-Item -LiteralPath (Join-Path $repo 'Run-Tests.ps1') -Destination $OutputDirectory
[void][IO.Directory]::CreateDirectory((Join-Path $OutputDirectory 'tools'))
Copy-Item -LiteralPath $Encoder -Destination (Join-Path $OutputDirectory 'tools\ffmpeg.exe')
$log=Join-Path $OutputDirectory 'synthetic-checks.txt'
& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -File (Join-Path $OutputDirectory 'Run-Tests.ps1') 2>&1 | Tee-Object -FilePath $log
if($LASTEXITCODE -ne 0){throw 'Synthetic compatibility failed; retained own fixtures and log.'}
$content=[IO.File]::ReadAllText($log)
if($content -notmatch 'PASS (\d+) checks; artifacts:'){throw 'Missing test completion summary.'}
$checks=[int]$Matches[1]
if($checks -lt 74 -or (Get-SoundLeafSha256 (Join-Path $OutputDirectory 'tools\ffmpeg.exe')) -ne $hash -or (& git -C $repo rev-parse HEAD).Trim() -ne $commit){throw 'Insufficient checks or changed test inputs.'}
$receipt=[pscustomobject]@{Passed=$true;Full=$false;Scope='Synthetic audio/storage/profile checks; no genuine capture and no installation';Checks=$checks;SourceCommit=$commit;EncoderSha256=$hash;Checked=(Get-Date -Format o);LogSha256=(Get-SoundLeafSha256 $log)}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'compatibility.json'),($receipt|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
Write-Output "Partial compatibility receipt: $OutputDirectory\compatibility.json"
