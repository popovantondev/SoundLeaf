param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$folder = $PSScriptRoot
. (Join-Path $folder 'File-Hash.ps1')
if (!$OutputDirectory) { $OutputDirectory = Join-Path $folder 'artifacts' }
$dirty = & git -C $folder status --porcelain
if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Commit verified changes before exporting source.' }
$commit = (& git -C $folder rev-parse --short HEAD).Trim()
$name = 'SoundLeaf-3.0.4-preview-source-' + $commit
[void](New-Item -ItemType Directory -Path $OutputDirectory -Force)
$zip = Join-Path $OutputDirectory ($name + '.zip')
$bundle = Join-Path $OutputDirectory ($name + '.bundle')
if ((Test-Path -LiteralPath $zip) -or (Test-Path -LiteralPath $bundle)) { throw 'Export exists; overwrite refused.' }
& git -C $folder archive --format=zip --output=$zip HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source export failed.' }
& git -C $folder bundle create $bundle --all
if ($LASTEXITCODE -ne 0) { throw 'Git history export failed.' }
foreach ($exportPath in @($zip, $bundle)) { [pscustomobject]@{Hash=(Get-SoundLeafSha256 $exportPath); Path=$exportPath} }
Write-Output 'Exported committed source and Git history; no public upload.'
