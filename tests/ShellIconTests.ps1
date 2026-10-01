$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if (!('SoundLeaf.ShellIconRefresh' -as [type])) { Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $project 'ShellIconRefresh.cs') -Raw -Encoding UTF8) -ReferencedAssemblies System.Drawing }
$testRoot = Join-Path $project ('Verification\shell-icons-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($testRoot)
$oldIco = Join-Path $testRoot 'old.ico'
[IO.File]::WriteAllBytes($oldIco, [SoundLeaf.SoundLeafIcons]::IcoBytes(3,0))
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
try {
    foreach ($version in @('old','new')) {
        $options = New-Object System.CodeDom.Compiler.CompilerParameters
        $options.GenerateExecutable = $true
        $options.OutputAssembly = Join-Path $testRoot ($version + '.exe')
        $ico = if ($version -eq 'old') { $oldIco } else { Join-Path $project 'assets\SoundLeaf.ico' }
        $options.CompilerOptions = '/target:winexe /platform:x64 /win32icon:"' + $ico + '"'
        $result = $provider.CompileAssemblyFromSource($options, 'public static class IconOnly { public static void Main() {} }')
        if ($result.Errors.HasErrors) { throw 'Shell fixture compilation failed.' }
    }
} finally { $provider.Dispose() }
$target = Join-Path $testRoot 'same-path.exe'
[IO.File]::Copy((Join-Path $testRoot 'old.exe'), $target, $false)
$previous = [SoundLeaf.ShellIconRefresh]::Capture($target)
$before = [SoundLeaf.ShellIconRefresh]::ReadCached($target,4)
$expected = [SoundLeaf.ShellIconRefresh]::ReadCached((Join-Path $testRoot 'new.exe'),4)
function Test-SamePixels($first, $second) {
    if ($first.Size -ne $second.Size) { return $false }
    foreach ($y in 0..($first.Height-1)) { foreach ($x in 0..($first.Width-1)) {
        $a = $first.GetPixel($x,$y); $b = $second.GetPixel($x,$y)
        if ($a.A -eq 0 -and $b.A -eq 0) { continue }
        if ($a.ToArgb() -ne $b.ToArgb()) { return $false }
    } }
    return $true
}
try {
    if (Test-SamePixels $before $expected) { throw 'Old/new fixture icons are not distinct.' }
    $before.Save((Join-Path $testRoot 'before.png'))
    [IO.File]::Replace((Join-Path $testRoot 'new.exe'), $target, (Join-Path $testRoot 'backup.exe'))
    [SoundLeaf.ShellIconRefresh]::Refresh($previous)
    [SoundLeaf.ShellIconRefresh]::InvalidateShellArtwork()
    # A bare PowerShell process is not a registered shell-view change listener.
    # Read back in a new process rather than mistaking its private image list for Explorer.
    $arguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+(Join-Path $PSScriptRoot 'ShellIconReadback.ps1')+'"'),'-Target',('"'+$target+'"'),'-Reference',('"'+(Join-Path $project 'SoundLeaf.next.exe')+'"'),'-Output',('"'+(Join-Path $testRoot 'after.png')+'"'))
    $readbackPassed = $false
    foreach ($attempt in 1..5) {
        $reader = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $testRoot ("readback-$attempt.log"))
        $readerHandle = $reader.Handle
        if (!$reader.WaitForExit(5000)) { $reader.Kill(); throw 'Shell readback fixture timed out.' }
        $reader.WaitForExit(); $reader.Refresh()
        if ($null -eq $reader.ExitCode) { throw 'Shell fixture exit code unavailable; success cannot be inferred.' }
        if ($reader.ExitCode -eq 0) { $readbackPassed = $true; break }
        [Threading.Thread]::Sleep(250)
    }
    if (!$readbackPassed) { throw "Fresh-process shell readback differs after shell artwork invalidation; details: $testRoot" }
    Write-Output 'PASS 6 shell-cache checks; distinct old/new icons and fresh-process same-path readback at 4 shell sizes after shell artwork invalidation. No Explorer restart or cache-file deletion; not an assertion about every existing Explorer view.'
} finally { $before.Dispose(); $expected.Dispose() }
