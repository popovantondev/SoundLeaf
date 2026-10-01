param([string]$Target, [string]$Reference, [string]$Output)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $project 'ShellIconRefresh.cs') -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing
foreach ($size in @(0,1,2,4)) {
    if (!([SoundLeaf.ShellIconRefresh]::SameArtwork($Target,$Reference,$size))) {
        $image = [SoundLeaf.ShellIconRefresh]::ReadCached($Target,$size)
        try { $image.Save($Output + '.mismatch-' + $size + '.png') } finally { $image.Dispose() }
        Write-Output "Shell readback mismatch at image-list size $size."
        exit 1
    }
}
$image = [SoundLeaf.ShellIconRefresh]::ReadCached($Target,4)
try { $image.Save($Output) } finally { $image.Dispose() }
exit 0
