$ErrorActionPreference = 'Stop'
$folder = Split-Path -Parent $PSCommandPath
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $folder 'src/PlayerIcons.cs') -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing
[Player.PlayerIcons]::Export((Join-Path $folder 'assets'))
Write-Output 'Generated multi-resolution Player.ico and tray-preview.png.'
