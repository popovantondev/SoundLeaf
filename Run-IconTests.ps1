$ErrorActionPreference = 'Stop'
$folder = Split-Path -Parent $PSCommandPath
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $folder 'src/PlayerIcons.cs') -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing
$checks = 0
foreach ($state in 0..4) {
    foreach ($size in @(16,20,24,32,48,64,128,256)) {
        $bitmap = [Player.PlayerIcons]::Render($state, $size, 0)
        try {
            if ($bitmap.Width -ne $size -or $bitmap.Height -ne $size -or $bitmap.GetPixel(0,0).A -ne 0) {
                throw "Invalid transparent icon: state=$state size=$size"
            }
            $checks++
        } finally { $bitmap.Dispose() }
    }
    $icon = [Player.PlayerIcons]::Create($state,0)
    try { if ($icon.Handle -eq [IntPtr]::Zero) { throw 'Invalid native icon.' }; $checks++ }
    finally { $icon.Dispose() }
}
$green = [Player.PlayerIcons]::Render(0,64,0)
$red = [Player.PlayerIcons]::Render(3,64,0)
try {
    if ($green.GetPixel(24,32).G -le $green.GetPixel(24,32).R) { throw 'Recording icon is not green.' }
    if ($red.GetPixel(22,32).R -le $red.GetPixel(22,32).G) { throw 'Error icon is not red.' }
    $checks += 2
} finally { $green.Dispose(); $red.Dispose() }
$frames = New-Object 'System.Collections.Generic.HashSet[string]'
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    foreach ($frame in 0..([Player.PlayerIcons]::FrameCount - 1)) {
        $bytes = [Player.PlayerIcons]::IcoBytes(4,$frame)
        if (!$frames.Add([BitConverter]::ToString($sha.ComputeHash($bytes)))) {
            throw 'Duplicate animation frame.'
        }
        $icon = [Player.PlayerIcons]::Create(4,$frame)
        try { if ($icon.Handle -eq [IntPtr]::Zero) { throw 'Invalid animation icon.' }; $checks++ }
        finally { $icon.Dispose() }
    }
} finally { $sha.Dispose() }
$embedded = [System.Drawing.Icon]::ExtractAssociatedIcon((Join-Path $folder 'Player.next.exe'))
try {
    if ($embedded -eq $null) { throw 'Executable has no embedded icon.' }
    $bitmap = $embedded.ToBitmap()
    try {
        $pixel = $bitmap.GetPixel([int]($bitmap.Width * 0.375), [int]($bitmap.Height * 0.5))
        if ($pixel.G -le $pixel.R) { throw 'Executable icon is not the green Player artwork.' }
        $checks++
    } finally { $bitmap.Dispose() }
} finally { if ($embedded) { $embedded.Dispose() } }
Write-Output "PASS $checks icon checks; 24 distinct animation frames."
