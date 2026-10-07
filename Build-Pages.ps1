param([string]$OutputDirectory)
$ErrorActionPreference='Stop';$root=$PSScriptRoot
if(!$OutputDirectory){$OutputDirectory=Join-Path $root ('artifacts\Pages-'+[Guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Pages output exists; overwrite refused.'}
[void][IO.Directory]::CreateDirectory($OutputDirectory)
[void][IO.Directory]::CreateDirectory((Join-Path $OutputDirectory 'assets'))
[IO.File]::Copy((Join-Path $root 'assets\SoundLeaf.ico'),(Join-Path $OutputDirectory 'assets\SoundLeaf.ico'),$false)
[IO.File]::Copy((Join-Path $root 'assets\tray-preview.png'),(Join-Path $OutputDirectory 'assets\tray-preview.png'),$false)
Copy-Item -LiteralPath (Join-Path $root 'assets\screenshots') -Destination (Join-Path $OutputDirectory 'assets\screenshots') -Recurse
[IO.File]::Copy((Join-Path $root 'docs\guide.css'),(Join-Path $OutputDirectory 'guide.css'),$false)
[IO.File]::Copy((Join-Path $root 'docs\guide-theme.js'),(Join-Path $OutputDirectory 'guide-theme.js'),$false)
$utf8=New-Object Text.UTF8Encoding($false)
foreach($lang in @('ru','de','en')){
    $html=[IO.File]::ReadAllText((Join-Path $root ('docs\Guide-'+$lang+'.html')),[Text.Encoding]::UTF8)
    $html=$html.Replace('../assets/','assets/')
    foreach($file in @('README.ru.md','README.de.md','VERIFICATION.md')){$html=$html.Replace('href="'+$file+'"','href="https://github.com/popovantondev/SoundLeaf/blob/main/docs/'+$file+'"')}
    $html=$html.Replace('href="../README.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/README.md"').Replace('href="../RIGHTS.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/RIGHTS.md"')
    $html=$html.Replace('href="../THIRD_PARTY_NOTICES.md"','href="https://github.com/popovantondev/SoundLeaf/blob/main/THIRD_PARTY_NOTICES.md"')
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ('Guide-'+$lang+'.html')),$html,$utf8)
}
foreach($lang in @('ru','de','en')){
    $html=[IO.File]::ReadAllText((Join-Path $OutputDirectory ('Guide-'+$lang+'.html')),[Text.Encoding]::UTF8)
    foreach($match in [regex]::Matches($html,'(?:href|src)="([^"]+)"')){
        $url=$match.Groups[1].Value;if($url -match '^(https://|#)'){continue}
        $target=[IO.Path]::GetFullPath((Join-Path $OutputDirectory $url.Split('#')[0]))
        if(!$target.StartsWith([IO.Path]::GetFullPath($OutputDirectory)+'\',[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $target)){throw "Invalid Pages link: $url"}
    }
}
[IO.File]::Copy((Join-Path $OutputDirectory 'Guide-en.html'),(Join-Path $OutputDirectory 'index.html'),$false)
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $root 'src\SoundLeafIcons.cs'),[Text.Encoding]::UTF8)) -ReferencedAssemblies System.Drawing
$leaf=[SoundLeaf.SoundLeafIcons]::Brand(256)
try{$leaf.Save((Join-Path $OutputDirectory 'assets\SoundLeaf.png'),[Drawing.Imaging.ImageFormat]::Png)}finally{$leaf.Dispose()}
Write-Output "PASS static Pages guide tree and local links; hosted URLs remain unverified: $OutputDirectory"
