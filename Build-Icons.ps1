$ErrorActionPreference = 'Stop'
$folder = Split-Path -Parent $PSCommandPath
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $folder 'src/SoundLeafIcons.cs') -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing
[SoundLeaf.SoundLeafIcons]::Export((Join-Path $folder 'assets'))
Write-Output 'Generated multi-resolution SoundLeaf.ico and tray-preview.png.'
