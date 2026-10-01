# Streaming SHA-256 without depending on PowerShell module discovery in a subprocess.
function Get-SoundLeafSha256 {
    param([Parameter(Mandatory=$true)][string]$LiteralPath)
    $inputStream = [IO.File]::Open([IO.Path]::GetFullPath($LiteralPath), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $algorithm = $null
    try {
        $algorithm = [Security.Cryptography.SHA256]::Create()
        return [BitConverter]::ToString($algorithm.ComputeHash($inputStream)).Replace('-', '')
    } finally { if ($algorithm) { $algorithm.Dispose() }; $inputStream.Dispose() }
}
