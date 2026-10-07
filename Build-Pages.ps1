param([string]$OutputDirectory, [switch]$SyncRoot)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$source = Join-Path $root 'website'
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root ('artifacts\Pages-' + [Guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Pages output exists; overwrite refused.' }
if ($output.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase) -or $output -eq $source) { throw 'Output cannot be inside the website source.' }
$files = @(Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object Name -ne 'README.md')
$html = @($files | Where-Object Extension -eq '.html')
if ($html.Count -ne 16) { throw 'Expected default homepage plus fifteen localized pages.' }
$utf8 = [Text.UTF8Encoding]::new($false, $true)
foreach ($name in @('RIGHTS.md', 'THIRD_PARTY_NOTICES.md')) {
    if ((Get-FileHash (Join-Path $source $name)).Hash -ne (Get-FileHash (Join-Path $root $name)).Hash) { throw "Canonical notice differs: $name" }
}
foreach ($file in $files | Where-Object { $_.Extension -in @('.html', '.css', '.js', '.md') }) {
    $text = $utf8.GetString([IO.File]::ReadAllBytes($file.FullName))
    if ($file.Extension -eq '.html' -and $text -notmatch '(?i)charset=["'']?utf-8') { throw "Missing UTF-8 declaration: $($file.Name)" }
    $pattern = '(?:href|src)=["'']([^"'']+)["'']|url\(["'']?([^\)"'']+)["'']?\)'
    foreach ($match in [regex]::Matches($text, $pattern)) {
        $url = $match.Groups[1].Value
        if (!$url) { $url = $match.Groups[2].Value }
        if ($url -match '^(https?://|#|data:|mailto:)') { continue }
        $path = [Uri]::UnescapeDataString(($url -split '[#?]')[0])
        if (!$path) { continue }
        $target = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $path))
        if (!$target.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $target -PathType Leaf)) { throw "Invalid local link in $($file.Name): $url" }
    }
}
foreach ($file in $files) {
    $relative = $file.FullName.Substring($source.Length + 1).Replace('\', '/')
    if ($relative -notmatch '^(\.nojekyll|(?:index(?:-(?:ru|de|en))?|(?:Guide|rights|notices|licenses)-(?:ru|de|en))\.html|site\.(?:css|js)|RIGHTS\.md|THIRD_PARTY_NOTICES\.md|assets/.+\.(?:png|ico)|licenses/.+)$') { throw "Unapproved published path: $relative" }
    $destination = Join-Path $output $relative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
    [IO.File]::Copy($file.FullName, $destination, $false)
}
if ($SyncRoot) {
    # Synchronize only static website files; never application or release artifacts.
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($source.Length + 1)
        if ($relative -in @('RIGHTS.md', 'THIRD_PARTY_NOTICES.md')) { continue }
        $destination = Join-Path $root $relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
        [IO.File]::Copy((Join-Path $output $relative), $destination, $true)
    }
}
Write-Output "PASS: $($html.Count) HTML pages, valid local references and UTF-8; unchanged canonical rights. Output: $output"
