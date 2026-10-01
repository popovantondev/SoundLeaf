$ErrorActionPreference = 'Stop'
$folder = Split-Path -Parent $PSCommandPath
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $folder 'src/SoundLeafIcons.cs') -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing
$checks = 0
foreach ($state in 0..4) {
    foreach ($size in @(16,20,24,32,48,64,128,256)) {
        $bitmap = [SoundLeaf.SoundLeafIcons]::Render($state, $size, 0)
        try {
            if ($bitmap.Width -ne $size -or $bitmap.Height -ne $size -or $bitmap.GetPixel(0,0).A -ne 0) {
                throw "Invalid transparent icon: state=$state size=$size"
            }
            $checks++
        } finally { $bitmap.Dispose() }
    }
    $icon = [SoundLeaf.SoundLeafIcons]::Create($state,0)
    try { if ($icon.Handle -eq [IntPtr]::Zero) { throw 'Invalid native icon.' }; $checks++ }
    finally { $icon.Dispose() }
}
$green = [SoundLeaf.SoundLeafIcons]::Render(0,64,0)
$red = [SoundLeaf.SoundLeafIcons]::Render(3,64,0)
try {
    if ($green.GetPixel(24,32).G -le $green.GetPixel(24,32).R) { throw 'Recording icon is not green.' }
    if ($red.GetPixel(22,32).R -le $red.GetPixel(22,32).G) { throw 'Error icon is not red.' }
    $checks += 2
} finally { $green.Dispose(); $red.Dispose() }
$frames = New-Object 'System.Collections.Generic.HashSet[string]'
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    foreach ($frame in 0..([SoundLeaf.SoundLeafIcons]::FrameCount - 1)) {
        $bytes = [SoundLeaf.SoundLeafIcons]::IcoBytes(4,$frame)
        if (!$frames.Add([BitConverter]::ToString($sha.ComputeHash($bytes)))) {
            throw 'Duplicate animation frame.'
        }
        $icon = [SoundLeaf.SoundLeafIcons]::Create(4,$frame)
        try { if ($icon.Handle -eq [IntPtr]::Zero) { throw 'Invalid animation icon.' }; $checks++ }
        finally { $icon.Dispose() }
    }
} finally { $sha.Dispose() }
$reference = [SoundLeaf.SoundLeafIcons]::Render(4,64,0)
try {
    $left = 64; $right = -1
    foreach ($x in 16..48) { if ($reference.GetPixel($x,32).A -gt 0) { $left = [Math]::Min($left,$x); $right = [Math]::Max($right,$x) } }
    if ([Math]::Abs(($left + $right + 1) / 2.0 - 32) -gt 0.5) { throw 'Saving triangle is not centered.' }
    $checks++
    foreach ($frame in 0..23) {
        $bitmap = [SoundLeaf.SoundLeafIcons]::Render(4,64,$frame)
        try {
            foreach ($y in 20..44) { foreach ($x in 20..44) {
                if ($bitmap.GetPixel($x,$y).ToArgb() -ne $reference.GetPixel($x,$y).ToArgb()) { throw 'Central triangle moves during saving.' }
            } }
            foreach ($x in 0..63) {
                if ($bitmap.GetPixel($x,0).A -ne 0 -or $bitmap.GetPixel($x,63).A -ne 0 -or
                    $bitmap.GetPixel(0,$x).A -ne 0 -or $bitmap.GetPixel(63,$x).A -ne 0) { throw 'Saving ring touches canvas edge.' }
            }
            $checks++
        } finally { $bitmap.Dispose() }
    }
} finally { $reference.Dispose() }
$embedded = [System.Drawing.Icon]::ExtractAssociatedIcon((Join-Path $folder 'SoundLeaf.next.exe'))
try {
    if ($embedded -eq $null) { throw 'Executable has no embedded icon.' }
    $bitmap = $embedded.ToBitmap()
    try {
        $pixel = $bitmap.GetPixel([int]($bitmap.Width * 0.375), [int]($bitmap.Height * 0.5))
        if ($pixel.G -le $pixel.R) { throw 'Executable icon is not the green SoundLeaf artwork.' }
        $checks++
    } finally { $bitmap.Dispose() }
} finally { if ($embedded) { $embedded.Dispose() } }
Write-Output "PASS $checks icon checks; 24 distinct animation frames."
