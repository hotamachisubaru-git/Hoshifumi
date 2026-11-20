# Check the actual Windows caption and icon, including the SDL window recreated at startup.
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class HoshifumiWindow {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, UIntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
}
'@

function Test-EnvelopeIcon([Drawing.Icon]$Icon) {
    $bitmap = $Icon.ToBitmap()
    try {
        # The teal envelope shadow and gold star distinguish the artwork from the MonoGame logo.
        $teal = $false
        $gold = $false
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                $c = $bitmap.GetPixel($x, $y)
                if ($c.A -lt 200) { continue }
                if ($c.R -lt 60 -and $c.G -gt 100 -and $c.B -gt 100) { $teal = $true }
                if ($c.R -gt 220 -and $c.G -gt 145 -and $c.B -lt 115) { $gold = $true }
            }
        }
        if (-not ($teal -and $gold)) { throw 'The star-envelope artwork was not found.' }
    } finally { $bitmap.Dispose() }
}

$exe = (Resolve-Path $Executable).Path
$fileIcon = [Drawing.Icon]::ExtractAssociatedIcon($exe)
try { Test-EnvelopeIcon $fileIcon } finally { $fileIcon.Dispose() }

$expected = '星ふみ郵便局 | HOSHIFUMI'
$game = Start-Process -FilePath $exe -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do {
        Start-Sleep -Milliseconds 200
        $game.Refresh()
        if ($game.HasExited) { throw "The game exited before showing its window ($($game.ExitCode))." }
        if ($game.MainWindowHandle -ne [IntPtr]::Zero -and $game.MainWindowTitle -ceq $expected) { break }
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($game.MainWindowTitle -cne $expected) { throw "Unexpected Windows caption: '$($game.MainWindowTitle)'." }

    $iconHandle = [UIntPtr]::Zero
    # WM_GETICON, ICON_BIG. The icon returned by the window remains owned by SDL.
    $response = [HoshifumiWindow]::SendMessageTimeout($game.MainWindowHandle, 0x7f, [UIntPtr]1,
        [IntPtr]::Zero, 2, 2000, [ref]$iconHandle)
    if ($response -eq [IntPtr]::Zero -or $iconHandle -eq [UIntPtr]::Zero) { throw 'No window icon.' }
    $icon = [Drawing.Icon]::FromHandle([IntPtr]([long]$iconHandle.ToUInt64()))
    try { Test-EnvelopeIcon $icon } finally { $icon.Dispose() }
    Write-Host "PASS: '$expected', native window icon, and executable icon."
} finally {
    if (-not $game.HasExited) { $game.Kill(); $game.WaitForExit() }
    $game.Dispose()
}
