param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'File-Hash.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$manifest = Get-Content -LiteralPath (Join-Path $Directory 'release-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.EncoderIncluded -or $manifest.Uploaded -or $manifest.Archives.Count -ne 2) { throw 'Unexpected release manifest.' }
$allFiles = @(& git -C $PSScriptRoot -c core.quotepath=false ls-files)
$portable = Join-Path $Directory 'SoundLeaf-3.0.4-win-x64-no-ffmpeg.zip'
foreach ($entry in $manifest.Archives) {
    if ([IO.Path]::GetFileName($entry.File) -ne $entry.File) { throw 'Unsafe manifest name.' }
    $path = Join-Path $Directory $entry.File
    $actual = Get-SoundLeafSha256 $path
    $sidecar = [IO.File]::ReadAllText($path+'.sha256').Trim()
    if ($actual -ne $entry.Sha256 -or $sidecar -ne ($actual.ToLowerInvariant()+'  '+$entry.File)) { throw 'Archive/sidecar checksum mismatch.' }
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    $seen = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $files = 0
    try {
        foreach ($item in $zip.Entries) {
            $name = $item.FullName.Replace('\','/')
            if ($name.StartsWith('/') -or $name -match '(^|/)\.\.(/|$)|:|(^|/)(Recordings|State|logs|Verification|Backups|RecoveryWork|\.ai-dev|\.git)/' -or !$seen.Add($name)) { throw 'Unsafe, duplicate or private ZIP entry.' }
            if (!$item.Name) { continue }; $files++
            if ($entry.File -like '*source.zip') {
                if ($allFiles -notcontains $name) { throw "Non-source archive entry: $name" }
            } elseif ($name -match '\.(exe|wav|mkv|ogg|mp3|m4a|aac|bundle)$' -and $name -ne 'SoundLeaf/SoundLeaf.exe') { throw 'Unexpected executable/audio in portable archive.' }
        }
        if ($entry.File -like '*source.zip' -and $files -ne $allFiles.Count) { throw 'Source ZIP omits committed files.' }
    } finally { $zip.Dispose() }
}
$extract = Join-Path $Directory ('SmokeExtract-'+[Guid]::NewGuid().ToString('N'))
[IO.Compression.ZipFile]::ExtractToDirectory($portable,$extract)
$app = Join-Path $extract 'SoundLeaf'
$exe = Join-Path $app 'SoundLeaf.exe'
if ((Get-SoundLeafSha256 $exe) -ne $manifest.ExecutableSha256 -or (Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '3.0.4.0') { throw 'Extracted EXE differs from verified runtime.' }
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'tests\EmbeddedIconTests.cs') -Raw -Encoding UTF8)
[void][SoundLeaf.EmbeddedIconTests]::Verify($exe,(Join-Path $app 'SoundLeaf.ico'))
$links = 0
foreach ($language in @('ru','de','en')) {
    $guide = Join-Path $app ('docs\Guide-'+$language+'.html')
    $html = [IO.File]::ReadAllText($guide,[Text.Encoding]::UTF8)
    foreach ($match in [regex]::Matches($html,'(?:href|src)="([^"]+)"')) {
        $reference = $match.Groups[1].Value
        if ($reference -match '^(https?://|#)') { continue }
        $target = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($guide)) $reference))
        if (!$target.StartsWith($app+'\',[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $target)) { throw "Broken local guide reference: $language/$reference" }
        $links++
    }
}
if (Test-Path -LiteralPath (Join-Path $app 'tools\ffmpeg.exe')) { throw 'Encoder unexpectedly included.' }
Write-Output "PASS release ZIPs: hashes/sidecars, exact committed source, safe/private entry rules, clean extraction, unchanged EXE/version/eight icons and $links local guide links. EXE not launched; external links/hosted workflow not yet verified. Extraction retained: $extract"
