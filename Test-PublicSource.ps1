param([switch]$History)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$tracked = @(& git -C $root -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0 -or !$tracked.Count) { throw 'Cannot enumerate committed source.' }
$privatePath = '(^|/)(Recordings|State|logs|Verification|Backups|RecoveryWork|artifacts|\.ai-dev|\.vs|bin|obj)/|\.(exe|wav|mkv|ogg|mp3|m4a|aac|zip|bundle|pdb)$'
$excludedNames = @(('Te'+'ams'),('Chat'+'GPT'),('Open'+'AI'), ('AI'+'-assisted'),('AI'+'-generated'))
$excludedText = ($excludedNames | ForEach-Object { [regex]::Escape($_) }) -join '|'
$credentials = 'gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
$utf8 = New-Object Text.UTF8Encoding($false,$true)
$texts = 0
foreach ($relative in $tracked) {
    if ($relative -match $privatePath) { throw "Private/generated file tracked: $relative" }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (!$path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Tracked path escapes source root.' }
    if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse source file: $relative" }
    if ($relative -match '\.(cs|ps1|md|html|css|yml|yaml|json|txt)$' -or $relative -match '^\.(gitignore|gitattributes|editorconfig)$') {
        $content = [IO.File]::ReadAllText($path,$utf8)
        if ($content -match $excludedText) { throw "Out-of-scope naming or development attribution in $relative" }
        if ($content -match $credentials) { throw "Possible credential material in $relative; review before publication." }
        $texts++
    }
}
if ($History) {
    $objects = @(& git -C $root rev-list --objects --all)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot audit history paths.' }
    foreach ($object in $objects) { if ($object -match '^[a-f0-9]+ (.+)$' -and $Matches[1] -match $privatePath) { throw 'Private/generated historical path; review required without automatic history rewriting.' } }
    $mentions = @(& git -C $root log --all --format=%H '-i' '-G' $excludedText -- .)
    if ($LASTEXITCODE -ne 0 -or $mentions.Count) { throw 'History naming audit requires review; history was not changed.' }
}
Write-Output "PASS public-source audit: $($tracked.Count) tracked files, $texts strict UTF-8 text files; private paths, selected attribution and credential patterns checked. Pattern checks are not a guarantee against every secret."
